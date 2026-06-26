# Amharic Helper

Help Amharic-speaking residents of Israel understand official Hebrew documents
(government, bank, insurance, healthcare, municipality, employer). Upload a
document → OCR extracts the text → AI produces a structured analysis (summary,
type, urgency, key points, required actions, deadlines) **in all three languages**
→ listen to a spoken explanation → chat with the document for follow-up questions.

> **Status:** Deployed and running. Frontend on **Netlify**, backend + **PostgreSQL**
> on **Railway**. The full pipeline runs end-to-end on real providers: **Claude**
> for OCR (vision) and analysis, **Azure Neural TTS** for spoken explanations
> (incl. native Amharic voices). Every analysis field is generated in Hebrew,
> Amharic, and English so the UI and audio stay in one chosen language.

## Tech stack

| Layer     | Tech                                                                 |
|-----------|----------------------------------------------------------------------|
| Backend   | ASP.NET Core 9 Web API · Clean Architecture · CQRS (MediatR) · Repository pattern · Dapper |
| Database  | PostgreSQL (via Npgsql); Railway-managed in production, Docker locally |
| Auth      | JWT access + refresh tokens, PBKDF2 password hashing                 |
| AI        | `IAiProvider` → `ClaudeAiProvider` (default) / `OpenAiProvider`      |
| OCR       | `IOcrProvider` → `ClaudeOcrProvider` (default) / Google Vision / Azure |
| TTS       | `ITtsProvider` → `AzureTtsProvider` (default) / ElevenLabs · audio cached per (document, language) |
| Frontend  | Next.js 15 · TypeScript · Tailwind CSS · shadcn-style UI             |
| Languages | Hebrew (default, RTL) · Amharic · English                            |
| Hosting   | Netlify (frontend) · Railway (API + PostgreSQL) — see `DEPLOY.md`    |

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
   ├─ app/                      # landing, login, register, dashboard, upload,
   │                           #   documents/[id], documents/[id]/chat, profile
   ├─ components/               # Navbar, language picker, FileDropzone, AnalysisCard, ui/
   ├─ lib/                      # api client, auth + language contexts, types
   └─ i18n/                     # he / am / en dictionaries
```

### Database schema
`Users`, `RefreshTokens`, `Documents`, `DocumentAnalyses`, `ChatMessages`,
`TtsAudioCache` (see `backend/src/AmharicHelper.Infrastructure/Migrations/`).
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
- `GET /health`

## Local development

### Option A — everything in Docker (recommended)
```bash
cd "Fana 2.0"
export ANTHROPIC_API_KEY=sk-ant-...     # required for real OCR + analysis
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
cd backend && dotnet test
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
WhatsApp integration · mobile app · lawyer referral · government-forms assistant ·
appointment booking.
