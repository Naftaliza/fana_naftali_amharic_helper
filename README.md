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
> **Organizations** below). A first-run, full-screen language picker
> (`LanguageGate`) blocks the rest of the app — including onboarding — until a
> visitor explicitly picks Hebrew/Amharic/English (no silent `navigator.language`
> guessing), since the app otherwise defaults to Hebrew. Once a document's pages
> are uploaded, a thumbnail strip (`DocumentPages`) lets the user reopen and
> pinch/button-zoom any page they photographed — including after a failed OCR —
> so they can verify the app read the right document. The in-app camera
> (`CameraCapture`) runs a client-side blur/exposure check (`lib/imageQuality.ts`,
> a Laplacian-variance pass) on every capture and warns (never blocks) before a
> bad photo costs the ~60s OCR+analysis round trip.

![Sample generated invoice PDF, branded with the Fana logo and colors](docs/invoice-sample.png)

## Tech stack

| Layer     | Tech                                                                 |
|-----------|----------------------------------------------------------------------|
| Backend   | ASP.NET Core 9 Web API · Clean Architecture · CQRS (MediatR) · Repository pattern · Dapper |
| Database  | PostgreSQL (via Npgsql); Railway-managed in production, Docker locally |
| Auth      | JWT access + refresh tokens, PBKDF2 password hashing; admins by `Admin:Emails` allowlist; forgot/reset-password flow emails a single-use, 1-hour link (`/forgot-password` → `/reset-password`) via the same `IEmailSender` built for invoicing; registration requires email verification before login — a new account starts unverified, gets emailed a single-use, 1-hour link (`/verify-email`), and only receives JWTs once that link is clicked (`/check-email` interstitial + a resend-verification option, both server- and login-page-side); account lockout after repeated failed logins (`Auth:MaxFailedLoginAttempts`/`Auth:LockoutMinutes`, on top of the per-IP rate limiter); "remember me" on login chooses `localStorage` (persists across browser restarts) vs. `sessionStorage` (cleared when the tab closes) for the token pair |
| Referrals | Vetted providers matched per document category · per-lead tracking with lifecycle status (New/Contacted/Responded/Converted/Invalid) for billing integrity · anonymous post-contact "was this helpful?" feedback → per-provider satisfaction rate · anonymous partner self-registration · admin approve/manage console |
| Invoicing | Admin-initiated, persisted PDF invoices (QuestPDF), branded with the Fana logo/colors, per provider + calendar month, snapshotting billable (Converted) leads so a later status change never rewrites history · emailed via SMTP (MailKit) · one invoice per provider/month (unique index) · generation always persists even if the email send fails (`Status=Failed` + `SendError`, PDF still downloadable) · a billing statement, not a payment-collection/tax document — no tax ID or bank details, since payment is handled directly, out-of-band |
| Organizations | B2B/B2G tenants (`Organizations`) — a user optionally tags itself to a tenant by slug at registration; admin-only tenant create/edit/activate-deactivate and an aggregate, anonymized usage dashboard (documents processed, unique members, category/urgency/weekly breakdowns) per tenant. Slug is locked after creation |
| AI        | `IAiProvider` → `ClaudeAiProvider` (default) / `OpenAiProvider` · prompt-cached system prompt + tool schema (`cache_control`, ~90% input-cost cut on repeats within the cache TTL) · per-call token usage and `stop_reason`-truncation logged (`AnthropicResponseHelpers`) · transport-failure + `Retry-After`-aware retry with an explicit 60s `HttpClient.Timeout` (`AnthropicHttp`) |
| OCR       | `IOcrProvider` → `ClaudeOcrProvider` (default, on a separate cheaper `Ai:AnthropicOcrModel` tier) / Google Vision / Azure · runs off the request thread — see Document processing below |
| Document processing | Upload persists every page and returns `202 Accepted` immediately; OCR then runs page-by-page in `DocumentProcessor`, driven by an in-memory queue + `DocumentProcessingWorker` background service (with startup reconciliation for anything left mid-job by a crash/redeploy). The client polls `GET /documents/{id}` (`Status`/`ProcessedPages`/`TotalPages`) instead of holding one long-lived request open |
| Analytics | Minimal funnel instrumentation (`AnalyticsEvents`) — registered/verified/uploaded/analyzed counts, viewable via `GET /api/admin/analytics/funnel` (admin only) and on the `/admin/analytics` dashboard page (period selector: 7/30/90 days); each stage is clickable and drills into the individual events (`GET /api/admin/analytics/funnel/{eventName}`) — who (email, or "deleted account" if the user's since been removed) and when. Tracking failures never fail the request they're attached to |
| TTS       | `ITtsProvider` → `AzureTtsProvider` (default) / ElevenLabs · audio cached per (document, language) |
| Frontend  | Next.js 15 · TypeScript · Tailwind CSS · shadcn-style UI             |
| Languages | Hebrew (default until chosen, RTL) · Amharic · English · a blocking first-run `LanguageGate` asks explicitly rather than guessing from `navigator.language` |
| Hosting   | Netlify (frontend) · Railway (API + PostgreSQL, + a Volume for uploaded files — see `DEPLOY.md`) |
| Hardening | Per-endpoint rate limiting (auth/trial/referrals) · CORS policy · PWA service worker · accessibility widget |
| Performance | Brotli/gzip response compression · output caching on the tenant-branding endpoint · indexed hot query paths (`Users.OrganizationId`, `DocumentAnalyses.CreatedAt`) · immutable-cached static assets and tree-shaken icon imports on the frontend |
| Health    | `/health` runs a real Postgres connectivity check (`DatabaseHealthCheck`), not a static literal — returns 503 when the database is unreachable |

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
   ├─ app/                      # landing, login, register, check-email, verify-email,
   │                           #   forgot-password, reset-password,
   │                           #   dashboard, upload, documents/[id], documents/[id]/chat, profile,
   │                           #   partners (self-registration), admin/providers,
   │                           #   admin/organizations (B2G tenant console),
   │                           #   admin/analytics (funnel dashboard)
   ├─ components/               # Navbar, UploadExperience, CameraCapture, AnalysisCard,
   │                           #   DocumentPages (page thumbnails + zoom lightbox),
   │                           #   ReferralBlock, LeadFeedbackPrompt, ShareButton, Onboarding,
   │                           #   LanguageGate, AccessibilityWidget, ServiceWorker, ui/
   ├─ lib/                      # api client, auth + language + organization contexts, types,
   │                           #   imageQuality (client-side blur/exposure check)
   ├─ i18n/                     # he / am / en dictionaries
   └─ jest.config.js, jest.setup.ts  # Jest + React Testing Library; *.test.ts(x) live in
                                      #   __tests__/ folders next to the code they cover
```

The backend Application layer is organized by feature (`Auth`, `Documents`,
`Chat`, `Trial`, `Partners`, `Referrals`, `Organizations`), each with its CQRS
commands/queries.

### Database schema
`Users` (with an optional `OrganizationId`), `RefreshTokens`, `Documents` (with
`Status`/`ProcessedPages`/`TotalPages`/`SkippedPages`/`ProcessingError` for the
background OCR pipeline, plus `PageContentTypes` alongside `PagePaths`),
`DocumentAnalyses`, `ChatMessages`, `TtsAudioCache`, `Providers`, `Leads` (with
`Status` and `Helpful` columns for lead lifecycle + post-contact feedback),
`Organizations` (B2B/B2G tenant branding), `Invoices` (persisted, immutable
per-provider/month billing snapshots, unique on `(ProviderId, PeriodYear,
PeriodMonth)`), `AnalyticsEvents` (minimal funnel instrumentation — event name +
optional user + timestamp) (see
`backend/src/AmharicHelper.Infrastructure/Migrations/`, numbered `001`–`018`).
Migrations are idempotent PostgreSQL and run on API startup. Analysis text
columns store JSON localized to `{ he, am, en }`.

### API endpoints
- `POST /api/auth/register | login | refresh | forgot-password | reset-password | verify-email | resend-verification`
- `GET  /api/users/me`, `GET /api/users/me/export` (GDPR Art. 15 data export — profile + every document/analysis/chat as JSON), `DELETE /api/users/me` (GDPR Art. 17 account erasure — irreversible)
- `POST /api/documents` (upload — returns `202 Accepted` immediately; OCR runs in the background, see Document processing above), `GET /api/documents` (list, with each document's processing `Status`), `GET /api/documents/{id}` (poll for `Status`/`ProcessedPages`/`TotalPages`/`SkippedPages`/`ProcessingError` and, once ready, the analysis)
- `POST /api/documents/{id}/analyze?category=`
- `GET  /api/documents/{id}/pages/{index}` (raw file for one uploaded page — image or PDF — so the user can review what they photographed; client-cached, immutable once uploaded)
- `GET  /api/documents/{id}/speech?language=` (spoken explanation, MP3)
- `GET|POST /api/documents/{id}/chat`
- `POST /api/trial/analyze`, `POST /api/trial/speech` (anonymous trial, nothing saved — still synchronous, capped at 5 pages)
- `GET  /api/referrals?category=` (matched providers), `POST /api/referrals/{providerId}/lead` (log a contact) — anonymous, rate-limited
- `POST /api/referrals/feedback/{refCode}` (post-contact "did this help?" signal) — anonymous, rate-limited
- `POST /api/partners/apply` (business self-registration, anonymous, rate-limited)
- `GET|PUT|POST|DELETE /api/admin/providers...` (review/manage providers — admin only)
- `GET  /api/admin/leads` (lead overview — admin only), `PUT /api/admin/leads/{id}/status` (update lead lifecycle status — admin only)
- `GET /api/admin/providers/{id}/invoices/status?year=&month=` (check if an invoice exists for a provider+month), `POST /api/admin/providers/{id}/invoices/generate` (generate + email a PDF invoice for the billable leads in that month), `GET /api/admin/providers/{id}/invoices/{invoiceId}/pdf` (re-download the PDF) — admin only
- `GET  /api/organizations/{slug}` (tenant branding lookup — anonymous, drives a white-labeled front end, output-cached 2 min)
- `POST /api/admin/organizations` (create a tenant), `GET /api/admin/organizations` (list), `PUT /api/admin/organizations/{id}` (edit branding — slug immutable), `POST /api/admin/organizations/{id}/active?value=` (activate/deactivate), `GET /api/admin/organizations/{id}/stats` (usage dashboard) — admin only
- `GET /api/admin/analytics/funnel?days=` (event counts over a trailing window — admin only), `GET /api/admin/analytics/funnel/{eventName}?days=` (drill down: the individual occurrences behind one count — admin only)
- `GET /health` (real Postgres connectivity check)

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
| `Ai__AnthropicOcrModel`   | Model used for OCR — a cheaper tier than `Ai__AnthropicModel` since OCR is high-volume transcription, not analysis-grade reasoning | `claude-haiku-4-5-20251001` |
| `Ocr__Provider`           | `Claude`, `Mock`, `Google`, or `Azure`             | `Claude`      |
| `Storage__RootPath`       | Where uploaded page files are written — point this at a mounted volume in production (see `DEPLOY.md`) or every redeploy loses uploaded files | `<app>/uploads` |
| `Tts__Provider`           | `Azure` or `ElevenLabs`                            | `Azure`       |
| `Tts__AzureSpeechKey` / `Tts__AzureRegion` | Azure Speech credentials          | empty         |
| `Admin__Emails`           | CSV of emails granted admin access (provider/lead consoles) | empty |
| `Auth__MaxFailedLoginAttempts` | Consecutive wrong-password attempts before an account is temporarily locked | `5` |
| `Auth__LockoutMinutes`    | How long an account stays locked after hitting the attempt limit | `15` |
| `Email__Provider` | `SendGridApi` (default, HTTPS — works on hosts like Railway that block outbound SMTP ports) or `Smtp` (MailKit, for hosts that don't) | `SendGridApi` |
| `Email__Smtp__Password` / `From` | SendGrid API key / verified sender, used to email generated invoice PDFs and password-reset links; via `SENDGRID_API_KEY` / `SENDGRID_FROM_EMAIL` in Docker. `Host`/`Port`/`Username` only matter for the `Smtp` provider | empty |
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
