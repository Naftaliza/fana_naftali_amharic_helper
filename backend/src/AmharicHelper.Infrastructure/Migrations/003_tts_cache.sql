-- Cache synthesized text-to-speech audio per (document, language) so replaying an
-- explanation, or re-opening a document, does not re-bill the speech provider.
-- ON DELETE CASCADE removes cached audio automatically when the document is deleted.

CREATE TABLE IF NOT EXISTS TtsAudioCache (
    DocumentId  UUID        NOT NULL,
    Language    INT         NOT NULL,
    ContentType TEXT        NOT NULL,
    Audio       BYTEA       NOT NULL,
    CreatedAt   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT PK_TtsAudioCache PRIMARY KEY (DocumentId, Language),
    CONSTRAINT FK_TtsAudioCache_Documents FOREIGN KEY (DocumentId)
        REFERENCES Documents(Id) ON DELETE CASCADE
);
