# ThinkOnERP API documentation

This is the entry point for documentation generated from the current running code on 2026-07-18. Older root-level API summaries may describe earlier implementations; use the live Swagger/OpenAPI documents and current controllers as the source of truth.

## Documentation files

- [Endpoint catalog](API_ENDPOINT_CATALOG.md): every operation in both Swagger surfaces, with method, path, summary, accepted request content, and documented response codes.
- [SuperAdmin OpenAPI](openapi/superadmin.json): full machine-readable request/response schemas for the platform API.
- [Company OpenAPI](openapi/company.json): full machine-readable request/response schemas for tenant APIs.
- [Verification report](API_TEST_REPORT_2026-07-18.md): exactly what was compiled and exercised, observed results, gaps, and defects.

When running locally with the default HTTP launch profile:

- Swagger UI: `http://localhost:5160/swagger`
- SuperAdmin JSON: `http://localhost:5160/swagger/superadmin/swagger.json`
- Company JSON: `http://localhost:5160/swagger/company/swagger.json`
- Public health: `GET /api/health`

## API surfaces

The application publishes two overlapping OpenAPI documents:

| Surface | Purpose | Current size |
|---|---|---:|
| SuperAdmin | Platform administration, companies, reference configuration, documents, tickets/audit/monitoring, alerts, and security | 133 operations / 103 paths |
| Company | Tenant authentication, users, roles, permissions, branches, fiscal years, tickets, saved searches, and documents | 92 operations / 64 paths |
| Unique across both | Deduplicated by HTTP method and route | 213 operations |

`DocumentsController` is intentionally included in both surfaces, so the two surface totals cannot be added to calculate unique operations.

## Authentication and tenancy

Use `Authorization: Bearer <JWT>` for protected endpoints. Swagger's authorization dialog expects only the token value and adds the `Bearer` prefix.

Company authentication:

- `POST /api/Auth/login`
- `POST /api/Auth/refresh`

SuperAdmin authentication:

- `POST /api/auth/superadmin/login`
- `POST /api/auth/superadmin/refresh`

Company JWTs carry tenant context such as `companySchema`. `SchemaRoutingMiddleware` uses that claim to set Oracle `CURRENT_SCHEMA`; therefore a successful call in one tenant does not validate another tenant. Test central/SuperAdmin and tenant/company paths separately.

The main authorization policies are:

- authenticated user (`[Authorize]`)
- `AdminOnly`
- `SuperAdminOnly`

Important current defect: controller-level `[AllowAnonymous]` on `AuditHealthController` overrides method-level authorization. As verified on 2026-07-18, `/api/AuditHealth/metrics` and `/api/AuditHealth/replay-fallback` are anonymously accessible even though those actions declare `AdminOnly`.

## Request and response conventions

Normal JSON endpoints generally return `ApiResponse<T>` with success/error state, a message, and optional data. Validation failures normally return `400`; unauthenticated access returns `401`; insufficient permissions returns `403`; missing resources generally return `404`; unhandled failures are shaped by the global exception middleware.

Consult the OpenAPI JSON for the authoritative DTO schema for each operation. File upload operations use `multipart/form-data`; sending JSON to them can produce `415 Unsupported Media Type` before authentication/authorization is reported.

## Regenerating

Build and run the API, then execute:

```powershell
./scripts/generate-api-docs.ps1 -BaseUrl http://127.0.0.1:5160
```

This updates both OpenAPI JSON artifacts and the endpoint catalog.
