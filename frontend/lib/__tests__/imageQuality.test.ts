import { assessQuality } from "@/lib/imageQuality";

// Build a synthetic ImageData-like object (jsdom doesn't implement the real ImageData
// constructor pixel semantics reliably enough to trust it here) with a given pixel-value
// generator, so assessQuality is exercised without a real <canvas>.
function makeImage(width: number, height: number, pixel: (x: number, y: number) => number): ImageData {
  const data = new Uint8ClampedArray(width * height * 4);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const v = pixel(x, y);
      const i = (y * width + x) * 4;
      data[i] = v;
      data[i + 1] = v;
      data[i + 2] = v;
      data[i + 3] = 255;
    }
  }
  return { width, height, data, colorSpace: "srgb" } as ImageData;
}

describe("assessQuality", () => {
  it("scores a sharp high-contrast checkerboard as ok", () => {
    const img = makeImage(32, 32, (x, y) => ((x + y) % 2 === 0 ? 0 : 255));
    const result = assessQuality(img);
    expect(result.verdict).toBe("ok");
  });

  it("scores a flat, uniform image as blurry", () => {
    const img = makeImage(32, 32, () => 128);
    const result = assessQuality(img);
    expect(result.verdict).toBe("blurry");
    expect(result.blurVariance).toBe(0);
  });

  it("scores a near-black image as dark", () => {
    // Slight noise so it isn't flagged as perfectly uniform for an unrelated reason —
    // the point under test is the luminance threshold, not the blur one.
    const img = makeImage(32, 32, (x, y) => ((x + y) % 2 === 0 ? 5 : 10));
    const result = assessQuality(img);
    expect(result.verdict).toBe("dark");
    expect(result.luminance).toBeLessThan(40);
  });

  it("scores a near-white image as washed out", () => {
    const img = makeImage(32, 32, (x, y) => ((x + y) % 2 === 0 ? 245 : 250));
    const result = assessQuality(img);
    expect(result.verdict).toBe("washedOut");
    expect(result.luminance).toBeGreaterThan(220);
  });
});
