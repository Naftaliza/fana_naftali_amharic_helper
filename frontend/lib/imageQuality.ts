// Cheap client-side check on a just-captured photo, so a blurry/dark shot is caught in
// ~10ms instead of costing the user the ~60s OCR+analysis round trip only to come back
// with a vague result. Warns, never blocks — a false positive on a legitimately
// low-contrast document must not trap the user with no way forward.

export type QualityVerdict = "ok" | "blurry" | "dark" | "washedOut";

// Thresholds are a starting point, not calibrated against a corpus of real device photos —
// revisit once real capture data (or user complaints) exist.
const BLUR_VARIANCE_THRESHOLD = 100;
const DARK_LUMINANCE_THRESHOLD = 40;
const WASHED_OUT_LUMINANCE_THRESHOLD = 220;

// Downscale before assessing — the Laplacian pass is O(pixels), and a phone photo's actual
// resolution doesn't add signal here (blur/exposure are visible at low res too).
export const ASSESS_MAX_WIDTH = 640;

export interface QualityAssessment {
  blurVariance: number;
  luminance: number;
  verdict: QualityVerdict;
}

/**
 * Assess a downscaled RGBA frame for blur (variance of a Laplacian edge-detection pass over
 * grayscale) and exposure (mean luminance). Pure function over pixel data so it's testable
 * without a real <canvas>.
 */
export function assessQuality(data: ImageData): QualityAssessment {
  const { width, height, data: px } = data;
  const gray = new Float32Array(width * height);
  let luminanceSum = 0;

  for (let i = 0; i < width * height; i++) {
    const r = px[i * 4];
    const g = px[i * 4 + 1];
    const b = px[i * 4 + 2];
    const y = 0.299 * r + 0.587 * g + 0.114 * b;
    gray[i] = y;
    luminanceSum += y;
  }
  const luminance = luminanceSum / (width * height);

  // 3x3 Laplacian kernel [[0,1,0],[1,-4,1],[0,1,0]] — high variance in the response means
  // lots of sharp edges (in focus); low variance means the image is smooth/blurry.
  const laplacian: number[] = [];
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const idx = y * width + x;
      const value =
        gray[idx - width] + gray[idx - 1] + gray[idx + 1] + gray[idx + width] - 4 * gray[idx];
      laplacian.push(value);
    }
  }
  const mean = laplacian.reduce((s, v) => s + v, 0) / (laplacian.length || 1);
  const blurVariance =
    laplacian.reduce((s, v) => s + (v - mean) ** 2, 0) / (laplacian.length || 1);

  let verdict: QualityVerdict = "ok";
  if (blurVariance < BLUR_VARIANCE_THRESHOLD) verdict = "blurry";
  else if (luminance < DARK_LUMINANCE_THRESHOLD) verdict = "dark";
  else if (luminance > WASHED_OUT_LUMINANCE_THRESHOLD) verdict = "washedOut";

  return { blurVariance, luminance, verdict };
}

/** Downscale a canvas to ASSESS_MAX_WIDTH and assess it. Not unit-tested directly (needs a
 * real canvas) — assessQuality() carries the actual logic and is tested in isolation. */
export function assessCanvas(source: HTMLCanvasElement): QualityAssessment | null {
  const scale = Math.min(1, ASSESS_MAX_WIDTH / source.width);
  const width = Math.max(1, Math.round(source.width * scale));
  const height = Math.max(1, Math.round(source.height * scale));

  const small = document.createElement("canvas");
  small.width = width;
  small.height = height;
  const ctx = small.getContext("2d");
  if (!ctx) return null;
  ctx.drawImage(source, 0, 0, width, height);

  const data = ctx.getImageData(0, 0, width, height);
  return assessQuality(data);
}
