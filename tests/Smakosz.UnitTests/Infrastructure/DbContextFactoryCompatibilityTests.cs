using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.UnitTests.Infrastructure;

[Trait("Category", "DependencyInjection")]
public class DbContextFactoryCompatibilityTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly string _dbName = $"DbFactorySpike_{Guid.NewGuid():N}";

    public DbContextFactoryCompatibilityTests()
    {
        Action<DbContextOptionsBuilder> configure = opts =>
            opts.UseInMemoryDatabase(_dbName);

        _sp = new ServiceCollection()
            .AddDbContext<SmakoszDbContext>(configure)
            .AddDbContextFactory<SmakoszDbContext>(configure, lifetime: ServiceLifetime.Singleton)
            .AddScoped<ISmakoszDbContext>(sp => sp.GetRequiredService<SmakoszDbContext>())
            .BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void Factory_IsRegistered_AndResolvable()
    {
        var factory = _sp.GetService<IDbContextFactory<SmakoszDbContext>>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public void ScopedContext_AndFactory_BothResolvable_OnSameType()
    {
        using var scope = _sp.CreateScope();
        var scoped = scope.ServiceProvider.GetService<SmakoszDbContext>();
        var factory = _sp.GetService<IDbContextFactory<SmakoszDbContext>>();

        scoped.Should().NotBeNull();
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task FactoryCreatedContexts_ShareInMemoryState_ByDbName()
    {
        var factory = _sp.GetRequiredService<IDbContextFactory<SmakoszDbContext>>();

        await using (var ctx1 = await factory.CreateDbContextAsync())
        {
            ctx1.Set<HomePageCache>().Add(new HomePageCache { Id = 1, TotalDishes = 42 });
            await ctx1.SaveChangesAsync();
        }

        await using var ctx2 = await factory.CreateDbContextAsync();
        var stats = await ctx2.Set<HomePageCache>().AsNoTracking().FirstOrDefaultAsync();

        stats.Should().NotBeNull();
        stats!.TotalDishes.Should().Be(42);
    }

    [Fact]
    public async Task FactoryContexts_RunInParallel_WithoutSecondOperationException()
    {
        var factory = _sp.GetRequiredService<IDbContextFactory<SmakoszDbContext>>();

        await using (var seed = await factory.CreateDbContextAsync())
        {
            seed.Set<HomePageCache>().Add(new HomePageCache { Id = 1, TotalDishes = 7, TotalRestaurants = 7, TotalReviews = 7 });
            await seed.SaveChangesAsync();
        }

        async Task<int> ReadDishesAsync()
        {
            await using var ctx = await factory.CreateDbContextAsync();
            return (await ctx.Set<HomePageCache>().AsNoTracking().FirstAsync()).TotalDishes;
        }

        async Task<int> ReadRestaurantsAsync()
        {
            await using var ctx = await factory.CreateDbContextAsync();
            return (await ctx.Set<HomePageCache>().AsNoTracking().FirstAsync()).TotalRestaurants;
        }

        async Task<int> ReadReviewsAsync()
        {
            await using var ctx = await factory.CreateDbContextAsync();
            return (await ctx.Set<HomePageCache>().AsNoTracking().FirstAsync()).TotalReviews;
        }

        var dishesTask = ReadDishesAsync();
        var restaurantsTask = ReadRestaurantsAsync();
        var reviewsTask = ReadReviewsAsync();

        await Task.WhenAll(dishesTask, restaurantsTask, reviewsTask);

        (await dishesTask).Should().Be(7);
        (await restaurantsTask).Should().Be(7);
        (await reviewsTask).Should().Be(7);
    }
}
