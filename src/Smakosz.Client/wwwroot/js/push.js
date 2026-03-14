window.smakoszPush = {
    isSupported: function () {
        return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    },

    getPermission: function () {
        return Notification.permission;
    },

    subscribe: async function (vapidPublicKey) {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
        });

        const json = subscription.toJSON();
        return {
            endpoint: json.endpoint,
            p256dh: json.keys.p256dh,
            auth: json.keys.auth
        };
    },

    getSubscription: async function () {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        if (!subscription) return null;

        const json = subscription.toJSON();
        return {
            endpoint: json.endpoint,
            p256dh: json.keys.p256dh,
            auth: json.keys.auth
        };
    },

    unsubscribe: async function () {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        if (subscription) {
            await subscription.unsubscribe();
            return true;
        }
        return false;
    }
};

function urlBase64ToUint8Array(base64String) {
    var padding = '='.repeat((4 - base64String.length % 4) % 4);
    var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    var rawData = window.atob(base64);
    var outputArray = new Uint8Array(rawData.length);
    for (var i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}
