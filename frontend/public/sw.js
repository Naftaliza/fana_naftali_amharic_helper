// Minimal service worker: enables installability and caches the app shell.
// API calls are always network-first (never served stale).

const CACHE = "amharic-helper-v1";
const SHELL = ["/", "/manifest.webmanifest", "/icon-192.png", "/icon-512.png"];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (event) => {
  const { request } = event;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  // Never cache API responses — always hit the network.
  if (url.pathname.startsWith("/api")) return;

  // App shell / static: cache-first, falling back to network and caching the result.
  event.respondWith(
    caches.match(request).then(
      (cached) =>
        cached ||
        fetch(request)
          .then((resp) => {
            if (resp.ok && url.origin === self.location.origin) {
              const copy = resp.clone();
              caches.open(CACHE).then((c) => c.put(request, copy));
            }
            return resp;
          })
          .catch(() => cached)
    )
  );
});
