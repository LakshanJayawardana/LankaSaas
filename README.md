# LankaSaaS foundation

A tenant-safe SaaS foundation for Sri Lankan small and medium businesses. This release includes company registration, Admin/Staff authorization foundations, JWT access and refresh tokens, customers, products, expenses, invoices, and a responsive LKR-first web application. Broader ERP features are intentionally excluded.

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

Use a managed PostgreSQL database, HTTPS termination, a secret manager, strict production CORS, and versioned migrations. The current browser client stores its session in local storage; move refresh-token transport to a Secure, HttpOnly, SameSite cookie before exposing the application publicly. Add rate limiting and email verification alongside that hardening pass.
