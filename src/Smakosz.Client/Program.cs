using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Smakosz.Client;
using Smakosz.Client.Auth;
using Smakosz.Client.Services;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient with auth handler
builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddHttpClient("SmakoszAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001");
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("SmakoszAPI"));

// Blazored.LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Auth
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

// API Client
builder.Services.AddScoped<SmakoszApiClient>();

// Toast service
builder.Services.AddScoped<ToastService>();

// Real API services
builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IDishService, DishService>();
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// Real API services (continued)
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IMediaService, MediaService>();

await builder.Build().RunAsync();
