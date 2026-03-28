using Blazored.LocalStorage;
using Bunit.TestDoubles;

namespace Smakosz.ClientTests.Common;

public abstract class BunitTestBase : Bunit.TestContext
{
    protected TestAuthorizationContext AuthContext { get; }

    protected BunitTestBase()
    {
        AuthContext = this.AddTestAuthorization();

        Services.AddSingleton<ToastService>();

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
    }

    protected void SetAuthenticatedUser(string username = "testuser", string role = "User")
    {
        AuthContext.SetAuthorized(username);
        AuthContext.SetRoles(role);
    }
}
