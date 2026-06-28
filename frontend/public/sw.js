// PWA service worker.
// - Navigations (HTML) are NETWORK-FIRST so a new deploy is shown immediately; the cached
//   shell is only a fallback when offline. (Cache-first on HTML made deploys invisible.)
// - Static assets are cache-first (Next.js chunks are content-hashed, so this is safe).
// - API calls always hit the network.
// Bump CACHE on changes here so old caches are purged on activate.

const CACHE = "amharic-helper-v2";
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

  // Navigations / HTML documents: network-first, fall back to cache (offline).
  const isNavigation =
    request.mode === "navigate" ||
    (request.headers.get("accept") || "").includes("text/html");

  if (isNavigation) {
    event.respondWith(
      fetch(request)
        .then((resp) => {
          if (resp.ok && url.origin === self.location.origin) {
            const copy = resp.clone();
            caches.open(CACHE).then((c) => c.put(request, copy));
          }
          return resp;
        })
        .catch(() => caches.match(request).then((cached) => cached || caches.match("/")))
    );
    return;
  }

  // Static assets (content-hashed): cache-first, falling back to network and caching it.
  event.respondWith(
    caches.match(request).then(
      (cached) =>
        cached ||
        fetch(request).then((resp) => {
          if (resp.ok && url.origin === self.location.origin) {
            const copy = resp.clone();
            caches.open(CACHE).then((c) => c.put(request, copy));
          }
          return resp;
        }).catch(() => cached)
    )
  );
});
