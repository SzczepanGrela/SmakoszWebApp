// Based on the official Blazor WASM PWA template with versioned cache from manifest.

self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('push', event => event.waitUntil(onPush(event)));
self.addEventListener('notificationclick', event => event.waitUntil(onNotificationClick(event)));
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

async function onInstall(event) {
    console.info('Service worker: Install');

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    // One-time migration: remove old hardcoded cache from previous SW version
    await caches.delete('smakosz-cache-v1');
}

async function onFetch(event) {
    let cachedResponse = null;

    if (event.request.method === 'GET') {
        const shouldServeIndexHtml = event.request.mode === 'navigate';
        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
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
