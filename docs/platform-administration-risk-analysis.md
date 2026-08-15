# Platform administration risk analysis

## Purpose

Platform administration is the operator control plane for the WebWaves Digital event operations platform. It manages tenants and subscription access. It is deliberately separate from a tenant `Admin`, which only manages one client's company.

## Security boundaries

- Platform users are global records and never implement `ITenantOwned`.
- Platform JWTs use a separate signing key, issuer, audience, authentication scheme and role.
- Platform tokens contain no `tenant_id` claim and cannot authenticate against tenant endpoints.
- Tenant tokens cannot satisfy the platform authorization policy.
- Platform endpoints expose tenant identity and subscription metadata only; they do not expose events, customers, finance, employees or other tenant business records.
- Every subscription mutation requires a reason and creates an immutable platform audit event.

## Subscription behavior

`Trialing`, `Active`, `PastDue`, `Suspended`, `Cancelled` and `Expired` are supported. A suspended or access-ended tenant remains able to read its data, but business-data mutations return HTTP 402. This avoids data loss and allows the client to review/export records while payment is resolved.

## Main risks and controls

| Risk | Impact | Control |
|---|---|---|
| Tenant administrator gains platform access | Critical cross-tenant control | Separate identity table, JWT key/audience/scheme and platform-only policy |
| Platform token is accepted by tenant API | Critical tenant-data exposure | Tenant API validates only the tenant JWT scheme and requires tenant claims |
| Operator views routine client records | Privacy breach | Platform API returns subscription metadata only; no impersonation feature |
| Subscription changed accidentally | Client outage | Required reason, confirmation in future UI, and audit history |
| Concurrent payment callback and manual action conflict | Incorrect status | Tenant-scoped PostgreSQL advisory transaction lock |
| Suspension destroys or hides client data | Trust and recovery risk | Suspension is read-only and data is retained |
| Bootstrap credentials leak | Full platform compromise | Environment-only secrets, minimum length, no logging, create only when no platform owner exists |
| Platform session remains after access revocation | Unauthorized persistence | Active flag and access-version validation on every token validation |

## Operational requirements before production

1. Generate different random values of at least 32 bytes for `JWT_KEY` and `PLATFORM_JWT_KEY`.
2. Set bootstrap email/password only for the first owner creation. Remove the bootstrap password from the deployment environment afterward.
3. Restrict `/api/platform/*` with Cloudflare Access or an equivalent operator-only network policy in addition to application authentication.
4. Back up PostgreSQL before subscription-control releases and test restore procedures.
5. Run `tests/platform-administration.ps1` plus the existing smoke and department-access tests.

## Deferred intentionally

- Tenant impersonation is excluded because it materially increases cross-tenant privacy risk.
- Tenant deletion is excluded. A separate retention and recoverable archival design is required.
- Multiple platform roles and owner management are deferred until last-owner protection, MFA and recovery workflows are designed.
