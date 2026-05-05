using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Admin;

public class GetTicketsSummaryTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateAdminUser(99, hash));
            db.Users.Add(SeedHelpers.CreateUser(1, "jan", "jan@test.test", hash));
            await db.SaveChangesAsync();

            db.SystemTickets.AddRange(
                new SystemTicket { TicketType = TicketType.Contact, ReferenceId = 1, Status = TicketStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new SystemTicket { TicketType = TicketType.Contact, ReferenceId = 2, Status = TicketStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new SystemTicket { TicketType = TicketType.Contact, ReferenceId = 3, Status = TicketStatus.Resolved, CreatedAt = DateTime.UtcNow },
                new SystemTicket { TicketType = TicketType.RestaurantClaim, ReferenceId = 1, Status = TicketStatus.Open, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetTicketsSummary_AsAdmin_ReturnsCounts()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/tickets/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await DeserializeResponse<List<TicketSummaryDto>>(response);
        result.Should().NotBeNull();

        var contact = result!.FirstOrDefault(s => s.TicketType == "Contact");
        contact.Should().NotBeNull();
        contact!.OpenCount.Should().Be(2);

        var claim = result!.FirstOrDefault(s => s.TicketType == "RestaurantClaim");
        claim.Should().NotBeNull();
        claim!.OpenCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTicketsSummary_OldestOpenAt_IsEarliestOpenTicket()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/tickets/summary");
        var result = await DeserializeResponse<List<TicketSummaryDto>>(response);

        var contact = result!.First(s => s.TicketType == "Contact");
        contact.OldestOpenAt.Should().NotBeNull();
        contact.OldestOpenAt!.Value.Date.Should().Be(DateTime.UtcNow.AddDays(-3).Date);
    }

    [Fact]
    public async Task GetTicketsSummary_TypeWithOnlyResolvedTickets_NotInResult()
    {
        using var client = Factory.CreateAdminClient(99, "administrator");

        var response = await client.GetAsync("/api/admin/tickets/summary");
        var result = await DeserializeResponse<List<TicketSummaryDto>>(response);

        result!.Should().NotContain(s => s.TicketType == "Photo");
    }
}
