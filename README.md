# LankaSaaS

A tenant-safe, event-operations SaaS application for Sri Lankan businesses. The current release candidate covers event planning, staffing and location-aware attendance, logistics, purchasing, quotations and invoices, event finance and reporting, accounting, customers, products, department-based access control, tenant branding, subscriptions, and a separate platform-owner console.

The application is available as a controlled local client demo through Cloudflare Quick Tunnel and includes production-readiness checks, tenant-isolation coverage, verified backup/restore tooling, health endpoints, structured logging, and auditable platform administration.

## Architecture

The backend is a modular monolith with one-way dependencies:

- `LankaSaaS.Domain`: entities and domain roles
- `LankaSaaS.Application`: request/response DTOs and application contracts
- `LankaSaaS.Infrastructure`: EF Core PostgreSQL persistence and tenant query filters
- `LankaSaaS.Api`: HTTP endpoints, authentication, authorization, validation, and safe problem responses
- `src/frontend`: Next.js App Router application

Every business entity implements `ITenantOwned`. Its `TenantId` is assigned from authenticated JWT claims during inserts and EF Core global query filters scope reads, updates, and deletes. Resource request DTOs do not expose `TenantId`. Refresh tokens are random, stored only as SHA-256 hashes, rotated on refresh, and revoked on logout.

## Prerequisites

- Docker Desktop with Compose, or .NET 10 SDK + Node.js 24 + PostgreSQL 17
- PowerShell 5.1 or later for local operations and test suites
- Git and GitHub CLI for the reviewed branch and release workflow

## Docker setup (recommended)

1. Copy `.env.example` to `.env`.
2. Replace the PostgreSQL password, tenant JWT key, platform JWT key, platform-owner email, and one-time platform-owner password. The two JWT keys must be strong and different.
3. Run `docker compose up --build`.
4. Open `http://localhost:3001`; the API health endpoint is `http://localhost:8080/health`.

The API automatically applies versioned EF Core migrations during startup. The baseline migration safely adopts databases previously created with `EnsureCreated` by creating only missing tables and indexes.

After pulling schema changes, rebuild and restart the API; migrations run automatically without deleting local data.

## Developer and operations handbook

The primary handbook covers local development, Docker, GitHub authentication, branch and pull-request workflow, testing, release tags, the isolated client-demo database, laptop restart, Cloudflare URL retrieval, health checks, shutdown, security, and troubleshooting:

- [Developer, GitHub, and Cloudflare client-demo handbook](docs/local-demo-cloudflare.md)

Client demonstrations must use the separate `lankasaas-demo` Compose project and `.env.demo`, not the development database.

The normal startup after restarting the laptop is:

```powershell
cd "C:\Users\Madus\Documents\Codex\2026-08-11\build"

docker compose `
  --project-name lankasaas-demo `
  --env-file .env.demo `
  -f docker-compose.yml `
  -f docker-compose.demo.yml `
  up -d

docker logs lankasaas-demo-tunnel-1 --tail 100
```

Use the newest `trycloudflare.com` URL followed by `Registered tunnel connection`. Quick Tunnel is temporary and suitable only for evaluation; it requires Docker Desktop and the laptop to remain online.

## Local setup

Backend configuration uses standard ASP.NET Core environment-variable mapping:

- `ConnectionStrings__Default`
- `Jwt__Key` (at least 32 random characters)
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__AccessMinutes`
- `Jwt__RefreshDays`
- `FrontendUrl`
- `PayHere__MerchantId`, `PayHere__MerchantSecret`, and `PayHere__PublicApiUrl`
- `PayHere__AppId` and `PayHere__AppSecret` for subscription cancellation

Run the API:

```powershell
dotnet restore
dotnet run --project src/backend/LankaSaaS.Api
```

Run the frontend:

```powershell
cd src/frontend
npm install
npm run dev
```

Set `NEXT_PUBLIC_API_URL` before the frontend build when the API is not at `http://localhost:8080/api`.

## Database migrations

Install the EF CLI and create the first versioned migration before a production deployment:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/backend/LankaSaaS.Infrastructure --startup-project src/backend/LankaSaaS.Api
dotnet ef database update --project src/backend/LankaSaaS.Infrastructure --startup-project src/backend/LankaSaaS.Api
```

## Build and tests

```powershell
dotnet build LankaSaaS.slnx
cd src/frontend
npm run typecheck
npm run build
cd ../..
powershell -File tests/api-smoke.ps1
```

The smoke suite expects the Docker stack to be running. It verifies registration, login, unauthorized rejection, customer creation, product creation, tenant-filtered lists, and a direct cross-tenant customer access attempt.

Run the complete local release gate before creating a release candidate:

```powershell
.\tests\release-readiness.ps1 `
  -PlatformEmail "<platform-owner-email>" `
  -PlatformPassword "<platform-owner-password>"
```

Never run data-creating smoke, department-access, platform-administration, or release-readiness suites against a client demo or production database. They deliberately create disposable tenants and records.

GitHub Actions repeats backend/frontend builds and runs the smoke suite against a real PostgreSQL container. Authentication endpoints are rate-limited per client IP, and refresh tokens are rotated through an HttpOnly cookie rather than exposed to browser JavaScript.

## Production notes

PayHere recurring checkout is server-signed, and subscription state changes only after a verified, idempotent notification. Its notification URL must be publicly reachable over HTTPS because PayHere cannot notify localhost. Keep merchant credentials in environment variables and begin with sandbox mode.

Use a managed PostgreSQL database, HTTPS termination, a secret manager, strict production CORS, and versioned migrations. Access and database secrets are required environment variables and must never be stored in source control. Refresh tokens use Secure, HttpOnly, SameSite cookies in production.

Run the production override behind an HTTPS reverse proxy:

```powershell
docker compose -f docker-compose.yml -f docker-compose.production.yml up --build -d
```

The production override binds the API and web ports to loopback. Configure the proxy to send `X-Forwarded-For` and `X-Forwarded-Proto`, and expose only ports 80/443 publicly. Check `/health` for liveness and `/health/ready` for database readiness. Authenticated mutations receive a correlation ID and create tenant-scoped audit events available to Admins at `/api/audit`.

### PostgreSQL backup and restore

Create encrypted, off-host backups regularly and test restores away from production:

```powershell
docker compose exec db pg_dump -U postgres -d lankasaas -Fc -f /tmp/lankasaas.backup
docker compose cp db:/tmp/lankasaas.backup ./lankasaas.backup
```

Restore into an empty database after stopping API writes:

```powershell
docker compose cp ./lankasaas.backup db:/tmp/lankasaas.backup
docker compose exec db pg_restore -U postgres -d lankasaas --clean --if-exists /tmp/lankasaas.backup
```

Never overwrite the only production database while testing a restore. Configure retention, encryption, access controls, and automated restore drills in the deployment platform.

## Production validation

Operational documentation:

- [Developer, GitHub, and Cloudflare client-demo handbook](docs/local-demo-cloudflare.md)
- [Production validation and release gates](docs/production-validation.md)
- [VPS deployment, backups, rollback, and monitoring](docs/production-deployment.md)

The local Cloudflare Quick Tunnel is not the production deployment. Paying clients should move to the reviewed VPS process with a named tunnel or HTTPS reverse proxy, a stable hostname, automated encrypted off-site backups, monitoring, and controlled releases.
