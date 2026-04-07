using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Services;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Services")]
public class CachedConfigProviderTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly MemoryCache _cache;
    private readonly CachedConfigProvider _sut;

    public CachedConfigProviderTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISmakoszDbContext)).Returns(_db);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _sut = new CachedConfigProvider(scopeFactory, _cache);
    }

    [Fact]
    public async Task GetPublicConfigAsync_ReturnsOnlyPublicConfigs()
    {
        _sets.SystemConfigs.AddRange(new[]
        {
            new SystemConfig { Key = "pub1", Value = "v1", IsPublic = true },
            new SystemConfig { Key = "pub2", Value = "v2", IsPublic = true },
            new SystemConfig { Key = "priv1", Value = "secret", IsPublic = false },
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.GetPublicConfigAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainKeys("pub1", "pub2");
        result.Should().NotContainKey("priv1");
    }

    [Fact]
    public async Task GetIntAsync_ReturnsParsedInt()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "k", Value = "42" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.GetIntAsync("k", 0, CancellationToken.None);

        result.Should().Be(42);
    }

    [Fact]
    public async Task GetIntAsync_ReturnsDefault_WhenKeyMissing()
    {
        var result = await _sut.GetIntAsync("missing", 99, CancellationToken.None);

        result.Should().Be(99);
    }

    [Fact]
    public async Task GetBoolAsync_ReturnsParsedBool()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "flag", Value = "true" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.GetBoolAsync("flag", false, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetValueAsync_ReturnsNull_WhenKeyMissing()
    {
        var result = await _sut.GetValueAsync("missing", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public void GetInt_Sync_ReturnsParsedInt()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "k", Value = "7" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = _sut.GetInt("k", 0);

        result.Should().Be(7);
    }

    [Fact]
    public async Task InvalidateCache_ClearsCache_SubsequentCallReReadsDb()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "k", Value = "10" });
        DbContextMockFactory.Refresh(_db, _sets);

        var first = await _sut.GetIntAsync("k", 0, CancellationToken.None);
        first.Should().Be(10);

        _sets.SystemConfigs[0].Value = "20";
        DbContextMockFactory.Refresh(_db, _sets);

        var cached = await _sut.GetIntAsync("k", 0, CancellationToken.None);
        cached.Should().Be(10);

        _sut.InvalidateCache();

        var refreshed = await _sut.GetIntAsync("k", 0, CancellationToken.None);
        refreshed.Should().Be(20);
    }
}
