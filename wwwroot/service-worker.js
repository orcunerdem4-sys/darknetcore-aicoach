const CACHE_NAME = 'dopamind-v3';
const urlsToCache = [
    '/css/site.css',
    '/js/dashboard.js',
    '/favicon.ico',
    '/manifest.json'
];

self.addEventListener('install', event => {
    self.skipWaiting();
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(urlsToCache))
            .catch(console.error)
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(clients.claim());

    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cacheName => {
                    if (cacheName !== CACHE_NAME) {
                        return caches.delete(cacheName);
                    }
                })
            );
        })
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    event.respondWith(
        fetch(event.request)
            .then(response => {
                if (response && response.status === 200 && response.type === 'basic' &&
                    !event.request.url.includes('/api/') && !event.request.url.includes('/Dashboard/')) {
                    const responseToCache = response.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(event.request, responseToCache));
                }
                return response;
            })
            .catch(() => {
                return caches.match(event.request);
            })
    );
});

// ── Web Push Event Listner ──
self.addEventListener('push', function (event) {
    if (event.data) {
        let payload = {};
        try {
            payload = event.data.json();
        } catch (e) {
            payload = { title: 'Dopamind Bildirimi', message: event.data.text() };
        }

        const title = payload.title || 'Dopamind';
        const options = {
            body: payload.message || 'Yeni bir bildiriminiz var.',
            icon: '/favicon.ico',
            badge: '/favicon.ico',
            data: { url: payload.url || '/' }
        };

        event.waitUntil(self.registration.showNotification(title, options));
    }
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    if (event.notification.data && event.notification.data.url) {
        event.waitUntil(
            clients.matchAll({ type: 'window' }).then(windowClients => {
                for (let i = 0; i < windowClients.length; i++) {
                    let client = windowClients[i];
                    if (client.url === event.notification.data.url && 'focus' in client) {
                        return client.focus();
                    }
                }
                if (clients.openWindow) {
                    return clients.openWindow(event.notification.data.url);
                }
            })
        );
    }
});
