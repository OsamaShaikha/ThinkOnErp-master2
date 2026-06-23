# ThinkOnERP verification reference

## Fast checks

```powershell
git status --short
git diff --check
dotnet build src\ThinkOnErp.API\ThinkOnErp.API.csproj --no-restore --verbosity minimal
```

If restore state is missing, run the build without `--no-restore`. Network or package downloads may require approval.

## Test strategy

Do not begin with `dotnet test ThinkOnErp.sln`. First inspect the relevant test project and build it:

```powershell
dotnet build tests\ThinkOnErp.API.Tests\ThinkOnErp.API.Tests.csproj --no-restore
dotnet build tests\ThinkOnErp.Infrastructure.Tests\ThinkOnErp.Infrastructure.Tests.csproj --no-restore
```

If the project compiles, run a focused filter:

```powershell
dotnet test tests\ThinkOnErp.API.Tests\ThinkOnErp.API.Tests.csproj --no-build --filter "FullyQualifiedName~FeatureName"
```

If it does not compile, report the errors as pre-existing or task-related based on the scoped diff. Do not repair unrelated historical tests unless requested.

## API smoke checks

Useful public checks:

- `GET /api/health`
- `GET /api/AuditHealth/status`

Useful authenticated audit checks:

- `GET /api/auditlogs/dashboard`
- `GET /api/auditlogs/legacy?pageNumber=1&pageSize=5`
- `GET /api/AuditHealth/metrics`

Audit log endpoints require an `isAdmin=true` JWT.

## HTML dashboard checks

Extract inline JavaScript and validate syntax:

```powershell
$html = Get-Content audit-logs-dashboard-api-connected.html -Raw
$script = [regex]::Match($html, '<script>([\s\S]*)</script>').Groups[1].Value
$script | node --check
```

Check for accidentally committed credentials or fixed hosts:

```powershell
rg -n "password|secret|Bearer |https?://" *.html
```

Inspect the page in the in-app browser when browser policy permits the target. A `file://` page may require the user to open it directly.

## Database-impact checklist

For entity or schema changes, verify:

1. Domain entity
2. EF configuration
3. `OracleDbContext` DbSet and configuration application
4. Repository interface and implementation
5. Dependency injection
6. Central migration or tenant schema creation SQL
7. Existing tenant upgrade path
8. Seed data, only if required
9. Oracle identifiers, Boolean mappings, sequences, and foreign keys

Never report a database change as fully verified without either executing it against an appropriate Oracle environment or explicitly stating that verification was compile/static only.
