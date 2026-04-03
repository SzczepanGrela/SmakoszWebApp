const CACHE_NAME = 'smakosz-cache-v1';

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('push', event => event.waitUntil(onPush(event)));
self.addEventListener('notificationclick', event => event.waitUntil(onNotificationClick(event)));

async function onInstall(event) {
    console.info('Service worker: Install');
    const assetsManifest = await caches.match('service-worker-assets.js');
    // Cache framework files
}

async function onActivate(event) {
    console.info('Service worker: Activate');
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key !== CACHE_NAME)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    // For navigation requests, serve index.html from cache
    if (event.request.mode === 'navigate') {
        try {
            return await fetch(event.request);
        } catch {
            return caches.match('index.html');
        }
    }

    // For other requests, try cache first, then network
    const cachedResponse = await caches.match(event.request);
    if (cachedResponse) {
        return cachedResponse;
    }

    try {
        const response = await fetch(event.request);
        return response;
    } catch {
        return new Response('', { status: 408, statusText: 'Request timed out.' });
    }
}

async function onPush(event) {
    var data = { title: 'Smakosz', body: 'Masz nowe powiadomienie' };
    try {
        data = event.data.json();
    } catch { }

    await self.registration.showNotification(data.title, {
        body: data.body,
        icon: '/favicon-96x96.png',
        badge: '/favicon-96x96.png',
        data: { url: data.url || '/' }
    });
}

async function onNotificationClick(event) {
    event.notification.close();
    var url = event.notification.data?.url || '/';

    var allClients = await clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (var i = 0; i < allClients.length; i++) {
        if (allClients[i].url.includes(self.location.origin)) {
            allClients[i].focus();
            allClients[i].navigate(url);
            return;
        }
    }
    await clients.openWindow(url);
}
