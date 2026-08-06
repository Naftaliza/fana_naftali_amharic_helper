// `flag` is an ISO country code used to load a flag image (emoji flags don't render on Windows).
// Self-hosted SVGs under public/flags/ rather than flagcdn.com: the LanguageGate is the very
// first thing a visitor sees, and a third-party DNS lookup + TLS handshake on a poor connection
// can render it broken before anything else works.
export function Flag({ code, size = 24 }: { code: string; size?: number }) {
  const h = Math.round((size / 4) * 3);
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`/flags/${code}.svg`}
      width={size}
      height={h}
      alt=""
      aria-hidden="true"
      className="rounded-sm shadow-sm"
    />
  );
}
