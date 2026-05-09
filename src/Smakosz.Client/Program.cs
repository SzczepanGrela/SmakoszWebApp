using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Smakosz.Client;
using Smakosz.Client.Auth;
using Smakosz.Client.Services;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddHttpClient("SmakoszAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
        ?? builder.HostEnvironment.BaseAddress.TrimEnd('/'));
}).AddHttpMessageHandler<AuthTokenHandler>();

// Raw client without AuthTokenHandler so TokenRefreshService can call /api/auth/refresh without recursion.
builder.Services.AddHttpClient(TokenRefreshService.RawClientName, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
        ?? builder.HostEnvironment.BaseAddress.TrimEnd('/'));
});

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("SmakoszAPI"));

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ITokenRefreshService, TokenRefreshService>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

builder.Services.AddScoped<SmakoszApiClient>();

builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<IConfirmService, ConfirmService>();
builder.Services.AddScoped<IConcurrencyConflictService, ConcurrencyConflictService>();

builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IDishService, DishService>();
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<PushSubscriptionManager>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IIngredientService, IngredientService>();
builder.Services.AddScoped<IPublicConfigService, PublicConfigService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

await builder.Build().RunAsync();
