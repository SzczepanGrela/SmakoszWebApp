using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Smakosz.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Smakosz.IntegrationTests.Infrastructure;

// Static singleton because xUnit instantiates IntegrationTestBase per test and we want to share one container across the whole assembly.
// Testcontainers Ryuk handles container shutdown after the test process exits, so we never dispose it explicitly.
public static class PostgresFixture
{
    private static PostgreSqlContainer? _container;
    private static Respawner? _respawner;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public static string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("PostgresFixture has not been started. Call EnsureStartedAsync first.");

    public static async Task EnsureStartedAsync()
    {
        if (_container is not null) return;

        await _initLock.WaitAsync();
        try
        {
            if (_container is not null) return;

            var container = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("smakosz_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await container.StartAsync();

            // Override appsettings.json connection string before any WebApplicationFactory builds Program.cs.
            // Program.cs reads GetConnectionString("DefaultConnection") before Build(), so ConfigureAppConfiguration on the WAF
            // applies too late and the API would otherwise dial the developer's local Postgres instead of the test container.
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", container.GetConnectionString());

            // unaccent + pg_trgm extensions are required by f_unaccent function and trigram indexes that ApplySqlObjectsAsync recreates.
            await using (var bootstrap = new NpgsqlConnection(container.GetConnectionString()))
            {
                await bootstrap.OpenAsync();
                await using var cmd = bootstrap.CreateCommand();
                cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS unaccent; CREATE EXTENSION IF NOT EXISTS pg_trgm;";
                await cmd.ExecuteNonQueryAsync();
            }

            var optionsBuilder = new DbContextOptionsBuilder<SmakoszDbContext>();
            optionsBuilder.UseNpgsql(container.GetConnectionString())
                .UseSnakeCaseNamingConvention();
            await using (var ctx = new SmakoszDbContext(optionsBuilder.Options))
            {
                await ctx.Database.MigrateAsync();
            }

            await using (var respawnConn = new NpgsqlConnection(container.GetConnectionString()))
            {
                await respawnConn.OpenAsync();
                _respawner = await Respawner.CreateAsync(respawnConn, new RespawnerOptions
                {
                    DbAdapter = DbAdapter.Postgres,
                    SchemasToInclude = new[] { "public", "system" },
                    TablesToIgnore = new Respawn.Graph.Table[] { "__EFMigrationsHistory" }
                });
            }

            _container = container;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public static async Task ResetAsync()
    {
        if (_respawner is null) return;
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    // For every column whose default uses a serial/identity sequence, set the sequence's current value to the max id present in the column so the next nextval() returns a free id even after explicit-id seeding bypassed the sequence.
    public static async Task AdvanceSequencesAsync()
    {
        const string sql = @"
            DO $$
            DECLARE
                rec RECORD;
                seq_name TEXT;
                max_val BIGINT;
            BEGIN
                FOR rec IN
                    SELECT n.nspname AS schema_name, c.relname AS table_name, a.attname AS column_name
                    FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
                    WHERE c.relkind = 'r'
                      AND n.nspname IN ('public', 'system')
                      AND a.atttypid IN ('int2'::regtype, 'int4'::regtype, 'int8'::regtype)
                LOOP
                    seq_name := pg_get_serial_sequence(format('%I.%I', rec.schema_name, rec.table_name), rec.column_name);
                    IF seq_name IS NULL THEN CONTINUE; END IF;
                    EXECUTE format('SELECT COALESCE(MAX(%I), 0) FROM %I.%I', rec.column_name, rec.schema_name, rec.table_name) INTO max_val;
                    IF max_val > 0 THEN
                        PERFORM setval(seq_name, max_val);
                    END IF;
                END LOOP;
            END $$;";

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
