# LankaSaaS coding rules
- Keep the backend a modular monolith: Domain -> Application -> Infrastructure -> API.
- Every tenant-owned entity implements `ITenantOwned`; never accept `TenantId` in client DTOs.
- Resolve tenant identity only from validated JWT claims and preserve EF global query filters.
- Use DTOs, validation, async database calls, and safe errors.
- Keep secrets in environment variables; never commit real credentials.
- Add tenant-isolation coverage for every tenant-owned feature.
- Keep the UI responsive, plain-language, LKR-first, and localization-ready.
- Do not add ERP modules until explicitly requested.
