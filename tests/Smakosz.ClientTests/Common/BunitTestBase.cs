using Blazored.LocalStorage;
using Bunit.TestDoubles;
using Microsoft.Extensions.Configuration;

namespace Smakosz.ClientTests.Common;

public abstract class BunitTestBase : Bunit.TestContext
{
    protected TestAuthorizationContext AuthContext { get; }

    protected BunitTestBase()
    {
        AuthContext = this.AddTestAuthorization();

        Services.AddSingleton<ToastService>();
        Services.AddSingleton<IConfirmService>(new AutoConfirmService());

        Services.AddSingleton(Substitute.For<IHomeService>());
        Services.AddSingleton(Substitute.For<IDishService>());
        Services.AddSingleton(Substitute.For<IReviewService>());
        Services.AddSingleton(Substitute.For<IUserProfileService>());
        Services.AddSingleton(Substitute.For<ISearchService>());
        Services.AddSingleton(Substitute.For<INotificationService>());
        Services.AddSingleton(Substitute.For<IMediaService>());
        Services.AddSingleton(Substitute.For<IBusinessService>());
        Services.AddSingleton(Substitute.For<IAdminService>());
        Services.AddSingleton(Substitute.For<IAuthService>());
        Services.AddSingleton(Substitute.For<IContentService>());
        Services.AddSingleton(Substitute.For<IRecommendationService>());
        Services.AddSingleton(Substitute.For<IRestaurantService>());
        Services.AddSingleton(Substitute.For<ILocalStorageService>());
        Services.AddSingleton(Substitute.For<IPublicConfigService>());
        Services.AddSingleton(Substitute.For<ICategoryService>());
        Services.AddSingleton(Substitute.For<IScrollPositionService>());
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Turnstile:SiteKey"] = "1x00000000000000000000AA"
            })
            .Build());
    }

    protected void SetAuthenticatedUser(string username = "testuser", string role = "User")
    {
        AuthContext.SetAuthorized(username);
        AuthContext.SetRoles(role);
    }

    // Auto-confirm prompts in tests so destructive actions wired through IConfirmService are exercised end-to-end.
    private sealed class AutoConfirmService : IConfirmService
    {
        public event Action? StateChanged;
        public bool IsOpen => false;
        public string Message => "";
        public Task<bool> AskAsync(string message)
        {
            StateChanged?.Invoke();
            return Task.FromResult(true);
        }
        public void Confirm() { }
        public void Cancel() { }
    }
}
