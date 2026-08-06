/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  compress: true,
  // Tree-shakes lucide-react (and other multi-export packages) to only the icons actually
  // imported per file instead of bundling the whole icon set into the client chunk.
  experimental: {
    optimizePackageImports: ["lucide-react"],
  },
  async headers() {
    return [
      {
        // Next's hashed static build assets are immutable — safe to cache for a year.
        source: "/_next/static/:path*",
        headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
      },
      {
        // mp3 added for the generated UI/sample audio clips (public/audio/...) — without this
        // they'd revalidate on every load instead of being cached like every other static asset.
        source: "/:path*(svg|jpg|jpeg|png|webp|avif|ico|woff2|mp3)",
        headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
      },
    ];
  },
};

module.exports = nextConfig;
