using Microsoft.JSInterop;

namespace Smakosz.Client.Services;

public class PushSubscriptionManager
{
    private readonly IJSRuntime _js;
    private readonly SmakoszApiClient _api;

    public PushSubscriptionManager(IJSRuntime js, SmakoszApiClient api)
    {
        _js = js;
        _api = api;
    }

    public async Task<bool> IsSupportedAsync()
    {
        return await _js.InvokeAsync<bool>("smakoszPush.isSupported");
    }

    public async Task<string> GetPermissionAsync()
    {
        return await _js.InvokeAsync<string>("smakoszPush.getPermission");
    }

    public async Task<bool> SubscribeAsync()
    {
        var keyResponse = await _api.GetAsync<VapidKeyResponse>("/api/me/push-public-key");
        if (keyResponse is null || string.IsNullOrEmpty(keyResponse.PublicKey))
            return false;

        var sub = await _js.InvokeAsync<PushSubscriptionDto>("smakoszPush.subscribe", keyResponse.PublicKey);
        if (sub is null)
            return false;

        var response = await _api.PostApiResponseAsync<object>("/api/me/push-subscriptions", new
        {
            endpoint = sub.Endpoint,
            p256dh = sub.P256dh,
            auth = sub.Auth
        });

        return response.Success;
    }

    public async Task<bool> UnsubscribeAsync()
    {
        var sub = await _js.InvokeAsync<PushSubscriptionDto?>("smakoszPush.getSubscription");

        var unsubscribed = await _js.InvokeAsync<bool>("smakoszPush.unsubscribe");

        if (sub is not null)
        {
            await _api.PostApiResponseAsync<object>("/api/me/push-subscriptions/unsubscribe", new
            {
                endpoint = sub.Endpoint
            });
        }

        return unsubscribed;
    }

    public async Task<bool> IsSubscribedAsync()
    {
        var sub = await _js.InvokeAsync<PushSubscriptionDto?>("smakoszPush.getSubscription");
        return sub is not null;
    }

    private record VapidKeyResponse(string PublicKey);
    private record PushSubscriptionDto(string Endpoint, string P256dh, string Auth);
}
