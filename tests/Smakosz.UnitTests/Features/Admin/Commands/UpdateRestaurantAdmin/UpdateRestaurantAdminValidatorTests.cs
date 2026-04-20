using FluentAssertions;
using FluentValidation.TestHelper;
using Smakosz.Application.Features.Admin.Commands.UpdateRestaurantAdmin;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateRestaurantAdmin;

[Trait("Category", "Validators")]
public class UpdateRestaurantAdminValidatorTests
{
    private readonly UpdateRestaurantAdminValidator _validator = new();

    private static UpdateRestaurantAdminCommand Cmd(
        string? name = null, string? description = null, string? email = null,
        string? phone = null, string? website = null, int? priceLevel = null,
        string? postalCode = null, int? cuisineTypeId = null, int? cityId = null,
        int expectedVersion = 1)
        => new(Guid.NewGuid(), name, description, cuisineTypeId, priceLevel,
            null, postalCode, phone, email, website, cityId, expectedVersion);

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    public void Name_TooShort_Fails(string name)
    {
        var result = _validator.TestValidate(Cmd(name: name));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Null_Passes()
    {
        var result = _validator.TestValidate(Cmd());
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_Valid_Passes()
    {
        var result = _validator.TestValidate(Cmd(name: "Bella Italia"));
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("not-email")]
    [InlineData("@missing.local")]
    public void Email_Invalid_Fails(string email)
    {
        var result = _validator.TestValidate(Cmd(email: email));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_Valid_Passes()
    {
        var result = _validator.TestValidate(Cmd(email: "a@b.com"));
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Phone_Invalid_Fails()
    {
        var result = _validator.TestValidate(Cmd(phone: "abc"));
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Phone_Valid_Passes()
    {
        var result = _validator.TestValidate(Cmd(phone: "+48 123 456 789"));
        result.ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Website_Invalid_Fails()
    {
        var result = _validator.TestValidate(Cmd(website: "no-protocol.com"));
        result.ShouldHaveValidationErrorFor(x => x.Website);
    }

    [Fact]
    public void Website_Valid_Passes()
    {
        var result = _validator.TestValidate(Cmd(website: "https://example.com"));
        result.ShouldNotHaveValidationErrorFor(x => x.Website);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void PriceLevel_OutOfRange_Fails(int level)
    {
        var result = _validator.TestValidate(Cmd(priceLevel: level));
        result.ShouldHaveValidationErrorFor(x => x.PriceLevel);
    }

    [Fact]
    public void PriceLevel_Valid_Passes()
    {
        var result = _validator.TestValidate(Cmd(priceLevel: 3));
        result.ShouldNotHaveValidationErrorFor(x => x.PriceLevel);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1-234")]
    public void PostalCode_Invalid_Fails(string code)
    {
        var result = _validator.TestValidate(Cmd(postalCode: code));
        result.ShouldHaveValidationErrorFor(x => x.PostalCode);
    }

    [Fact]
    public void PostalCode_Valid_Passes()
    {
        var result = _validator.TestValidate(Cmd(postalCode: "30-001"));
        result.ShouldNotHaveValidationErrorFor(x => x.PostalCode);
    }

    [Fact]
    public void ExpectedVersion_Zero_Fails()
    {
        var result = _validator.TestValidate(Cmd(expectedVersion: 0));
        result.ShouldHaveValidationErrorFor(x => x.ExpectedVersion);
    }

    [Fact]
    public void CityId_Zero_Fails()
    {
        var result = _validator.TestValidate(Cmd(cityId: 0));
        result.ShouldHaveValidationErrorFor(x => x.CityId);
    }
}
