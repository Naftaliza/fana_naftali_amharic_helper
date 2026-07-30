import "@testing-library/jest-dom";

// jsdom doesn't implement the Blob object-URL APIs — several components (audio playback,
// invoice PDF viewing, .ics calendar download links) call these to turn a Blob into a URL.
if (typeof URL.createObjectURL !== "function") {
  URL.createObjectURL = jest.fn(() => "blob:mock-url");
}
if (typeof URL.revokeObjectURL !== "function") {
  URL.revokeObjectURL = jest.fn();
}
