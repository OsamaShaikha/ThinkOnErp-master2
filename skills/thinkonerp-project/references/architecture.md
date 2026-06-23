# ThinkOnERP architecture reference

## Solution

| Project | Responsibility |
|---|---|
| `ThinkOnErp.API` | Controllers, middleware, authentication, authorization, Swagger, OpenTelemetry |
| `ThinkOnErp.Application` | MediatR CQRS, DTOs, validators, pipeline behaviors |
| `ThinkOnErp.Domain` | Entities, interfaces, models, exceptions |
| `ThinkOnErp.Infrastructure` | Oracle EF Core, repositories, services, resilience, integrations, hosted workers |

The API references Application and Infrastructure. Application references Domain. Infrastructure references Domain and Application.

## Request pipeline

The effective order in `Program.cs` is:

1. Global exception handling
2. Swagger and optional Prometheus endpoint
3. CORS
4. Authentication
5. Request tracing
6. Force-logout check
7. Tenant schema routing
8. Authorization
9. Controllers

Changing this order can affect claims, tracing context, tenant routing, and authorization.

## Authentication surfaces

- Company login: `POST /api/Auth/login`
- Company refresh: `POST /api/Auth/refresh`
- SuperAdmin login: `POST /api/auth/superadmin/login`
- SuperAdmin refresh: `POST /api/auth/superadmin/refresh`

Company tokens may contain `userId`, `userName`, `role`, `companyId`, `branchId`, `isAdmin`, `companyCode`, and `companySchema`.

SuperAdmin tokens contain `isAdmin=true` and `isSuperAdmin=true`.

## Tenancy

The central schema stores platform entities such as company records and super administrators. A company may have an Oracle schema named by `SysCompany.CompanySchema`. Tenant requests set `CURRENT_SCHEMA` using the JWT claim.

Tenant-sensitive work must consider:

- Login before authenticated middleware exists
- Refresh-token lookup
- Connection pooling and Oracle session state
- Hosted/background services without an HTTP context
- Provisioning and upgrading existing tenant schemas
- Central versus tenant repositories sharing one `OracleDbContext`

## Main domains

- Companies, branches, currencies, fiscal years
- Users, roles, branch access, screen permissions
- Systems/modules, screens, features, branch provisioning
- Tickets, comments, attachments, SLA and reports
- Documents and storage providers
- Audit logs, request tracing, compliance, archival
- Security monitoring, alerts, metrics, Redis caching
- Key management and encryption

## Important implementation hotspots

- `src/ThinkOnErp.API/Program.cs`
- `src/ThinkOnErp.Infrastructure/DependencyInjection.cs`
- `src/ThinkOnErp.Infrastructure/Data/OracleDbContext.cs`
- `src/ThinkOnErp.API/Middleware/SchemaRoutingMiddleware.cs`
- `src/ThinkOnErp.API/Controllers/AuthController.cs`
- `src/ThinkOnErp.Infrastructure/Services/OracleSchemaService.cs`
- `src/ThinkOnErp.Infrastructure/Services/AuditLogger.cs`
- `src/ThinkOnErp.Infrastructure/Services/ResilientAuditLogger.cs`

## Known repository conditions

- The production API project builds.
- The complete solution has historically failed because tests lag current production interfaces.
- Package warnings have included MailKit/OpenTelemetry advisories and TDigest compatibility.
- Startup currently performs seed and tenant schema work; treat modifications there as high risk.
- Some archival, export, cloud key provider, and alert-rule paths remain incomplete.
- The repository contains many historical summaries and deployment variants; verify against current code.
