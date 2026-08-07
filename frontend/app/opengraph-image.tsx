import { ImageResponse } from "next/og";

// Next.js auto-wires this file into every page's <meta property="og:image">/twitter:image
// unless a route defines its own (see app/sample/page.tsx). Kept to Latin text only so the
// default ImageResponse font suffices — Ethiopic/Hebrew would require fetching TTFs by URL
// at render time, which is unnecessary risk for a share-card image.
export const alt = "Fana — understand Hebrew documents in Amharic";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          background: "linear-gradient(135deg, #2563eb 0%, #0ea5a4 100%)",
          color: "white",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 24 }}>
          <div
            style={{
              display: "flex",
              width: 120,
              height: 120,
              borderRadius: 28,
              background: "rgba(255,255,255,0.16)",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 72,
            }}
          >
            📄
          </div>
          <div style={{ fontSize: 140, fontWeight: 700, letterSpacing: -4 }}>Fana</div>
        </div>
        <div style={{ display: "flex", fontSize: 40, marginTop: 32, opacity: 0.94 }}>
          Understand Hebrew documents — in Amharic
        </div>
      </div>
    ),
    { ...size }
  );
}
