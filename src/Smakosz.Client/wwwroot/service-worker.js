// Caution! Be sure you understand the caveats before publishing an application with
// temporary/incremental service worker support. Some of the considerations include:
//
// - An update to any file within wwwroot will result in the whole service worker being replaced.
// - The service worker is only guaranteed to be able to cache the assets requested at install time.
//   If it fails to cache any file, it must abort. The next attempt will be made when there's a
//   newer service worker.
//
// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });
self.addEventListener('push', () => { });
self.addEventListener('notificationclick', () => { });
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});
