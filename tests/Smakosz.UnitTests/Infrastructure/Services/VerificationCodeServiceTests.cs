using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Services;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Services")]
public class VerificationCodeServiceTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICodeHasher _codeHasher;
    private readonly VerificationCodeService _sut;

    public VerificationCodeServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _codeHasher = Substitute.For<ICodeHasher>();
        _codeHasher.Hash(Arg.Any<string>()).Returns("hashed");
        _sut = new VerificationCodeService(_db, _codeHasher);
    }

    [Fact]
    public async Task CreateCodeAsync_ReturnsCodeWith6Digits()
    {
        var code = await _sut.CreateCodeAsync(1, VerificationCodeType.Register, CancellationToken.None);

        int.Parse(code).Should().BeInRange(100000, 999999);
    }

    [Fact]
    public async Task CreateCodeAsync_CallsCodeHasherWithCode()
    {
        var code = await _sut.CreateCodeAsync(1, VerificationCodeType.Register, CancellationToken.None);

        _codeHasher.Received(1).Hash(code);
    }

    [Fact]
    public async Task CreateCodeAsync_AddsEntityToDbSet()
    {
        await _sut.CreateCodeAsync(42, VerificationCodeType.ResetPassword, CancellationToken.None);

        _sets.VerificationCodes.Should().HaveCount(1);
        var entity = _sets.VerificationCodes[0];
        entity.UserId.Should().Be(42);
        entity.CodeHash.Should().Be("hashed");
        entity.Type.Should().Be(VerificationCodeType.ResetPassword);
    }

    [Fact]
    public async Task CreateCodeAsync_UsesDefaultTtl_WhenNoConfig()
    {
        await _sut.CreateCodeAsync(1, VerificationCodeType.Register, CancellationToken.None);

        var entity = _sets.VerificationCodes[0];
        entity.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateCodeAsync_UsesFallbackTtl_WhenConfigInvalid()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "auth.verify_code_ttl_min", Value = "invalid" });
        DbContextMockFactory.Refresh(_db, _sets);

        await _sut.CreateCodeAsync(1, VerificationCodeType.Register, CancellationToken.None);

        var entity = _sets.VerificationCodes[0];
        entity.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateCodeAsync_UsesCustomTtl_FromConfig()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "auth.verify_code_ttl_min", Value = "10" });
        DbContextMockFactory.Refresh(_db, _sets);

        await _sut.CreateCodeAsync(1, VerificationCodeType.Register, CancellationToken.None);

        var entity = _sets.VerificationCodes[0];
        entity.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(5));
    }
}
