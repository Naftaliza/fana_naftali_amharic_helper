# Deployment Guide — Amharic Helper

Two platforms: **Netlify** hosts the Next.js frontend; **Railway** hosts the .NET API
and a managed **PostgreSQL** database (Netlify can't run .NET or a database). Branch: `fana_mvp`.

Live backend: `https://fananaftaliamharichelper-production.up.railway.app` (`/health` → `{"status":"ok"}`).

---

## Railway — backend + database (already provisioned)

Project contains two services in the `production` environment.

### `Postgres` service (managed database)
- Add Railway's **PostgreSQL** plugin/service (New → Database → PostgreSQL). Railway
  provisions it with a **persistent volume that just works** — no custom image, no volume
  permission/IO hacks. This replaces the old SQL Server service, which could not run on
  Railway's volume (fatal `misaligned log IOs` / Stack Overflow on boot).
- It exposes `DATABASE_URL` (a `postgresql://user:pass@host:port/db` URL) plus `PGHOST`,
  `PGUSER`, etc. The API reads `DATABASE_URL` directly — the app converts the URL to an
  Npgsql connection string (see [SqlConnectionFactory.cs](backend/src/AmharicHelper.Infrastructure/Persistence/SqlConnectionFactory.cs)).
- The app creates its own tables on startup (idempotent migrations in
  [Migrations/](backend/src/AmharicHelper.Infrastructure/Migrations)), so no manual schema setup.

> One-time switch on the live project (Railway dashboard):
> 1. Delete the old `sqlserver` service (and its volume) entirely.
> 2. **New → Database → Add PostgreSQL.**
> 3. On the **API** service → **Variables**, set `ConnectionStrings__Default` to reference the
>    DB URL: value `${{Postgres.DATABASE_URL}}` (use whatever the Postgres service is named).
> 4. **Deploy** the API. It waits for the DB, runs migrations, and is ready — data now
>    survives every redeploy.

### `fana_naftali_amharic_helper` service (API)
- Source: GitHub repo, **Root Directory = `backend`**, build = Dockerfile (`backend/Dockerfile`).
- Outbound IPv6 / TCP-proxy tweaks from the SQL Server era are **no longer needed** — Npgsql
  connects to the Postgres service over Railway's private network using `DATABASE_URL`.
- Public domain generated on port **8080**.
- Variables (note the **double-underscore** config keys, NOT bare env names):

  | Variable | Value |
  |---|---|
  | `ConnectionStrings__Default` | `${{Postgres.DATABASE_URL}}` (reference the Postgres service's URL; or paste a `Host=...;Port=...;Database=...;Username=...;Password=...` string) |
  | `Jwt__Secret` | strong value, ≥32 chars |
  | `Jwt__Issuer` | `AmharicHelper` |
  | `Jwt__Audience` | `AmharicHelperClient` |
  | `Ai__Provider` | `Claude` |
  | `Ai__AnthropicApiKey` | (Anthropic key) |
  | `Ai__AnthropicModel` | `claude-sonnet-5` |
  | `Ocr__Provider` | `Claude` |
  | `Tts__Provider` | `Azure` |
  | `Tts__AzureSpeechKey` | (Azure Speech key) |
  | `Tts__AzureRegion` | `westus2` |
  | `ASPNETCORE_ENVIRONMENT` | `Production` |
  | `Frontend__Origin` | Netlify URL(s), comma-separated (see below) |

- The API auto-redeploys when `fana_mvp` is pushed to GitHub.
- Migrations run on startup ([DatabaseMigrator.cs](backend/src/AmharicHelper.Infrastructure/Persistence/DatabaseMigrator.cs)) and create the DB + tables.

---

## Netlify — frontend

1. New site from the GitHub repo, branch **`fana_mvp`**. `frontend/netlify.toml` sets
   base = `frontend`, build = `npm run build`, Node 20, `@netlify/plugin-nextjs`.
2. Site **Environment variables**:
   - `NEXT_PUBLIC_API_URL = https://fananaftaliamharichelper-production.up.railway.app`
3. Deploy → note the Netlify URL (e.g. `https://<site>.netlify.app`).

## Wire CORS (chicken-and-egg)

After Netlify is live, set the Railway API's `Frontend__Origin` to the Netlify URL
(comma-separate any preview domains), then redeploy the API:

```
Frontend__Origin = https://<site>.netlify.app,https://deploy-preview--<site>.netlify.app
```

The API splits this on commas (CORS multi-origin in
[Program.cs](backend/src/AmharicHelper.Api/Program.cs)).

---

## Verify

1. `GET https://<api>/health` → `{"status":"ok"}`.
2. Netlify site loads over HTTPS; DevTools Network shows API calls to the Railway URL
   with **no CORS errors**.
3. Anonymous upload → OCR → analysis → Amharic "Listen"; register/login; dashboard; delete.
4. Install as a PWA from the HTTPS Netlify URL.

## Security TODO

The Azure + Anthropic keys were shared during setup — **rotate them** and update the
Railway variables. The Postgres credentials are managed by Railway; if you ever expose
the DB via a public TCP proxy, rotate its password and keep the proxy disabled otherwise.
