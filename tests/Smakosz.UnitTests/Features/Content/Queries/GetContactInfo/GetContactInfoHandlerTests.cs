using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Content.Queries.GetContactInfo;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Content.Queries.GetContactInfo;

[Trait("Category", "Handlers")]
public class GetContactInfoHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetContactInfoHandler _handler;

    public GetContactInfoHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetContactInfoHandler(_db);
    }

    [Fact]
    public async Task Handle_ReturnsContactInfo()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "contact.email", Value = "info@smakosz.pl", IsPublic = true });
        _sets.SystemConfigs.Add(new SystemConfig { Key = "contact.phone", Value = "+48123456789", IsPublic = true });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetContactInfoQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("info@smakosz.pl");
        result.Value.Phone.Should().Be("+48123456789");
    }

    [Fact]
    public async Task Handle_MissingConfigs_ReturnsNullFields()
    {
        var result = await _handler.Handle(new GetContactInfoQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().BeNull();
        result.Value.Phone.Should().BeNull();
        result.Value.Address.Should().BeNull();
    }
}
