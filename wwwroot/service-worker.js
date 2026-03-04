const CACHE_NAME = 'dopamind-v1';
const urlsToCache = [
    '/',
    '/css/site.css',
    '/js/dashboard.js',
    '/favicon.ico',
    '/manifest.json'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                // Just cache basics, don't block install on these
                cache.addAll(urlsToCache).catch(console.error);
            })
    );
});

self.addEventListener('fetch', event => {
    // Only cache GET requests
    if (event.request.method !== 'GET') return;

    event.respondWith(
        caches.match(event.request)
            .then(response => {
                // Cache hit - return response
                if (response) {
                    return response;
                }
                return fetch(event.request).then(
                    function (response) {
                        // Check if we received a valid response
                        if (!response || response.status !== 200 || response.type !== 'basic') {
                            return response;
                        }

                        // Don't cache API calls aggressively
                        if (event.request.url.includes('/api/') || event.request.url.includes('/Dashboard/')) {
                            return response;
                        }

                        // Clone to put in cache
                        var responseToCache = response.clone();
                        caches.open(CACHE_NAME)
                            .then(function (cache) {
                                cache.put(event.request, responseToCache);
                            });

                        return response;
                    }
                );
            })
    );
});
