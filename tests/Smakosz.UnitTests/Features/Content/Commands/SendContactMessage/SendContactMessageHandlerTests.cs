using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Content.Commands.SendContactMessage;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Content.Commands.SendContactMessage;

[Trait("Category", "Handlers")]
public class SendContactMessageHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IDateTimeProvider _dateTime;
    private readonly IEmailService _email;
    private readonly SendContactMessageHandler _handler;

    public SendContactMessageHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _email = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<SendContactMessageHandler>>();
        var turnstile = Substitute.For<ITurnstileService>();
        turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _handler = new SendContactMessageHandler(_db, _dateTime, _email, logger, turnstile);
    }

    [Fact]
    public async Task Handle_MissingTurnstileToken_ReturnsCaptchaFailed()
    {
        var command = new SendContactMessageCommand("Jan", "jan@example.com", "Pytanie", "To jest testowa wiadomosc kontaktowa");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CAPTCHA_FAILED");
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesTicketAndReturnsSuccess()
    {
        var command = new SendContactMessageCommand("Jan", "jan@example.com", "Pytanie", "To jest testowa wiadomosc kontaktowa", "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets.Should().HaveCount(1);
        _sets.SystemTickets[0].TicketType.Should().Be(Domain.Enums.TicketType.Contact);
    }

    [Fact]
    public async Task Handle_HappyPath_SendsConfirmationEmail()
    {
        var command = new SendContactMessageCommand("Jan", "jan@example.com", "Pytanie", "To jest testowa wiadomosc kontaktowa", "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        await _email.Received(1).SendDigestAsync(
            "jan@example.com",
            Arg.Is<string>(s => s.Contains("potwierdzenie")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}

[Trait("Category", "Validators")]
public class SendContactMessageValidatorTests
{
    private readonly SendContactMessageValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new SendContactMessageCommand("Jan", "jan@example.com", "Pytanie", "To jest testowa wiadomosc kontaktowa", "valid-token");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "jan@example.com", "Temat", "Wiadomosc testowa min")]
    [InlineData("Jan", "", "Temat", "Wiadomosc testowa min")]
    [InlineData("Jan", "invalid-email", "Temat", "Wiadomosc testowa min")]
    [InlineData("Jan", "jan@example.com", "", "Wiadomosc testowa min")]
    [InlineData("Jan", "jan@example.com", "Temat", "krotka")]
    public void Validate_InvalidFields_ReturnsErrors(string name, string email, string subject, string message)
    {
        var command = new SendContactMessageCommand(name, email, subject, message);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
