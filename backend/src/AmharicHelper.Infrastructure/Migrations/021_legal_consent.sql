-- Legal surface + data retention. The app previously had no Terms, no Privacy Policy, and no
-- retention policy, while auth.registerDisclaimer already told users they "agree to our terms" —
-- a document that didn't exist and linked nowhere. A reachable, described privacy policy is also
-- a hard prerequisite for Meta WhatsApp Business Verification and any Israeli payment gateway, so
-- this unblocks both of those later. Idempotent: safe to run on every startup.

CREATE TABLE IF NOT EXISTS LegalDocuments (
    Id          UUID        NOT NULL PRIMARY KEY,
    Kind        TEXT        NOT NULL,               -- 'terms' | 'privacy'
    Version     INT         NOT NULL,
    BodyHe      TEXT        NOT NULL DEFAULT '',
    BodyAm      TEXT        NOT NULL DEFAULT '',
    BodyEn      TEXT        NOT NULL DEFAULT '',
    EffectiveAt TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_LegalDocuments_Kind_Version ON LegalDocuments(Kind, Version);

-- Who accepted what, when. Evidence for GDPR Art. 7(1) / Israeli Privacy Protection Law
-- amendment 13. ContactHash lets a not-yet-registered contact (e.g. a future WhatsApp user)
-- record consent before any User row exists for them.
CREATE TABLE IF NOT EXISTS ConsentRecords (
    Id           UUID        NOT NULL PRIMARY KEY,
    UserId       UUID NULL REFERENCES Users(Id) ON DELETE CASCADE,
    ContactHash  TEXT NULL,
    Kind         TEXT        NOT NULL,
    Version      INT         NOT NULL,
    AcceptedAt   TIMESTAMPTZ NOT NULL DEFAULT now(),
    SourceIp     TEXT NULL,
    CONSTRAINT CK_ConsentRecords_Subject CHECK (UserId IS NOT NULL OR ContactHash IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS IX_ConsentRecords_User ON ConsentRecords(UserId);
CREATE INDEX IF NOT EXISTS IX_ConsentRecords_ContactHash ON ConsentRecords(ContactHash);

-- Retention: uploaded page files and OCR text are the sensitive payload (bank, medical,
-- Bituach Leumi letters). Default 24 months from upload; a future per-document "keep this one"
-- override can null out RetainUntil without a schema change.
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS RetainUntil TIMESTAMPTZ NULL;
UPDATE Documents SET RetainUntil = UploadedAt + INTERVAL '24 months' WHERE RetainUntil IS NULL;
CREATE INDEX IF NOT EXISTS IX_Documents_RetainUntil ON Documents(RetainUntil);

-- Seed version 1 of both documents. DRAFT CONTENT — written to unblock the plumbing (the app
-- referenced a nonexistent Terms page before this migration existed), not reviewed by counsel.
-- Get both reviewed before relying on them for a real launch, WhatsApp verification, or a PSP
-- application; then insert version 2 rather than editing these rows (see LegalDocuments.Version).
INSERT INTO LegalDocuments (Id, Kind, Version, BodyHe, BodyAm, BodyEn, EffectiveAt)
VALUES (
    '9b6a1e2a-0001-4a11-8b1a-000000000001', 'terms', 1,
    E'תנאי שימוש באפליקציית Fana (טיוטה)\n\n'
    'Fana עוזרת למשתמשים דוברי אמהרית להבין מסמכים רשמיים בעברית, באמצעות זיהוי טקסט (OCR) וניתוח בבינה מלאכותית. '
    'המידע המוצג באפליקציה הוא הסבר כללי בלבד ואינו ייעוץ משפטי, רפואי או פיננסי מחייב. יש לאמת כל מידע קריטי מול הגורם הרשמי שהנפיק את המסמך. '
    'אין להעלות מסמכים שאינם שלכם או שאין לכם זכות חוקית לעבד. השירות מיועד לגילאי 18 ומעלה או בליווי מבוגר אחראי. '
    'אנו רשאים לעדכן תנאים אלו מעת לעת; שינויים מהותיים יפורסמו בעמוד זה עם מספר גרסה חדש. לשאלות ניתן לפנות לכתובת התמיכה המופיעה בעמוד העזרה.',
    E'የFana መተግበሪያ የአገልግሎት ውሎች (ረቂቅ)\n\n'
    'Fana አማርኛ ተናጋሪ ተጠቃሚዎች በዕብራይስጥ የተጻፉ ኦፊሴላዊ ሰነዶችን እንዲረዱ ይረዳል፣ በጽሑፍ ማወቂያ (OCR) እና በአርቴፊሻል ኢንተለጀንስ ትንተና አማካኝነት። '
    'በመተግበሪያው የሚታየው መረጃ አጠቃላይ ማብራሪያ ብቻ ነው፤ አስገዳጅ የሕግ፣ የሕክምና ወይም የፋይናንስ ምክር አይደለም። ማንኛውንም ወሳኝ መረጃ ሰነዱን ካወጣው ኦፊሴላዊ አካል ጋር ማረጋገጥ ያስፈልጋል። '
    'የእርስዎ ያልሆኑ ወይም ሕጋዊ መብት የሌላችሁባቸው ሰነዶችን መስቀል የተከለከለ ነው። አገልግሎቱ ለ18 ዓመት እና ከዚያ በላይ ወይም በኃላፊነት ባለው አዋቂ ታጅበው ለሚጠቀሙ የታሰበ ነው። '
    'እነዚህን ውሎች ከጊዜ ወደ ጊዜ ልናዘምናቸው እንችላለን፤ ጉልህ ለውጦች በአዲስ ስሪት ቁጥር በዚህ ገጽ ላይ ይታተማሉ። ጥያቄዎች ካሉዎት በእገዛ ገጹ ላይ ወዳለው የድጋፍ አድራሻ ይጠቀሙ።',
    E'Fana Terms of Service (Draft)\n\n'
    'Fana helps Amharic-speaking users understand official Hebrew documents through OCR text extraction and AI analysis. '
    'Information shown in the app is a general explanation only, not binding legal, medical, or financial advice. '
    'Always confirm anything critical with the official body that issued the document. '
    'Do not upload documents that are not yours or that you have no legal right to process. '
    'The service is intended for users 18 and older, or used with a responsible adult present. '
    'We may update these terms from time to time; material changes will be published on this page under a new version number. '
    'Questions can be sent to the support address listed on the Help page.',
    now()
)
ON CONFLICT (Kind, Version) DO NOTHING;

INSERT INTO LegalDocuments (Id, Kind, Version, BodyHe, BodyAm, BodyEn, EffectiveAt)
VALUES (
    '9b6a1e2a-0001-4a11-8b1a-000000000002', 'privacy', 1,
    E'מדיניות פרטיות של Fana (טיוטה)\n\n'
    'אנו אוספים: כתובת אימייל ופרטי חשבון, תמונות/קבצים של מסמכים שהעליתם, הטקסט שחולץ מהם וניתוח הבינה המלאכותית שלהם, והיסטוריית הצ׳אט על כל מסמך. '
    'המידע משמש להפעלת השירות (זיהוי טקסט, ניתוח, הקראה קולית, מענה בצ׳אט) ולשיפורו. אנו לא מוכרים מידע אישי לצדדים שלישיים. '
    'מסמכים שהועלו נשמרים עד 24 חודשים ולאחר מכן נמחקים אוטומטית, אלא אם תבחרו למחוק אותם קודם דרך האפליקציה. '
    'ניתן לבקש בכל עת עותק של כל המידע השמור עליכם או למחוק את החשבון וכל תוכנו לצמיתות, דרך עמוד הפרופיל. '
    'נתוני מסמכים (כולל מסמכים בנקאיים, ממשלתיים ורפואיים) מטופלים כמידע רגיש ואינם משמשים למטרות פרסום. '
    'מסמך זה הינו טיוטה ראשונית ויעודכן לאחר בדיקה משפטית.',
    E'የFana ግላዊነት ፖሊሲ (ረቂቅ)\n\n'
    'የምንሰበስበው መረጃ፦ የኢሜይል አድራሻ እና የመለያ ዝርዝሮች፣ የሰቀሏቸው የሰነድ ፎቶዎች/ፋይሎች፣ ከነሱ የተወጣው ጽሑፍ እና የአርቴፊሻል ኢንተለጀንስ ትንተናቸው፣ እና በእያንዳንዱ ሰነድ ላይ ያለው የውይይት ታሪክ። '
    'መረጃው አገልግሎቱን ለማስኬድ (ጽሑፍ ማወቂያ፣ ትንተና፣ በድምጽ ማንበብ፣ በውይይት መልስ መስጠት) እና ለማሻሻል ይውላል። የግል መረጃን ለሶስተኛ ወገኖች አንሸጥም። '
    'የተሰቀሉ ሰነዶች እስከ 24 ወራት ይቀመጣሉ፣ ከዚያ በኋላ በራስ-ሰር ይሰረዛሉ፤ ካልፈለጉ በስተቀር ቀድመው በመተግበሪያው በኩል ሊሰርዟቸው ይችላሉ። '
    'በማንኛውም ጊዜ ስለ እርስዎ የተቀመጠውን ሁሉንም መረጃ ቅጂ መጠየቅ ወይም መለያዎን እና ይዘቱን ሙሉ በሙሉ በመገለጫ ገጹ በኩል መሰረዝ ይችላሉ። '
    'የሰነድ መረጃ (የባንክ፣ የመንግስት እና የጤና ሰነዶችን ጨምሮ) እንደ ስሱ መረጃ ይታያል እና ለማስታወቂያ ዓላማዎች አይውልም። '
    'ይህ ሰነድ የመጀመሪያ ረቂቅ ሲሆን ከሕግ ግምገማ በኋላ ይዘምናል።',
    E'Fana Privacy Policy (Draft)\n\n'
    'We collect: your email and account details, the document photos/files you upload, the text extracted from them and their AI analysis, and the chat history on each document. '
    'This information is used to run the service (OCR, analysis, spoken audio, chat answers) and to improve it. We do not sell personal information to third parties. '
    'Uploaded documents are retained for up to 24 months and then automatically deleted, unless you delete them sooner yourself in the app. '
    'You can request a copy of everything stored about you, or permanently delete your account and its contents, at any time from the Profile page. '
    'Document data (including bank, government, and healthcare documents) is treated as sensitive and is never used for advertising. '
    'This document is an initial draft and will be updated after legal review.',
    now()
)
ON CONFLICT (Kind, Version) DO NOTHING;
