-- Sponsored referrals.
-- Providers: vetted professionals shown after an analysis, matched to the document category.
-- Leads:     one row per user→provider contact — the billable unit (count per provider/month).
-- Also persists the AI-classified category on each analysis so referrals can be matched even
-- for documents the user never categorized (e.g. anonymous trials).

CREATE TABLE IF NOT EXISTS Providers (
    Id          UUID        NOT NULL,
    Category    INT         NOT NULL,             -- maps to DocumentCategory enum
    DisplayName TEXT        NOT NULL,
    Phone       TEXT        NULL,
    WhatsApp    TEXT        NULL,                 -- international format, no '+' (e.g. 9725...)
    City        TEXT        NULL,
    Blurb       TEXT        NOT NULL DEFAULT '{}',-- LocalizedText JSON {"He","Am","En"}
    IsActive    BOOLEAN     NOT NULL DEFAULT TRUE,
    Priority    INT         NOT NULL DEFAULT 0,   -- higher shows first
    CreatedAt   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT PK_Providers PRIMARY KEY (Id)
);

CREATE INDEX IF NOT EXISTS IX_Providers_Category_Active ON Providers (Category, IsActive);

CREATE TABLE IF NOT EXISTS Leads (
    Id          UUID        NOT NULL,
    ProviderId  UUID        NOT NULL,
    Category    INT         NOT NULL,
    Urgency     INT         NOT NULL,
    DocumentId  UUID        NULL,                 -- null for anonymous trial leads
    CreatedAt   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT PK_Leads PRIMARY KEY (Id),
    CONSTRAINT FK_Leads_Providers FOREIGN KEY (ProviderId)
        REFERENCES Providers(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Leads_Provider_Created ON Leads (ProviderId, CreatedAt);

-- Persist the AI-classified category alongside each analysis. Legacy rows default to 6 (Other).
ALTER TABLE DocumentAnalyses ADD COLUMN IF NOT EXISTS Category INT NOT NULL DEFAULT 6;

-- ---------------------------------------------------------------------------------------------
-- SAMPLE providers so the referral flow is demonstrable end-to-end. The phone/WhatsApp numbers
-- are deliberately invalid placeholders (9725000000NN). REPLACE these with real, vetted,
-- Amharic-speaking professionals before going live. Category: 0=Government 1=Bank 2=Insurance
-- 3=Employment 4=Healthcare 5=Municipality 6=Other.
-- ---------------------------------------------------------------------------------------------
INSERT INTO Providers (Id, Category, DisplayName, Phone, WhatsApp, City, Blurb, IsActive, Priority) VALUES
('00000000-0000-0000-0000-0000000000a1', 0, 'Bituach Leumi Helpdesk (sample)', '972500000001', '972500000001', 'Tel Aviv',
 '{"He":"עזרה במילוי טפסים וערעורים מול הביטוח הלאומי","Am":"በብሔራዊ ኢንሹራንስ ቅጾችና ይግባኝ እርዳታ","En":"Help with National Insurance forms and appeals"}', TRUE, 10),
('00000000-0000-0000-0000-0000000000a2', 0, 'Gov Forms Advisor (sample)', '972500000002', '972500000002', 'Be''er Sheva',
 '{"He":"ייעוץ והגשת מסמכים מול משרדי הממשלה","Am":"ከመንግሥት መሥሪያ ቤቶች ጋር የሰነድ ማማከርና ማቅረብ","En":"Advice and filing with government offices"}', TRUE, 5),
('00000000-0000-0000-0000-0000000000b1', 1, 'Amharic-speaking Bank Advisor (sample)', '972500000011', '972500000011', 'Netanya',
 '{"He":"ליווי מול הבנק: עמלות, חשבון והסדרי תשלום","Am":"ከባንክ ጋር ድጋፍ፡ ክፍያዎች፣ ሂሳብና የክፍያ ዝግጅት","En":"Bank support: fees, accounts and payment plans"}', TRUE, 10),
('00000000-0000-0000-0000-0000000000c1', 2, 'Insurance Agent – Amharic (sample)', '972500000021', '972500000021', 'Rishon LeZion',
 '{"He":"סוכן ביטוח דובר אמהרית: כיסוי, תביעות וחידושים","Am":"አማርኛ ተናጋሪ የኢንሹራንስ ወኪል፡ ሽፋን፣ የይገባኛል ጥያቄና እድሳት","En":"Amharic-speaking insurance agent: coverage, claims and renewals"}', TRUE, 10),
('00000000-0000-0000-0000-0000000000d1', 3, 'Workers'' Rights Advisor (sample)', '972500000031', '972500000031', 'Tel Aviv',
 '{"He":"ייעוץ בזכויות עובדים, שכר וחוזי העסקה","Am":"በሠራተኞች መብት፣ ደመወዝና የቅጥር ውል ማማከር","En":"Advice on workers'' rights, pay and employment contracts"}', TRUE, 10),
('00000000-0000-0000-0000-0000000000e1', 4, 'Kupat Cholim Navigator (sample)', '972500000041', '972500000041', 'Haifa',
 '{"He":"עזרה בקופת חולים: זימונים, הפניות וטפסים","Am":"በኩፓት ሖሊም እርዳታ፡ ቀጠሮዎች፣ ሪፈራሎችና ቅጾች","En":"Health-fund help: appointments, referrals and forms"}', TRUE, 10),
('00000000-0000-0000-0000-0000000000f1', 5, 'Arnona & Municipality Help (sample)', '972500000051', '972500000051', 'Lod',
 '{"He":"עזרה בארנונה, אגרות ושירותי עירייה","Am":"በአርኖና፣ ክፍያዎችና የማዘጋጃ ቤት አገልግሎቶች እርዳታ","En":"Help with arnona, fees and municipality services"}', TRUE, 10)
ON CONFLICT (Id) DO NOTHING;
