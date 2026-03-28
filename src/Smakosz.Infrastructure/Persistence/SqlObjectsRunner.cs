using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Smakosz.Infrastructure.Persistence;

public static class SqlObjectsRunner
{
    public static async Task ApplySqlObjectsAsync(this SmakoszDbContext context,
        CancellationToken cancellationToken = default)
    {
        var folders = new[] { "Functions", "Views", "Indexes", "Triggers" };
        var assembly = typeof(SmakoszDbContext).Assembly;

        foreach (var folder in folders)
        {
            var prefix = $"Smakosz.Infrastructure.SqlObjects.{folder}.";
            var resources = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(prefix) && n.EndsWith(".sql"))
                .OrderBy(n => n);

            foreach (var name in resources)
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                var sql = await reader.ReadToEndAsync(cancellationToken);
                
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                if (context.Database.CurrentTransaction != null)
                {
                    command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
                }

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}
