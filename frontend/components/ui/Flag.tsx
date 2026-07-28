// `flag` is an ISO country code used to load a flag image (emoji flags don't render on Windows).
export function Flag({ code, size = 24 }: { code: string; size?: number }) {
  const h = Math.round((size / 4) * 3);
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`https://flagcdn.com/${size}x${h}/${code}.png`}
      srcSet={`https://flagcdn.com/${size * 2}x${h * 2}/${code}.png 2x`}
      width={size}
      height={h}
      alt=""
      aria-hidden="true"
      className="rounded-sm shadow-sm"
    />
  );
}
