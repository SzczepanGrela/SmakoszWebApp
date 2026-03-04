using Microsoft.EntityFrameworkCore;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.E2E.Infrastructure;

public static class E2EDatabaseSeeder
{
    public static async Task SeedAsync()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        await context.ApplySqlObjectsAsync();

        await Smakosz.E2E.SeedData.SeedData.SeedAsync(context);
    }

    public static async Task CleanupAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    public static SmakoszDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmakoszDbContext>()
            .UseNpgsql(TestConstants.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SmakoszDbContext(options);
    }
}
