# Deployment Guide — Amharic Helper

Two platforms: **Netlify** hosts the Next.js frontend; **Railway** hosts the .NET API
and SQL Server (Netlify can't run .NET or SQL). Branch: `fana_mvp`.

Live backend: `https://fananaftaliamharichelper-production.up.railway.app` (`/health` → `{"status":"ok"}`).

---

## Railway — backend + database (already provisioned)

Project contains two services in the `production` environment.

### `sqlserver` service
- Image: `mcr.microsoft.com/mssql/server:2022-latest`
- Variables:
  - `ACCEPT_EULA=Y`
  - `MSSQL_SA_PASSWORD=Fana_Prod_Pass_2026!`
  - `MSSQL_PID=Developer`
  - `MSSQL_MEMORY_LIMIT_MB=3072`  ← caps SQL's memory so it doesn't read the host's
    full RAM and get OOM-killed. Required on Railway.
- **No volume** attached (the mssql container runs as non-root and can't write a
  root-owned Railway volume → permission crash). Trade-off: DB resets on redeploy.
  Acceptable for the MVP. Migrations recreate the schema on every boot.
- Private hostname: `sqlserver.railway.internal` (IPv4 & IPv6).

### `fana_naftali_amharic_helper` service (API)
- Source: GitHub repo, **Root Directory = `backend`**, build = Dockerfile (`backend/Dockerfile`).
- **Settings → Networking → Outbound IPv6 = ON** (lets the API reach SQL over the
  IPv6 private network; without it you get pre-login handshake resets via the public proxy).
- Public domain generated on port **8080**.
- Variables (note the **double-underscore** config keys, NOT bare env names):

  | Variable | Value |
  |---|---|
  | `ConnectionStrings__Default` | `Server=sqlserver.railway.internal,1433;Database=AmharicHelper;User Id=sa;Password=Fana_Prod_Pass_2026!;TrustServerCertificate=True;Encrypt=False` |
  | `Jwt__Secret` | strong value, ≥32 chars |
  | `Jwt__Issuer` | `AmharicHelper` |
  | `Jwt__Audience` | `AmharicHelperClient` |
  | `Ai__Provider` | `Claude` |
  | `Ai__AnthropicApiKey` | (Anthropic key) |
  | `Ai__AnthropicModel` | `claude-sonnet-4-6` |
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
Railway variables. Consider rotating `MSSQL_SA_PASSWORD` too (the SQL TCP proxy, if
enabled, exposes the DB publicly behind only this password).
