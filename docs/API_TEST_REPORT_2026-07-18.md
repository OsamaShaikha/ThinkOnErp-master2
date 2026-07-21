# API verification report — 2026-07-18

## Executive result

The production API compiles and its complete routing/OpenAPI surface starts locally. The runtime exposes 213 unique operations. A non-destructive invalid-token sweep exercised routing/authentication for all 213 operations: 194 rejected the request with `401`, while 19 public, content-type-gated, or defective routes produced other results.

This is not a full business-level pass. Both main automated test projects currently fail to compile, and no reachable Oracle tenant plus valid Company/SuperAdmin credentials were supplied. Database CRUD, tenant isolation, refresh-token behavior, permissions, document storage, notifications, and destructive workflows therefore remain unverified end to end.

## Environment

- Repository: `D:\ThinkOnErp`
- Date: 2026-07-18
- Runtime target: .NET 8 (built with installed SDK 10.0.301)
- Smoke-test URL: `http://127.0.0.1:5199`
- Environment: Development
- Prometheus exporter disabled for the isolated smoke process

No credentials were embedded in generated documentation, and no valid login or mutating business request was sent.

## Build and automated-test results

| Check | Result | Evidence |
|---|---|---|
| Production API build | PASS | `dotnet build src\ThinkOnErp.API\ThinkOnErp.API.csproj --no-restore --verbosity minimal` completed successfully |
| API test project compile | FAIL | 14 errors, 20 warnings |
| Infrastructure test project compile | FAIL | 312 errors, 71 warnings |
| Test execution | BLOCKED | Tests cannot run until their projects compile |

Representative API-test drift:

- `AuthController` tests omit the current logger dependency.
- Tests pass the older `LoginCommand` where `CompanyLoginCommand` is now required.
- Tests initialize a removed `CreateUserCommand.BranchId` property.

Representative infrastructure-test drift:

- Removed `AddTraceabilitySystem` registration method.
- Old `ThinkOnErpDbContext` and `OracleDbContext.CreateConnection` APIs.
- Removed/renamed alert, SLA, compliance, storage, and audit members.
- Mocks no longer match nullable `Task<long?>` audit signatures.

These errors existed before this documentation task; the working tree was clean at the start.

## Runtime smoke results

| Probe | Result | Interpretation |
|---|---:|---|
| `GET /api/health` | 200 | API process is running |
| `GET /api/health/detailed` | 200 | Detailed application health route is reachable |
| `GET /api/Monitoring/health` | 200 | Public monitoring health route is reachable |
| `GET /api/AuditHealth/status` | 503 | Audit subsystem reports degraded/unavailable infrastructure |
| SuperAdmin Swagger JSON | 200 | 133 operations / 103 paths generated |
| Company Swagger JSON | 200 | 92 operations / 64 paths generated |

### Invalid-token sweep

Every unique OpenAPI operation was invoked once with `Authorization: Bearer invalid-token`. Route parameters were replaced with the inert value `1`; JSON operations received `{}`. This prevents protected actions from reaching business logic and avoids data mutation.

| Status | Count | Meaning |
|---:|---:|---|
| 401 | 194 | Protected route correctly rejected invalid authentication |
| 200 | 5 | Public health routes plus anonymously exposed AuditHealth actions |
| 400 | 2 | Anonymous refresh endpoints rejected an empty request |
| 415 | 8 | Multipart/form-data endpoints rejected JSON before an auth response |
| 500 | 3 | Empty login requests and anonymous document metadata failed at runtime |
| 503 | 1 | Audit health correctly reported degradation |
| Total | 213 | Complete unique route/method inventory |

The eight `415` results are content-negotiation checks, not proof that authentication can be bypassed. They should be retested with correctly formed multipart requests and valid/invalid tokens.

## Defects and risks found

### High — AuditHealth authorization is bypassed

`AuditHealthController` has controller-level `[AllowAnonymous]`. In ASP.NET Core, that metadata overrides action-level `[Authorize]`; consequently:

- `GET /api/AuditHealth/metrics` returned `200` without valid authentication.
- `POST /api/AuditHealth/replay-fallback` returned `200` without valid authentication.

The replay endpoint is operational/mutating and should not be public. Move `[AllowAnonymous]` to only the intended status action or remove it from the controller.

### Medium — Anonymous malformed login requests return 500

Both Company and SuperAdmin login returned `500` for `{}` rather than a validation-oriented `400`. Confirm DTO validation and null handling before database access.

### Medium — Anonymous document metadata returns 500

`GET /api/documents/metadata` is explicitly anonymous but returned `500` in the local environment. Determine whether it requires unavailable storage/database configuration or contains an independent null/configuration defect.

### Dependency warnings

The production build reports known moderate-severity advisories for MailKit 4.3.0 and OpenTelemetry ASP.NET Core/HTTP instrumentation 1.7.1. C5 2.3.0.1 and TDigest 1.0.8 were restored using .NET Framework compatibility assets rather than native `net8.0` assets.

## Required work for a true full API certification

1. Repair or replace the drifted API and infrastructure tests so both projects compile.
2. Provision an isolated Oracle central schema plus at least two tenant schemas.
3. Supply test-only SuperAdmin, company admin, normal user, and restricted-user credentials.
4. Run positive and negative contract tests for every operation using the request schemas in `docs/openapi/*.json`.
5. Verify cross-tenant denial by attempting tenant A identifiers/token against tenant B resources.
6. Verify all mutating workflows with cleanup: users, companies, branches, fiscal years, tickets, documents, permissions, alerts, keys, schema provisioning, and fallback replay.
7. Exercise Redis, email/SMS/webhooks, external storage, audit archival/replay, and background services in disposable infrastructure.
8. Record response schema validation, database side effects, audit entries, latency, and cleanup result per operation.

Until those steps pass, describe the current state as “build and route/auth smoke verified,” not “all APIs fully passed.”
