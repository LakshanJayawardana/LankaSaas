# LankaSaaS foundation

A tenant-safe SaaS foundation for Sri Lankan small and medium businesses. This release includes company registration and invoice branding, Admin/Staff user management with tenant-scoped login activity, subscription plans with enforced active-user limits, JWT access and refresh tokens, customers, products, expenses, invoices, and a responsive LKR-first web application. Broader ERP features are intentionally excluded.

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
- PowerShell to run the API smoke suite

## Docker setup (recommended)

1. Copy `.env.example` to `.env`.
2. Replace `POSTGRES_PASSWORD` and `JWT_KEY` with strong secrets.
3. Run `docker compose up --build`.
4. Open `http://localhost:3001`; the API health endpoint is `http://localhost:8080/health`.

The API automatically applies versioned EF Core migrations during startup. The baseline migration safely adopts databases previously created with `EnsureCreated` by creating only missing tables and indexes.

After pulling schema changes, rebuild and restart the API; migrations run automatically without deleting local data.

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

Production release checks are documented in [docs/production-validation.md](docs/production-validation.md).
