-- Per-section spoken audio (Summary/Explanation/KeyPoints/Actions/Deadlines) alongside the
-- existing full-document walkthrough, so a low-literacy user can play just "the actions"
-- instead of all-or-nothing audio. Section = 0 (Full) preserves every row cached before this
-- migration, so no backfill is needed.

ALTER TABLE TtsAudioCache ADD COLUMN IF NOT EXISTS Section INT NOT NULL DEFAULT 0;

ALTER TABLE TtsAudioCache DROP CONSTRAINT IF EXISTS PK_TtsAudioCache;
ALTER TABLE TtsAudioCache ADD CONSTRAINT PK_TtsAudioCache PRIMARY KEY (DocumentId, Language, Section);
