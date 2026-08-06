import type { Language, SpokenSectionKey } from "@/lib/types";

// Matches the output paths written by scripts/generate-ui-audio.mjs. Only "Full" is
// pre-generated for the demo slice (per-section clips are the full feature #2 build, deferred);
// AnalysisCard/SectionAudioButton already degrade gracefully (hide/error) on a null or missing
// file, so returning null for every other section is the correct, not a stopgap, behavior.
export function sampleAudioSrcFor(section: SpokenSectionKey, language: Language): string | null {
  if (section !== "Full") return null;
  return `/audio/sample/${language}/Full.mp3`;
}

// Only the fixture's first required action (index 0) has a pre-generated phrase clip.
export function samplePhraseAudioSrcFor(actionIndex: number): string | null {
  return actionIndex === 0 ? "/audio/sample/phrase-0.mp3" : null;
}
