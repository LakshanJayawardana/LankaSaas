# Local client demo and Cloudflare Quick Tunnel runbook

This runbook explains how to operate the isolated `lankasaas-demo` environment on a Windows laptop and share it temporarily through Cloudflare Quick Tunnel. It is intended for controlled client evaluation, not paid production hosting.

## What this environment provides

- A separate Docker Compose project named `lankasaas-demo`
- A separate PostgreSQL volume and clean demo database
- Web access on `http://127.0.0.1:3002`
- API access on `http://127.0.0.1:8081`
- A temporary HTTPS URL under `trycloudflare.com`
- Production-mode secure cookies and application safeguards

The original development stack can continue using ports `3001` and `8080`. Do not run automated smoke or access-matrix tests against the demo database because those tests create many disposable tenants.

## One-time setup

From PowerShell, navigate to the repository:

```powershell
cd "C:\Users\Madus\Documents\Codex\2026-08-11\build"
```

Create the private demo environment file:

```powershell
Copy-Item .env.example .env.demo
notepad .env.demo
```

Use unique, non-placeholder values. The important settings are:

```dotenv
POSTGRES_DB=lankasaas_demo
POSTGRES_USER=postgres
POSTGRES_PASSWORD=<strong-random-database-password>
JWT_KEY=<random-secret-of-at-least-32-bytes>
PLATFORM_JWT_KEY=<different-random-secret-of-at-least-32-bytes>
PLATFORM_ADMIN_EMAIL=<operator-email>
PLATFORM_ADMIN_PASSWORD=<strong-one-time-bootstrap-password>
NEXT_PUBLIC_API_URL=/api
WEB_PORT=3002
API_PORT=8081
PAYHERE_SANDBOX=true
```

Each setting must appear only once. `JWT_KEY` and `PLATFORM_JWT_KEY` must be different. Production mode rejects values containing `replace-with` and the database password `change-me`.

Generate secrets in Windows PowerShell 5.1 or later:

```powershell
function New-RandomSecret {
    param([int]$Bytes = 48)
    $buffer = New-Object byte[] $Bytes
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($buffer)
        [Convert]::ToBase64String($buffer)
    }
    finally {
        $generator.Dispose()
    }
}

New-RandomSecret
```

`.env.demo` is ignored by Git. Never paste its contents into commits, screenshots, tickets, or client messages.

Start and build the isolated environment:

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  up -d --build
```

After the first successful platform-owner login, remove `PLATFORM_ADMIN_PASSWORD` from `.env.demo` and recreate the API container. The existing owner remains in the database; the bootstrap password does not need to remain in container configuration.

## Start after restarting the laptop

1. Start Docker Desktop.
2. Wait until Docker Engine reports that it is running.
3. Open PowerShell and run:

```powershell
cd "C:\Users\Madus\Documents\Codex\2026-08-11\build"

docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  up -d
```

Confirm the services:

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  ps
```

Expected services are `db`, `api`, `web`, and `tunnel`. All must show `Up`; the database must also show `healthy`.

## Get the current Cloudflare URL

The most reliable command is:

```powershell
docker logs lankasaas-demo-tunnel-1 --tail 100
```

Use the newest URL matching:

```text
https://some-random-words.trycloudflare.com
```

The log should later contain `Registered tunnel connection`. Older Quick Tunnel URLs may be expired. A restart or recreation of the tunnel can produce a different URL, so confirm the current URL before sharing it.

To force a new tunnel session:

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  up -d --force-recreate tunnel

Start-Sleep -Seconds 15
docker logs lankasaas-demo-tunnel-1 --tail 100
```

## Health checks before sharing

Check the database-aware API readiness endpoint:

```powershell
Invoke-RestMethod http://127.0.0.1:8081/health/ready
```

Expected fields include `status: ready` and `database: available`.

Check the frontend:

```powershell
Invoke-WebRequest http://127.0.0.1:3002/login -UseBasicParsing
```

Expected HTTP status is `200`. Then open the current Cloudflare URL in an Incognito window and test:

1. Registration for a test client tenant
2. Login, logout, and login again
3. Dashboard and one operational workflow
4. Platform login using the operator account only
5. Tenant suspension and reactivation
6. Isolation between two test client tenants

Do not send platform-owner credentials or the `/platform/login` path to clients.

## Routine commands

Show service status:

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  ps
```

Show application logs:

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  logs api web --tail 150
```

Restart only the tunnel:

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  restart tunnel
```

Stop the demo while preserving its database:

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  down
```

Never add `-v` to the `down` command unless the explicit intention is to permanently delete the demo database volume.

## Troubleshooting

### Cloudflare page shows 502 Bad Gateway or Host Error

Cloudflare is reachable, but `cloudflared` cannot reach the web service.

```powershell
Invoke-WebRequest http://127.0.0.1:3002 -UseBasicParsing

docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  ps
```

If the frontend is healthy, recreate the tunnel and use its newest URL. If the frontend is unhealthy, inspect `web` and `api` logs first.

### No URL appears in filtered Compose logs

The creation message may be older than the selected time window. Read the container logs directly:

```powershell
docker logs lankasaas-demo-tunnel-1 --tail 100
```

If the container is missing or silent, recreate it with `up -d --force-recreate tunnel`.

### Tunnel repeatedly reports QUIC timeouts

Temporary network interruptions normally recover automatically. Confirm that a later log line says `Registered tunnel connection`. If UDP/QUIC is consistently blocked by the local network, use a named production tunnel configured for HTTP/2 or change the demo tunnel command only through a reviewed Compose change.

### API container shows `Restarting`

```powershell
docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  logs api --tail 150
```

Common causes:

- Placeholder secrets in `.env.demo`
- `JWT_KEY` shorter than 32 bytes
- Identical tenant and platform JWT keys
- Database password changed after the database volume was initialized
- Invalid or duplicate environment entries

Do not delete a database volume merely to troubleshoot. If the demo database contains client-entered data, take a verified backup before changing database credentials or storage.

### Port 8080 or 3001 is already allocated

The development stack is probably using those ports. The demo environment must use:

```dotenv
API_PORT=8081
WEB_PORT=3002
```

Remove duplicate port entries from `.env.demo`, then recreate the demo containers without deleting volumes.

### Production placeholder secrets must be replaced

Edit `.env.demo` and replace all example values. In particular, do not use `change-me` or values containing `replace-with`. If a brand-new empty PostgreSQL volume was initialized with the wrong password, it may be recreated. Never do this after client data has been entered; change the PostgreSQL password through a controlled procedure instead.

### Browser displays an old Cloudflare error

Confirm the newest URL in the tunnel logs and open it in an Incognito window. Quick Tunnel hostnames are temporary and may be cached after expiration.

## Data and security rules

- Treat the demo database as client-visible data and back it up before important demonstrations.
- Never run `api-smoke.ps1`, `department-access-matrix.ps1`, `platform-administration.ps1`, or `release-readiness.ps1` against this demo stack.
- Never expose PostgreSQL publicly.
- Never share `.env.demo`, JWT keys, database credentials, or platform-owner credentials.
- Use a separate tenant for every client.
- Use the platform console to enforce subscription status and user limits.
- Quick Tunnel requires the laptop, Docker Desktop, application containers, internet connection, and tunnel container to stay running.
- Quick Tunnel is for evaluation only. Move paying clients to the production deployment described in [production-deployment.md](production-deployment.md).

## Moving beyond the laptop demo

For continuous client access, deploy the reviewed release to a VPS, use a named Cloudflare Tunnel and owned hostname, automate encrypted off-site backups, monitor readiness and tunnel availability, and follow the production release gates. Quick Tunnel does not provide a stable hostname or production availability guarantee.
