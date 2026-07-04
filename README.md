# Amharic Helper

Help Amharic-speaking residents of Israel understand official Hebrew documents
(government, bank, insurance, healthcare, municipality, employer). Upload a
document → OCR extracts the text → AI produces a structured analysis (summary,
type, urgency, key points, required actions, deadlines) **in all three languages**
→ listen to a spoken explanation → chat with the document for follow-up questions
→ get matched to vetted professionals (accountant, lawyer, insurance agent…) who
can help with that document.

> **Status:** Deployed and running. Frontend on **Netlify**, backend + **PostgreSQL**
> on **Railway**. The full pipeline runs end-to-end on real providers: **Claude**
> for OCR (vision) and analysis, **Azure Neural TTS** for spoken explanations
> (incl. native Amharic voices). Every analysis field is generated in Hebrew,
> Amharic, and English so the UI and audio stay in one chosen language. A
> sponsored-referral marketplace (vetted providers, lead lifecycle status +
> post-contact satisfaction feedback for billing integrity, partner
> self-registration, and an admin review console) is built in, alongside a
> first-run onboarding walkthrough and a one-tap "Share Fana" growth loop.
> Provider billing turns those tracked lead counts into real invoices: an admin
> can generate a persisted, immutable PDF invoice for a provider's billable
> (Converted) leads in a given month and email it to them — no payment
> collection yet, the provider still pays out-of-band. A
> B2B/B2G tenant model (`Organizations`) lets an institution — municipality,
> health fund, NGO — license Fana under its own branding for its own
> residents/members. A `?org=slug` link resolves and persists tenant branding
> client-side (logo color, welcome message, and the registration flow tags new
> members to that tenant); a global-admin console at `/admin/organizations`
> creates, edits, and activates/deactivates tenants, and shows a per-tenant
> usage dashboard (documents processed, unique members, category/urgency
> breakdowns, weekly trend). Full white-label re-theming of every screen, a
> dedicated subdomain per tenant, per-tenant data isolation, member invites,
> and a scoped org-admin role (today's dashboard is gated by the same global
> admin allowlist as everything else) are still future work (see
> **Organizations** below).

![Sample generated invoice PDF, branded with the Fana logo and colors](docs/invoice-sample.png)

## Tech stack

| Layer     | Tech                                                                 |
|-----------|----------------------------------------------------------------------|
| Backend   | ASP.NET Core 9 Web API · Clean Architecture · CQRS (MediatR) · Repository pattern · Dapper |
| Database  | PostgreSQL (via Npgsql); Railway-managed in production, Docker locally |
| Auth      | JWT access + refresh tokens, PBKDF2 password hashing; admins by `Admin:Emails` allowlist; forgot/reset-password flow emails a single-use, 1-hour link (`/forgot-password` → `/reset-password`) via the same `IEmailSender` built for invoicing |
| Referrals | Vetted providers matched per document category · per-lead tracking with lifecycle status (New/Contacted/Responded/Converted/Invalid) for billing integrity · anonymous post-contact "was this helpful?" feedback → per-provider satisfaction rate · anonymous partner self-registration · admin approve/manage console |
| Invoicing | Admin-initiated, persisted PDF invoices (QuestPDF), branded with the Fana logo/colors, per provider + calendar month, snapshotting billable (Converted) leads so a later status change never rewrites history · emailed via SMTP (MailKit) · one invoice per provider/month (unique index) · generation always persists even if the email send fails (`Status=Failed` + `SendError`, PDF still downloadable) · a billing statement, not a payment-collection/tax document — no tax ID or bank details, since payment is handled directly, out-of-band |
| Organizations | B2B/B2G tenants (`Organizations`) — a user optionally tags itself to a tenant by slug at registration; admin-only tenant create/edit/activate-deactivate and an aggregate, anonymized usage dashboard (documents processed, unique members, category/urgency/weekly breakdowns) per tenant. Slug is locked after creation |
| AI        | `IAiProvider` → `ClaudeAiProvider` (default) / `OpenAiProvider`      |
| OCR       | `IOcrProvider` → `ClaudeOcrProvider` (default) / Google Vision / Azure |
| TTS       | `ITtsProvider` → `AzureTtsProvider` (default) / ElevenLabs · audio cached per (document, language) |
| Frontend  | Next.js 15 · TypeScript · Tailwind CSS · shadcn-style UI             |
| Languages | Hebrew (default, RTL) · Amharic · English                            |
| Hosting   | Netlify (frontend) · Railway (API + PostgreSQL) — see `DEPLOY.md`    |
| Hardening | Per-endpoint rate limiting (auth/trial/referrals) · CORS policy · PWA service worker · accessibility widget |
| Performance | Brotli/gzip response compression · output caching on the tenant-branding endpoint · indexed hot query paths (`Users.OrganizationId`, `DocumentAnalyses.CreatedAt`) · immutable-cached static assets and tree-shaken icon imports on the frontend |

> **QuestPDF licensing note:** invoice PDFs are generated with QuestPDF's free
> "Community" license, which applies only below a revenue threshold QuestPDF
> sets (check their license page before this matters commercially). Above that
> threshold, a paid QuestPDF license is required.

## Project structure

```
Fana 2.0/
├─ docker-compose.yml          # postgres + api + frontend
├─ DEPLOY.md                   # Netlify + Railway deployment guide
├─ backend/
│  ├─ AmharicHelper.sln
│  ├─ Dockerfile
│  └─ src/
│     ├─ AmharicHelper.Domain/         # entities, enums, value objects (incl. LocalizedText)
│     ├─ AmharicHelper.Application/     # CQRS, DTOs, abstractions, prompt templates, TTS text builder
│     ├─ AmharicHelper.Infrastructure/  # Dapper repos, migrations, providers, JWT
│     └─ AmharicHelper.Api/             # controllers, Program.cs, DI, Swagger
│  └─ tests/AmharicHelper.UnitTests/
└─ frontend/
   ├─ app/                      # landing, login, register, forgot-password, reset-password,
   │                           #   dashboard, upload, documents/[id], documents/[id]/chat, profile,
   │                           #   partners (self-registration), admin/providers,
   │                           #   admin/organizations (B2G tenant console)
   ├─ components/               # Navbar, UploadExperience, CameraCapture, AnalysisCard,
   │                           #   ReferralBlock, LeadFeedbackPrompt, ShareButton, Onboarding,
   │                           #   AccessibilityWidget, ServiceWorker, ui/
   ├─ lib/                      # api client, auth + language + organization contexts, types
   ├─ i18n/                     # he / am / en dictionaries
   └─ jest.config.js, jest.setup.ts  # Jest + React Testing Library; *.test.ts(x) live in
                                      #   __tests__/ folders next to the code they cover
```

The backend Application layer is organized by feature (`Auth`, `Documents`,
`Chat`, `Trial`, `Partners`, `Referrals`, `Organizations`), each with its CQRS
commands/queries.

### Database schema
`Users` (with an optional `OrganizationId`), `RefreshTokens`, `Documents`,
`DocumentAnalyses`, `ChatMessages`, `TtsAudioCache`, `Providers`, `Leads` (with
`Status` and `Helpful` columns for lead lifecycle + post-contact feedback),
`Organizations` (B2B/B2G tenant branding), `Invoices` (persisted, immutable
per-provider/month billing snapshots, unique on `(ProviderId, PeriodYear,
PeriodMonth)`) (see
`backend/src/AmharicHelper.Infrastructure/Migrations/`, numbered `001`–`014`).
Migrations are idempotent PostgreSQL and run on API startup. Analysis text
columns store JSON localized to `{ he, am, en }`.

### API endpoints
- `POST /api/auth/register | login | refresh | forgot-password | reset-password`
- `GET  /api/users/me`
- `POST /api/documents` (upload + OCR), `GET /api/documents`, `GET /api/documents/{id}`
- `POST /api/documents/{id}/analyze?category=`
- `GET  /api/documents/{id}/speech?language=` (spoken explanation, MP3)
- `GET|POST /api/documents/{id}/chat`
- `POST /api/trial/analyze`, `POST /api/trial/speech` (anonymous trial, nothing saved)
- `GET  /api/referrals?category=` (matched providers), `POST /api/referrals/{providerId}/lead` (log a contact) — anonymous, rate-limited
- `POST /api/referrals/feedback/{refCode}` (post-contact "did this help?" signal) — anonymous, rate-limited
- `POST /api/partners/apply` (business self-registration, anonymous, rate-limited)
- `GET|PUT|POST|DELETE /api/admin/providers...` (review/manage providers — admin only)
- `GET  /api/admin/leads` (lead overview — admin only), `PUT /api/admin/leads/{id}/status` (update lead lifecycle status — admin only)
- `GET /api/admin/providers/{id}/invoices/status?year=&month=` (check if an invoice exists for a provider+month), `POST /api/admin/providers/{id}/invoices/generate` (generate + email a PDF invoice for the billable leads in that month), `GET /api/admin/providers/{id}/invoices/{invoiceId}/pdf` (re-download the PDF) — admin only
- `GET  /api/organizations/{slug}` (tenant branding lookup — anonymous, drives a white-labeled front end, output-cached 2 min)
- `POST /api/admin/organizations` (create a tenant), `GET /api/admin/organizations` (list), `PUT /api/admin/organizations/{id}` (edit branding — slug immutable), `POST /api/admin/organizations/{id}/active?value=` (activate/deactivate), `GET /api/admin/organizations/{id}/stats` (usage dashboard) — admin only
- `GET /health`

## Local development

### Option A — everything in Docker (recommended)
```bash
cd "Fana 2.0"
export ANTHROPIC_API_KEY=sk-ant-...     # required for real OCR + analysis
export SENDGRID_API_KEY=SG....          # required for real invoice/password-reset emails
export SENDGRID_FROM_EMAIL=you@yourdomain.com  # must be verified in SendGrid (Settings -> Sender Authentication)
docker compose up --build
```
- API: http://localhost:5080 (Swagger at `/swagger`)
- Frontend: http://localhost:3001
- PostgreSQL: `localhost:5432` (`fana` / `fana_local_pass`, db `AmharicHelper`)

Migrations and seed scripts run automatically on API startup (idempotent). The
API waits for the database to accept connections before migrating, so it won't
crash if Postgres is still booting.

### Option B — run pieces locally
```bash
# 1. Database only
docker compose up postgres

# 2. Backend
cd backend
dotnet run --project src/AmharicHelper.Api      # http://localhost:5080

# 3. Frontend
cd frontend
cp .env.example .env.local
npm install
npm run dev                                     # http://localhost:3000
```

Create an account on the **Register** page to start (the seeded demo row is a
placeholder; register a real user for a working login).

### Tests
```bash
cd backend && dotnet test          # xUnit — hand-written fakes, no mocking library
cd frontend && npm test            # Jest + React Testing Library
```

## Configuration

Backend config lives in `appsettings.json` and can be overridden by environment
variables (double-underscore syntax, e.g. `Ai__Provider`):

| Key                       | Purpose                                            | Default       |
|---------------------------|----------------------------------------------------|---------------|
| `ConnectionStrings__Default` | PostgreSQL connection (Npgsql kv string, or a `postgres://` URL such as Railway's `DATABASE_URL`) | local Postgres |
| `Jwt__Secret`             | JWT signing key (≥32 chars)                        | placeholder   |
| `Ai__Provider`            | `Claude` or `OpenAI`                               | `Claude`      |
| `Ai__AnthropicApiKey` / `ANTHROPIC_API_KEY` | Claude API key (used for AI + OCR) | empty         |
| `Ocr__Provider`           | `Claude`, `Mock`, `Google`, or `Azure`             | `Claude`      |
| `Tts__Provider`           | `Azure` or `ElevenLabs`                            | `Azure`       |
| `Tts__AzureSpeechKey` / `Tts__AzureRegion` | Azure Speech credentials          | empty         |
| `Admin__Emails`           | CSV of emails granted admin access (provider/lead consoles) | empty |
| `Email__Smtp__Host` / `Port` / `Username` / `Password` / `From` | SMTP credentials (MailKit) used to email generated invoice PDFs and password-reset links; via `SENDGRID_API_KEY` / `SENDGRID_FROM_EMAIL` in Docker, host is `smtp.sendgrid.net`, username is the literal `apikey` | empty |
| `Company__SupportEmail`   | "Questions about this invoice?" contact shown on invoice PDFs; falls back to the first `Admin:Emails` entry if unset | empty |

## Deployment

Production runs on **Netlify** (frontend) + **Railway** (API + managed PostgreSQL).
Full step-by-step instructions, environment variables, and service settings are in
[`DEPLOY.md`](DEPLOY.md). Key points:

- The API reads `ConnectionStrings__Default`; on Railway set it to the Postgres
  service's `DATABASE_URL` (`${{Postgres.DATABASE_URL}}`). The app converts the URL
  to an Npgsql connection string automatically.
- Idempotent migrations create the schema on startup — no manual DB setup.
- Secrets (`Jwt__Secret`, `ANTHROPIC_API_KEY`, Azure Speech key) live in the host's
  variable store, never in the repo. `LocalFileStorage` keeps uploads on disk; swap
  it behind `IFileStorage` for blob storage if you need uploads to outlive the container.

## Future features (not implemented)
Native mobile app · government-forms assistant · appointment booking. (The
professional-referral marketplace, originally a future item, is now built — see
**Referrals** above. Contact links include WhatsApp deep-links.)
