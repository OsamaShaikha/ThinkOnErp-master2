# HR Module — Clean Architecture Refactor

Status: **Employees pilot done** (Employees + Salary Components). Remaining modules use the legacy service style until migrated.

## 1. Where HR stood (overview)

| Layer | Before | Problem |
|---|---|---|
| API (23 controllers) | Call `I*Service` directly and build `ApiResponse` by hand | No MediatR, logic spread between controller and service, inconsistent status codes |
| Application services (47 files, ~10k lines) | `LeaveService` 2,113 lines, `EmployeeService` 695 | God classes; interfaces declared inside implementation files |
| Dependencies | 122 optional `= null` ctor params | Missing DI registrations only fail at runtime |
| Infrastructure | `HrAccountingHandoffService` 1,740 lines | Business rules living in Infrastructure |
| DTOs | `HrDtos.cs` 1,347 lines | Single file for every HR contract |

POS already follows the target shape (`Features/Pos/<Area>/Commands|Queries/<UseCase>/`), but its handlers are thin pass-throughs to big services. HR goes one step further: **handlers own the use-case logic**.

## 2. Target shape (per HR area)

```
Application/Features/Hr/<Area>/
├── Common/                      shared, area-private helpers (mappers, guards, writers)
├── Commands/<UseCase>/<UseCase>Command.cs   record + FluentValidation validator + handler
└── Queries/<UseCase>/<UseCase>Query.cs      record + handler
API/Controllers/Hr/<Area>Controller.cs        : HrControllerBase, IMediator only
```

Rules:
1. **Controller** = HTTP only: read route/body/claims → `_mediator.Send(...)` → `FromResponse(...)`. No logic, no services.
2. **Request records** are immutable and carry the acting `Username`.
3. **Validators** check request *shape* (required, lengths). Business invariants stay in the domain rules / handler.
4. **Handlers** take only required dependencies (no `= null`). One use case per handler.
5. **Business-rule violations throw** (`KeyNotFoundException` → 404, `ArgumentException` → 400, `InvalidOperationException` → 400/409, `UnauthorizedAccessException` → 403) and are mapped by `ExceptionHandlingMiddleware`. This is deliberate: the payroll `IHrUnitOfWork` relies on exceptions to roll back.
6. **Expected "not found" on reads** returns `ApiResponse.CreateFailure(..., 404)`.
7. Requests named `*Command` are audited by `AuditLoggingBehavior`; read-only operations must be named `*Query`.

## 3. Employees pilot — what changed

| File | Change |
|---|---|
| `Features/Hr/Employees/**` (25 files) | New. 12 commands + 9 queries, each with its handler; logic moved verbatim from `EmployeeService` |
| `Features/Hr/Employees/Common/EmployeeProfileWriter.cs` | Single home for the payroll profile guard and profile invariants (used by 7 handlers) |
| `Features/Hr/Employees/Common/EmployeeFieldGuard.cs` | Replaces `EmployeeFieldAccess? = null`; `EmployeeFieldGuard.Trusted` makes "no field checks" explicit |
| `Features/Hr/Employees/Common/EmployeeMappings.cs` | Entity → DTO mapping only |
| `API/Controllers/Hr/HrControllerBase.cs` | New: `CurrentUsername`, `TryGetCurrentUserId`, `FromResponse` |
| `EmployeesController`, `SalaryComponentsController` | Thin MediatR adapters. **Routes, permissions, JSON shape and messages unchanged** |
| `Services/Hr/EmployeeService.cs` | 695 → 144 lines, `[Obsolete]` facade that only builds the handlers. **Not registered in DI** |
| `Application/DependencyInjection.cs` | Registers `EmployeeFieldGuard`; removes `IEmployeeService` |
| `Infrastructure/Services/AuditLogger.cs` | Request/response payloads are now masked (they were stored raw for every module) |
| `appsettings.json`, `AuditLoggingOptions.cs` | Added `nationalId`, `passportNumber`, `bankAccountNumber`, `bankIban`, `iban`, `sscNumber` to masked fields |
| `tests/.../EmployeeUserLinkControllerTests.cs` | Mocks `IMediator` instead of the service |
| `tests/.../HrAttendanceHttpTests.cs` | Test host registers MediatR + `EmployeeFieldGuard` |

Behaviour notes:
- Every check order, lock range, and `Func<Task<T>>` type passed to `IHrUnitOfWork` matches the old service, so the existing lock tests remain valid.
- Inner "employee not found" inside `AddDependent`/`AssignSalaryStructure` now throws `KeyNotFoundException` (404) instead of `InvalidOperationException` (400). This only applies on a concurrent delete, because the outer check already returned 404.
- `ValidateEmployeeImport` is a **query** (dry run), so it is not audited and the upload stream is never serialized. `ImportEmployeesCommand.Workbook` is `[JsonIgnore]`.

### Why the obsolete facade exists
9 unit-test files and 5 `HrSchemaTool` Oracle verification scripts construct `EmployeeService` directly. The facade keeps them compiling **and still exercising the real handler logic**. Its legacy constructor quirks live only there (`UnavailableHrUnitOfWork`, `PassThroughHrUnitOfWork`). Once those callers use the handlers, delete `EmployeeService.cs` and `LegacyHrUnitsOfWork.cs`.

## 4. Verify before merging

```bash
dotnet build ThinkOnErp.sln
dotnet test tests/ThinkOnErp.HR.Tests
dotnet test tests/ThinkOnErp.HR.Api.Tests
dotnet test tests/ThinkOnErp.API.Tests --filter EmployeeUserLink
```
Then smoke-test the frontend Employees screens: list, profile, edit, bank, identity, dependents, salary, import/export.

## 5. Next modules (suggested order)

| # | Area | Main source | Notes |
|---|---|---|---|
| 1 | ✅ Employees + Salary components | `EmployeeService` | Done |
| 2 | Organization / catalog | `OrganizationController` | Small, good second slice |
| 3 | Shifts & work calendars | `Shift*Service`, `WorkCalendarService` | Mostly CRUD |
| 4 | Attendance & corrections | `HrAttendanceService`, `AttendanceCorrectionService` | Keep calculation engines as domain services |
| 5 | Leaves | `LeaveService` (2,113 lines) | Split by use case: request, approve, cancel, accrual, year-close, cash-out |
| 6 | Loans & adjustments | `LoanDeductionService`, `PayrollAdjustmentService` | |
| 7 | Payroll | `PayrollService` + engines | Engines stay; orchestration moves to handlers |
| 8 | End of Service | `EndOfServiceService` | |
| 9 | Accounting handoff | `HrAccountingHandoffService` (Infrastructure) | Move rules to Application, keep outbox/IO in Infrastructure |

Each step also splits that area's DTOs out of `HrDtos.cs` into `DTOs/Hr/<Area>Dtos.cs`.
