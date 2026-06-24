# Amharic Helper

Help Amharic-speaking residents of Israel understand official Hebrew documents
(government, bank, insurance, healthcare, municipality, employer). Upload a
document → OCR extracts the text → AI produces a structured analysis (summary,
type, urgency, key points, required actions, deadlines, Amharic + simple-Hebrew
translations) → chat with the document for follow-up questions.

> **Status:** MVP **architecture + scaffolding**. The full pipeline runs end-to-end
> with a **Mock OCR** provider and an **AI provider** that uses Anthropic (Claude)
> when an API key is set, or returns deterministic mock results otherwise.
> Google/Azure OCR and the OpenAI provider are stubbed behind their interfaces.

## Tech stack

| Layer     | Tech                                                                 |
|-----------|----------------------------------------------------------------------|
| Backend   | ASP.NET Core 9 Web API · Clean Architecture · CQRS (MediatR) · Repository pattern · Dapper |
| Database  | SQL Server 2022 (via Docker)                                         |
| Auth      | JWT access + refresh tokens, PBKDF2 password hashing                 |
| AI        | `IAiProvider` → `ClaudeAiProvider` (real) / `OpenAiProvider` (stub)  |
| OCR       | `IOcrProvider` → `MockOcrProvider` / Google Vision / Azure (stubs)   |
| Frontend  | Next.js 15 · TypeScript · Tailwind CSS · shadcn-style UI             |
| Languages | Hebrew (default, RTL) · Amharic · English                            |

## Project structure

```
Fana 2.0/
├─ docker-compose.yml          # sqlserver + api + frontend
├─ backend/
│  ├─ AmharicHelper.sln
│  ├─ Dockerfile
│  └─ src/
│     ├─ AmharicHelper.Domain/         # entities, enums, value objects
│     ├─ AmharicHelper.Application/     # CQRS, DTOs, abstractions, prompt templates
│     ├─ AmharicHelper.Infrastructure/  # Dapper repos, migrations, providers, JWT
│     └─ AmharicHelper.Api/             # controllers, Program.cs, DI, Swagger
│  └─ tests/AmharicHelper.UnitTests/
└─ frontend/
   ├─ app/                      # landing, login, register, dashboard, upload,
   │                           #   documents/[id], documents/[id]/chat, profile
   ├─ components/               # Navbar, LanguageSwitcher, FileDropzone, AnalysisCard, ui/
   ├─ lib/                      # api client, auth + language contexts, types
   └─ i18n/                     # he / am / en dictionaries
```

### Database schema
`Users`, `RefreshTokens`, `Documents`, `DocumentAnalyses`, `ChatMessages`
(see `backend/src/AmharicHelper.Infrastructure/Migrations/001_init.sql`).

### API endpoints
- `POST /api/auth/register | login | refresh | forgot-password | reset-password`
- `GET  /api/users/me`
- `POST /api/documents` (upload + OCR), `GET /api/documents`, `GET /api/documents/{id}`
- `POST /api/documents/{id}/analyze?category=`
- `GET|POST /api/documents/{id}/chat`
- `GET /health`

## Local development

### Option A — everything in Docker (recommended)
```bash
cd "Fana 2.0"
export ANTHROPIC_API_KEY=sk-ant-...     # optional; omit to use mock AI
docker compose up --build
```
- API: http://localhost:5080 (Swagger at `/swagger`)
- Frontend: http://localhost:3000
- SQL Server: `localhost,1433` (sa / `Your_strong_Pass123`)

Migrations and seed scripts run automatically on API startup (idempotent).

### Option B — run pieces locally
```bash
# 1. Database only
docker compose up sqlserver

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

| Key                       | Purpose                                  | Default     |
|---------------------------|------------------------------------------|-------------|
| `ConnectionStrings__Default` | SQL Server connection string          | local sa    |
| `Jwt__Secret`             | JWT signing key (≥32 chars)              | placeholder |
| `Ai__Provider`            | `Claude` or `OpenAI`                     | `Claude`    |
| `Ai__AnthropicApiKey` / `ANTHROPIC_API_KEY` | Claude API key            | empty (mock)|
| `Ocr__Provider`           | `Mock`, `Google`, or `Azure`             | `Mock`      |

## Production deployment plan

1. **Containers** — build `backend/Dockerfile` and `frontend/Dockerfile`, push to a
   registry (ACR / ECR / GHCR). Tag by git SHA.
2. **Database** — use a managed SQL Server (Azure SQL / AWS RDS). The startup
   migrator runs the idempotent scripts; for stricter control, gate migrations to
   a one-off job. Take regular backups.
3. **Secrets** — inject `Jwt__Secret`, `ANTHROPIC_API_KEY`, and the connection
   string via the platform secret store (Azure Key Vault / AWS Secrets Manager).
   Never bake secrets into images.
4. **File storage** — replace `LocalFileStorage` with blob storage (Azure Blob /
   S3) behind `IFileStorage` so uploads survive container restarts and scale out.
5. **Hosting** — run API + frontend on a container platform (Azure Container Apps,
   AWS ECS/Fargate, or Kubernetes). Front with a reverse proxy / ingress
   terminating HTTPS; set `Frontend__Origin` to the real domain for CORS.
6. **Scaling & ops** — stateless API scales horizontally; add health probes on
   `/health`, structured logging/metrics, and rate limiting on auth + upload.
7. **Hardening before launch** — implement real password-reset email delivery,
   a real OCR provider (Google Vision / Azure), refresh-token rotation/revocation
   on logout, and validation of upload size/type at the edge.

## Future features (not implemented)
Voice explanations in Amharic · WhatsApp integration · mobile app · lawyer
referral · government-forms assistant · appointment booking.
