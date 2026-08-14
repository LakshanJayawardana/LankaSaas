# Production validation

## Automated release decision

Run the complete local go/no-go gate against the Docker stack:

```powershell
.\tests\release-readiness.ps1 `
  -PlatformEmail "owner@example.com" `
  -PlatformPassword "your-platform-password"
```

During development, add `-AllowWorkingTreeChanges`; never use that switch for a release candidate. The command validates tracked Git state, Compose, containers, API liveness/readiness, correlation IDs, web availability, tenant isolation, department permissions, platform boundaries, backup/restore and backup freshness. It writes an ignored Markdown report under `outputs/` and ends with either `Decision: GO` or `Decision: NO-GO`.

Run these release gates against a freshly rebuilt stack before promoting a release.

```powershell
docker compose up -d --build
./tests/api-smoke.ps1
./tests/department-access-matrix.ps1
```

The department matrix creates temporary test tenants and one employee. It validates the exact permission definitions of all seven standard departments and exercises Viewer, Member, and Manager enforcement through a comprehensive test department. It also validates combined department access, immediate session invalidation, inactive departments, administration boundaries, and cross-tenant assignment rejection without bypassing authentication rate limits.

Use an isolated test database. The scripts intentionally leave their timestamped test records in place so failures can be investigated. Do not run them against a client production database.

## Release gate

- Backend Release build succeeds.
- Frontend type check and production build succeed.
- API smoke test passes.
- Department permission matrix passes.
- Production Compose configuration validates.
- Database backup is completed and its restore is tested in an isolated database.
- A client representative completes the acceptance checklist.

Any failed item blocks release. Record the commit SHA, execution date, tester, and evidence for each production candidate.
