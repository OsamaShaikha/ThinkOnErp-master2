---
name: thinkonerp-project
description: Work safely and effectively in the ThinkOnERP ASP.NET Core 8 and Oracle repository. Use for implementing, debugging, reviewing, testing, documenting, or deploying ThinkOnERP features involving its Clean Architecture layers, CQRS/MediatR handlers, Oracle schema-per-company tenancy, authentication, permissions, audit logging, ticketing, documents, monitoring, Docker, or database scripts.
---

# ThinkOnERP Project

## Start every task

1. Run `git status --short` and preserve all unrelated or uncommitted user changes.
2. Locate code with `rg` or `rg --files`; ignore `bin/`, `obj/`, generated logs, and historical summary documents unless the task explicitly concerns them.
3. Treat running code as authoritative. The root README and many `*_SUMMARY.md` files may describe earlier designs.
4. Read [architecture.md](references/architecture.md) when changing cross-layer behavior, tenancy, permissions, authentication, auditing, or startup.
5. Read [verification.md](references/verification.md) before choosing build or test commands.

## Follow the layer boundaries

- Put HTTP routing, authorization attributes, response shaping, and middleware in `src/ThinkOnErp.API`.
- Put DTOs, validators, CQRS commands/queries, handlers, and application orchestration in `src/ThinkOnErp.Application`.
- Put entities, domain models, exceptions, and interfaces in `src/ThinkOnErp.Domain`.
- Put EF Core mappings, Oracle access, repository implementations, integrations, resilience, and hosted services in `src/ThinkOnErp.Infrastructure`.
- Register application services in `Application/DependencyInjection.cs` and infrastructure services in `Infrastructure/DependencyInjection.cs`.

Prefer existing project patterns over introducing a second pattern. Trace a nearby complete feature from controller through handler, interface, repository, entity/configuration, and registration before implementing a new one.

## Protect tenant isolation

- Distinguish the central platform schema from tenant company schemas.
- Company login resolves `CompanyCode`, authenticates against the tenant schema, and issues a JWT containing `companySchema`.
- Authenticated tenant requests use `SchemaRoutingMiddleware` to set Oracle `CURRENT_SCHEMA`.
- Never trust a schema identifier merely because it came from a JWT. Validate it as an Oracle identifier and, when changing routing, verify it against an authoritative company record.
- Verify that refresh-token, background-service, and anonymous flows have explicit tenant context; they cannot depend on authenticated middleware claims.
- Test central and tenant paths separately.

## Implement API changes

1. Identify whether the endpoint belongs to the SuperAdmin or Company Swagger surface in `Program.cs`.
2. Reuse `ApiResponse<T>` for normal JSON responses.
3. Apply the narrowest existing authorization policy.
4. Add validation in FluentValidation or explicit controller validation consistent with the neighboring feature.
5. Add or update interfaces before infrastructure implementations.
6. Register every new repository, service, authorization handler, or hosted service.
7. Update entity configuration and tenant provisioning/upgrade logic together when adding tenant tables.
8. Avoid placing secrets, default passwords, or deployment addresses in source or HTML tools.

## Change Oracle persistence carefully

- Follow existing EF configurations for Oracle naming and `NUMBER(1)` Boolean mapping.
- Treat schema creation, grants, `ALTER SESSION`, and raw SQL as security-sensitive.
- Do not concatenate user-controlled values into SQL.
- Keep central-schema migrations and tenant-schema upgrades distinct.
- Make provisioning idempotent and safe under repeated execution.
- Do not silently mutate every tenant during ordinary request handling.

## Work with audit and monitoring code

The audit subsystem uses asynchronous queues, resilient decorators, fallback files, encryption, signatures, archival, compliance reporting, and monitoring. Preserve graceful degradation: audit failures should be observable without breaking unrelated ERP requests unless the existing contract explicitly requires failure.

When changing an audit event, check:

- `AuditLoggingBehavior` and request middleware
- Domain audit models and interfaces
- `AuditLogger`, `ResilientAuditLogger`, and repositories
- Sensitive-data masking and encryption
- Query DTOs/controllers and legacy compatibility
- Fallback replay, metrics, and relevant hosted services

## Verify proportionally

Always build the production API project after source changes:

```powershell
dotnet build src\ThinkOnErp.API\ThinkOnErp.API.csproj --no-restore --verbosity minimal
```

Do not claim the full test suite passes unless it actually does. The current test projects contain substantial historical drift. Run focused tests only after confirming the selected project or test subset compiles. Report pre-existing failures separately from failures caused by the change.

For HTML test dashboards:

- Keep the API base URL configurable.
- Accept login credentials interactively or a pasted JWT; never hard-code credentials.
- Escape API values before inserting them with `innerHTML`.
- Display HTTP status, latency, and raw error responses.
- Test public health separately from authenticated AdminOnly endpoints.

## Finish the task

- Review `git diff --check` and the scoped diff.
- State exactly what was changed and what was verified.
- Call out database scripts, environment variables, migrations, or deployment steps the user still needs to run.
- Do not present TODO-backed or `NotImplementedException` paths as production-complete.
