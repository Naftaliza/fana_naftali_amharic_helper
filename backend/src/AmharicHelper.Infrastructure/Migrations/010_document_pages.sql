-- Multi-page documents.
-- A document can now be several uploaded files (e.g. a multi-page letter photographed page by
-- page, or a bundle). Each page is OCR'd and the texts concatenated into Documents.OcrText, so
-- everything downstream (analysis, TTS, chat) is unchanged. PagePaths is the authoritative,
-- ordered list of stored file paths (including page 0, which also stays in FilePath for back-compat).
-- Legacy single-file rows keep an empty array and fall back to FilePath on delete.

ALTER TABLE Documents ADD COLUMN IF NOT EXISTS PagePaths TEXT[] NOT NULL DEFAULT '{}';
