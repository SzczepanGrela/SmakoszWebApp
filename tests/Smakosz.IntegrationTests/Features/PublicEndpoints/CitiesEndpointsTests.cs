using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class CitiesEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.Cities.Add(SeedHelpers.CreateCity(1, "Warszawa"));
            db.Cities.Add(SeedHelpers.CreateCity(2, "Krakow", "Malopolskie"));
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetCities_ReturnsList()
    {
        var response = await AnonymousClient.GetAsync("/api/cities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Warszawa");
    }
}
