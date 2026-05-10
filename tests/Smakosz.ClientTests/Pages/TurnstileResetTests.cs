using Smakosz.Client.Models;
using Smakosz.Client.Pages.Auth;
using Smakosz.Client.Pages.Public;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages;

public class TurnstileResetTests : BunitTestBase
{
    public TurnstileResetTests()
    {
        JSInterop.SetupVoid("smakoszTurnstile.render", _ => true);
        JSInterop.SetupVoid("smakoszTurnstile.reset", _ => true);
    }

    [Fact]
    public void Login_OnError_ResetsTurnstile()
    {
        var auth = Services.GetRequiredService<IAuthService>();
        auth.LoginAsync(Arg.Any<LoginRequest>())
            .Returns(new ApiResponse<LoginResponse>
            {
                Success = false,
                Error = new ApiError { Code = "AUTH_INVALID_CREDENTIALS", Message = "Bad credentials" }
            });

        var cut = RenderComponent<Login>();
        cut.Find("input[type='email']").Change("test@test.com");
        cut.Find("input[type='password']").Input("wrong");
        cut.Find("form").Submit();

        cut.WaitForState(() => cut.Markup.Contains("Bad credentials") || cut.Markup.Contains("Nieprawidłowy"));
        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("smakoszTurnstile.reset"));
    }

    [Fact]
    public void Register_OnError_ResetsTurnstile()
    {
        var auth = Services.GetRequiredService<IAuthService>();
        auth.RegisterAsync(Arg.Any<RegisterRequest>())
            .Returns(new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError { Code = "AUTH_EMAIL_ALREADY_EXISTS", Message = "Email taken" }
            });

        var cut = RenderComponent<Register>();
        cut.Find("input[type='text']").Change("newuser");
        cut.Find("input[type='email']").Change("test@test.com");
        cut.Find("input[type='password']").Input("password123");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("smakoszTurnstile.reset"));
    }

    [Fact]
    public void ForgotPassword_OnError_ResetsTurnstile()
    {
        var auth = Services.GetRequiredService<IAuthService>();
        auth.ForgotPasswordAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError { Code = "AUTH_NOT_FOUND", Message = "Not found" }
            });

        var cut = RenderComponent<ForgotPassword>();
        cut.Find("input[type='email']").Change("test@test.com");
        cut.Find("button").Click();

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("smakoszTurnstile.reset"));
    }

    [Fact]
    public async Task Contact_OnSend_ResetsTurnstile()
    {
        var content = Services.GetRequiredService<IContentService>();
        content.GetContactPageAsync().Returns(new ContactPageDto { Email = "support@smakosz.xyz" });
        content.SendContactMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(new ContactMessageResult(true, null));

        var cut = RenderComponent<Contact>();
        cut.WaitForState(() => cut.Markup.Contains("Wyślij"));
        cut.FindAll("input")[0].Change("Jan");
        cut.FindAll("input")[1].Change("test@test.com");
        cut.FindAll("input")[2].Change("Tytul");
        cut.Find("textarea").Change("Wiadomosc testowa o dlugosci powyzej 10 znakow.");

        var widget = cut.FindComponent<Smakosz.Client.Components.TurnstileWidget>().Instance;
        await cut.InvokeAsync(() => widget.OnTokenChanged("test-token"));

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("smakoszTurnstile.reset"));
    }
}
