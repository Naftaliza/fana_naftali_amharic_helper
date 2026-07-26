-- Background document processing (see plan). OCR used to run synchronously inside the upload
-- request; it now runs in DocumentProcessor, driven off a queue by DocumentProcessingWorker, so a
-- 10-page upload no longer holds the HTTP request open for minutes. Existing rows default to
-- Ready with zero skipped/processed-page counters, since their OCR already completed synchronously
-- before this column existed.
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS PageContentTypes TEXT[] NOT NULL DEFAULT '{}';
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS Status INT NOT NULL DEFAULT 2; -- Ready
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS ProcessedPages INT NOT NULL DEFAULT 0;
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS TotalPages INT NOT NULL DEFAULT 0;
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS SkippedPages INT NOT NULL DEFAULT 0;
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS ProcessingError TEXT NULL;
