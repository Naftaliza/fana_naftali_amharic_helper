import "@testing-library/jest-dom";

// jsdom doesn't implement the Blob object-URL APIs — several components (audio playback,
// invoice PDF viewing, .ics calendar download links) call these to turn a Blob into a URL.
if (typeof URL.createObjectURL !== "function") {
  URL.createObjectURL = jest.fn(() => "blob:mock-url");
}
if (typeof URL.revokeObjectURL !== "function") {
  URL.revokeObjectURL = jest.fn();
}

// jsdom implements neither MediaRecorder nor HTMLMediaElement.play() — VoiceInputButton and the
// audio players (AnalysisCard, SectionAudioButton, PhraseCard, LanguageGate) all reach for them.
// Minimal stubs so component tests can render without a real device; behavior is exercised via
// mocking these globals per-test, not by this default implementation.
if (typeof (globalThis as { MediaRecorder?: unknown }).MediaRecorder !== "function") {
  class MockMediaRecorder {
    static isTypeSupported = () => false;
    ondataavailable: ((e: { data: Blob }) => void) | null = null;
    onstop: (() => void) | null = null;
    onerror: (() => void) | null = null;
    mimeType = "";
    start() {}
    stop() {
      this.onstop?.();
    }
  }
  (globalThis as { MediaRecorder?: unknown }).MediaRecorder = MockMediaRecorder;
}
// jsdom's own play()/pause() exist but throw "Not implemented" when called — replace outright.
Object.defineProperty(HTMLMediaElement.prototype, "play", {
  configurable: true,
  value: jest.fn().mockResolvedValue(undefined),
});
Object.defineProperty(HTMLMediaElement.prototype, "pause", {
  configurable: true,
  value: jest.fn(),
});
