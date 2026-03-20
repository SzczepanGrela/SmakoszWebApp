using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Smakosz.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmakoszDbContext>
{
    public SmakoszDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SMAKOSZ_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=smakosz;Username=smakosz;Password=***REMOVED***";

        var optionsBuilder = new DbContextOptionsBuilder<SmakoszDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new SmakoszDbContext(optionsBuilder.Options);
    }
}
