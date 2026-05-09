using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Services;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Services")]
public class SessionServiceTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _jwtTokenService.GenerateRefreshToken().Returns("raw_token");
        _sut = new SessionService(_db, _jwtTokenService);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public async Task CreateSession_AddsSessionToDbSet()
    {
        var result = await _sut.CreateSessionAsync(42, false, CancellationToken.None);

        _sets.UserSessions.Should().ContainSingle();
        _sets.UserSessions[0].UserId.Should().Be(42);
        _sets.UserSessions[0].IsRememberMe.Should().BeFalse();
    }

    [Fact]
    public async Task CreateSession_StoresHashNotPlaintext()
    {
        await _sut.CreateSessionAsync(1, false, CancellationToken.None);

        _sets.UserSessions[0].RefreshTokenHash.Should().NotBe("raw_token");
        _sets.UserSessions[0].RefreshTokenHash.Should().Be(HashToken("raw_token"));
    }

    [Fact]
    public async Task CreateSession_ReturnsPlaintextToken()
    {
        var result = await _sut.CreateSessionAsync(1, false, CancellationToken.None);

        result.Token.Should().Be("raw_token");
    }

    [Fact]
    public async Task CreateSession_ResultExpiresAtMatchesPersistedSession()
    {
        var result = await _sut.CreateSessionAsync(1, true, CancellationToken.None);

        result.ExpiresAt.Should().Be(_sets.UserSessions[0].ExpiresAt);
    }

    [Fact]
    public async Task CreateSession_RememberMe_SetsLongerExpiry()
    {
        var before = DateTime.UtcNow;

        await _sut.CreateSessionAsync(1, true, CancellationToken.None);

        _sets.UserSessions[0].IsRememberMe.Should().BeTrue();
        _sets.UserSessions[0].ExpiresAt.Should().BeCloseTo(before.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSession_NoRememberMe_SetsStandardExpiry()
    {
        var before = DateTime.UtcNow;

        await _sut.CreateSessionAsync(1, false, CancellationToken.None);

        _sets.UserSessions[0].ExpiresAt.Should().BeCloseTo(before.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSession_ReadsConfigFromDb()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "auth.refresh_ttl_days", Value = "14" });
        DbContextMockFactory.Refresh(_db, _sets);

        var before = DateTime.UtcNow;
        await _sut.CreateSessionAsync(1, false, CancellationToken.None);

        _sets.UserSessions[0].ExpiresAt.Should().BeCloseTo(before.AddDays(14), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FindActiveSession_ReturnsSession_WhenHashMatches()
    {
        var hash = HashToken("raw_token");
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash(hash)
            .Build();
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.FindActiveSessionAsync("raw_token", CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeSameAs(session);
    }

    [Fact]
    public async Task FindActiveSession_ReturnsNull_WhenTokenInvalid()
    {
        var hash = HashToken("raw_token");
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash(hash)
            .Build();
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.FindActiveSessionAsync("wrong_token", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindActiveSession_ReturnsNull_WhenRevoked()
    {
        var hash = HashToken("raw_token");
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash(hash)
            .AsRevoked()
            .Build();
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.FindActiveSessionAsync("raw_token", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindActiveSession_ReturnsNull_WhenExpired()
    {
        var hash = HashToken("raw_token");
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash(hash)
            .AsExpired()
            .Build();
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.FindActiveSessionAsync("raw_token", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindSessionForLogout_ReturnsSession_WhenExpired()
    {
        var hash = HashToken("raw_token");
        var user = new UserBuilder().Build();
        var session = new UserSessionBuilder()
            .WithUser(user)
            .WithRefreshTokenHash(hash)
            .AsExpired()
            .Build();
        _sets.UserSessions.Add(session);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.FindSessionForLogoutAsync("raw_token", CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeSameAs(session);
    }

    [Fact]
    public void RevokeSession_SetsIsRevokedTrue()
    {
        var session = new UserSessionBuilder().Build();

        _sut.RevokeSession(session);

        session.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RotateSession_SlidingWindow_ExtendsExpiry()
    {
        var oldSession = new UserSessionBuilder()
            .WithUser(new UserBuilder().Build())
            .WithExpiresAt(DateTime.UtcNow.AddDays(1))
            .Build();

        var before = DateTime.UtcNow;
        await _sut.RotateSessionAsync(oldSession, CancellationToken.None);

        oldSession.IsRevoked.Should().BeTrue();
        _sets.UserSessions.Should().ContainSingle();
        _sets.UserSessions[0].ExpiresAt.Should().BeCloseTo(before.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RotateSession_RememberMe_UsesLongerTtl()
    {
        var oldSession = new UserSessionBuilder()
            .WithUser(new UserBuilder().Build())
            .AsRememberMe()
            .Build();

        var before = DateTime.UtcNow;
        await _sut.RotateSessionAsync(oldSession, CancellationToken.None);

        _sets.UserSessions[0].IsRememberMe.Should().BeTrue();
        _sets.UserSessions[0].ExpiresAt.Should().BeCloseTo(before.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAccessTokenLifetimeSeconds_ReadsFromConfig()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "auth.access_ttl_sec", Value = "600" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _sut.GetAccessTokenLifetimeSecondsAsync(CancellationToken.None);

        result.Should().Be(600);
    }

    [Fact]
    public async Task GetAccessTokenLifetimeSeconds_DefaultsTo900()
    {
        var result = await _sut.GetAccessTokenLifetimeSecondsAsync(CancellationToken.None);

        result.Should().Be(900);
    }
}
