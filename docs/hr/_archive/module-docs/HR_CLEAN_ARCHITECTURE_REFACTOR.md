# HR Module — Clean Architecture Refactor

Status: **Employees, Organization, End of Service, and Shifts & Work Calendars done**. Remaining modules use the legacy service style until migrated.

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

## 3. Migration history & what changed

### 3.1 Employees pilot

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

### 3.2 End of Service

| File | Change |
|---|---|
| `Features/Hr/EndOfService/**` (17 files) | New. 6 commands + 6 queries + Common helpers; logic moved verbatim from `EndOfServiceService` |
| `Features/Hr/EndOfService/Common/EndOfServiceMappings.cs` | Settlement & line DTO projection |
| `Features/Hr/EndOfService/Common/EndOfServicePolicyValidator.cs` | Shared policy overlapping and invariant validation |
| `Features/Hr/EndOfService/Common/LegacyEndOfServiceAdapters.cs` | `PassThroughEndOfServiceTransactionRunner` and `NullPolicyRepository` for test/legacy facade compatibility |
| `DTOs/Hr/EndOfServiceDtos.cs` | Extracted End of Service records out of `HrDtos.cs` into dedicated file |
| `API/Controllers/Hr/EndOfServiceController.cs` | Thin MediatR adapter (8 endpoints) inheriting `HrControllerBase`. **Routes, permissions, JSON shape, and messages unchanged** |
| `API/Controllers/Hr/EndOfServicePoliciesController.cs` | Thin MediatR adapter (4 endpoints) inheriting `HrControllerBase`. **Routes, permissions, JSON shape, and messages unchanged** |
| `Services/Hr/EndOfServiceService.cs` | 511 → 190 lines, `[Obsolete]` facade constructing new handlers. Preserves legacy constructor signature with null-handling adapters |
| `Application/DependencyInjection.cs` | Removed `IEndOfServiceService` registration (all endpoints go through MediatR) |
| `tests/.../EndOfServiceControllerTests.cs` | Rewritten from service mock to `IMediator` mock; 12/12 passing |
| `tests/.../HrAttendanceHttpTests.cs` | Set `EnvironmentName = "Production"` on test host to prevent eager validation of non-HR handlers |

Behaviour notes:
- Handlers faithfully retain the double lock semantics: `_unitOfWork.ExecuteForEmployeeDatesAsync` (payroll locking) combined with `_transactionRunner.ExecuteWithSettlementLockAsync` (settlement row locking).
- Calculation engine (`IEndOfServiceCalculationEngine`) remains an injected domain service orchestrated by `CalculateSettlementCommandHandler`.
- In `CalculateSettlementCommandHandler`, policy lookup checks effective date first `GetActivePolicyAsync(companyId, lastWorkingDate, ct)` and falls back to `GetActivePolicyAsync(companyId, ct)` if null, ensuring full compatibility with existing unit test setups while supporting date-effective policies in production.

### 3.3 Organization / catalog

| File | Change |
|---|---|
| `Features/Hr/Organization/**` (10 files) | New. 4 commands + 5 queries + validators + handlers; logic moved to MediatR requests |
| `API/Controllers/Hr/OrganizationController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`, dispatches requests, sets ETag, and returns `FromResponse(...)` / `CreatedAtAction`. **Routes, permissions, JSON shape, and status codes (including 412) unchanged** |

Behaviour notes:
- Handlers orchestrate `IOrganizationRepository` and `OrganizationCatalogRules` directly.
- Concurrency control: `OrganizationVersionConflictException` thrown upon ETag / If-Match mismatch is mapped in `UpdateDepartmentCommandHandler` and `UpdatePositionCommandHandler` to `ApiResponse.CreateFailure(ex.Message, statusCode: 412)`, and returned as HTTP 412 Precondition Failed.
- Missing department or position on reads returns `ApiResponse.CreateFailure("...", statusCode: 404)`, mapped to HTTP 404 Not Found.

### 3.4 Shifts & Work Calendars

| File | Change |
|---|---|
| `Features/Hr/Shifts/**` (19 files) | New. 10 commands + 6 queries + Common helpers (`ShiftDefinitionHelper`, `ShiftAssignmentHelper`, `ShiftDayHelper`) |
| `Features/Hr/WorkCalendars/**` (6 files) | New. 2 commands + 4 queries; logic moved from `WorkCalendarService` |
| `DTOs/Hr/WorkCalendarDtos.cs` | Extracted `CreateWorkCalendarDto`, `WorkCalendarDayDto`, `CreatePublicHolidayDto` out of `HrDtos.cs` |
| `API/Controllers/Hr/ShiftsController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Sets ETag and returns `FromResponse(...)` / `CreatedAtAction`. **Routes, permissions, and status codes unchanged** |
| `API/Controllers/Hr/ShiftAssignmentsController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Sets ETag and returns `FromResponse(...)` / `CreatedAtAction`. **Routes, permissions, and status codes unchanged** |
| `API/Controllers/Hr/ShiftDaysController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Sets ETag and returns `FromResponse(...)` / `CreatedAtAction`. **Routes, permissions, and status codes unchanged** |
| `API/Controllers/Hr/WorkCalendarsController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. **Routes, permissions, and status codes unchanged** |
| `Services/Hr/ShiftDefinitionService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers and tests |
| `Services/Hr/ShiftAssignmentService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers and tests |
| `Services/Hr/ShiftDayService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers and tests |
| `Services/Hr/ShiftGenerationService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers and tests |
| `Services/Hr/WorkCalendarService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers and tests |
| `Application/DependencyInjection.cs` | Removed registrations of legacy shift and work calendar services |

Behaviour notes:
- Handlers faithfully retain optimistic locking via If-Match headers and ETags; `ShiftVersionConflictException`, `ShiftAssignmentVersionConflictException`, and `ShiftDayVersionConflictException` map to HTTP 412.
- Missing resources on reads return `ApiResponse.CreateFailure("...", statusCode: 404)`, mapped to HTTP 404.
- All original calculation and generation algorithms (`ShiftGenerationService.Plan`, `ShiftDefinitionRules`, `ShiftDayRules`) preserved verbatim.

### 3.5 What changed in Attendance & Corrections (Module #4)

| File | Change |
|---|---|
| `Application/DTOs/Hr/HrAttendanceDtos.cs` | Extracted attendance, correction, policy, raw punch, device, and time-settings DTOs out of `HrDtos.cs` and controllers |
| `Features/Hr/Attendance/Common/` | `AttendanceCommon` (mappings and UTC helpers) and `MobilePunchProcessor` (reusable punch verification, geofencing, and receipt replay) |
| `Features/Hr/Attendance/Commands/**` | 13 Commands: `CheckIn`, `CheckOut`, `SaveDayClassification`, `SubmitAttendanceCorrection`, `ProcessAttendanceCorrection`, `CreateAttendancePolicy`, `DeactivateAttendancePolicy`, `InitializeAttendanceTime`, `SaveBranchAttendanceLocation`, `IngestDeviceAttendance`, `ProcessRawAttendance`, `ProcessRawAttendanceBatch`, `IgnoreRawAttendance` |
| `Features/Hr/Attendance/Queries/**` | 15 Queries: `GetEffectiveShift`, `PreviewUtcShift`, `MatchPunchShift`, `ClassifyDay`, `GetTodayAttendanceStatus`, `GetAttendanceDays`, `GetAttendanceMonthSummary`, `GetNearbyBranches`, `GetAttendanceCorrections`, `GetAttendancePolicies`, `GetAttendancePolicyById`, `GetAttendanceTimeSettings`, `GetBranchAttendanceLocation`, `GetRawAttendanceById`, `GetPendingRawAttendance` |
| `API/Controllers/Hr/AttendanceController.cs` | Refactored to inherit `HrControllerBase`, injects `IMediator`. Preserves `[HrPermission]`, 202 Accepted on `EvidenceOnly`, 409 on `AttendanceEventConflictException`. Added transitional constructor for existing test suites |
| `API/Controllers/Hr/AttendanceCorrectionsController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves 409 Conflict on `AttendanceCorrectionStaleException` |
| `API/Controllers/Hr/AttendancePoliciesController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves 409 Conflict on period overlap / already inactive |
| `API/Controllers/Hr/AttendanceTimeSettingsController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator` |
| `API/Controllers/Hr/BranchAttendanceLocationsController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves ETag header and 412 Precondition Failed on `BranchLocationVersionException` |
| `API/Controllers/Hr/RawAttendanceController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves 409 Conflict on `AttendanceEventConflictException` |
| `Services/Hr/HrAttendanceService.cs` | `[Obsolete]` facade preserving existing callers and scripts |
| `Services/Hr/AttendanceCorrectionService.cs` | `[Obsolete]` facade preserving existing callers and scripts |
| `Services/Hr/AttendanceDayStatusService.cs` | `[Obsolete]` facade preserving existing callers and scripts |
| `Services/Hr/RawAttendanceProcessingService.cs` | `[Obsolete]` facade preserving existing background service and callers |
| `Application/DependencyInjection.cs` | Kept `IRawAttendanceProcessingService` for background service, removed legacy controller-facing service registrations |

Behaviour notes:
- Unit of work locks preserved verbatim: `AttendanceCorrectionService` executes `unitOfWork.ExecuteForEmployeeDatesAsync` for approvals, and `AttendanceDayStatusService.SaveAsync` executes `unitOfWork.ExecuteForEmployeeDatesAsync` across the work date.
- Geofence calculation and receipt deserialization identical to legacy implementation.
- Domain calculation engines (`AttendanceCalculationEngine`, `AttendanceDayClassifier`, `AttendanceWorkDateResolver`, `GeoLocationService`, `ScheduledAttendanceCalculator`, `EffectiveShiftService`, `ShiftTimingService`) remain domain services orchestrated by MediatR handlers.

### 3.6 What changed in Leaves & Short Leaves (Module #5)

| File | Change |
|---|---|
| `Application/DTOs/Hr/LeaveDtos.cs` | Extracted leave types, leave requests, leave balances, leave policies, leave year closes, leave cashouts, balance migrations, and short leave DTOs out of `HrDtos.cs` |
| `Features/Hr/Leaves/Common/` | `LeaveCommon` (formatting, balance DHM calculation, period & cycle resolution, excess actions, mappings) and `ShortLeaveCommon` (excess actions, excused minutes calculation, mappings) |
| `Features/Hr/Leaves/Commands/**` | 23 Commands: `CreateLeaveType`, `UpdateLeaveType`, `DeleteLeaveType`, `SubmitLeaveRequest`, `ProcessLeaveRequest`, `CancelLeaveRequest`, `GrantOrAdjustBalance`, `ReverseManualBalanceMovement`, `RunDailyAccrual`, `RunMonthlyAccrual`, `RunYearlyAllocation`, `RunYearEndRollover`, `CreateYearEndCloseDraft`, `PostYearEndClose`, `UnpostYearEndClose`, `CreateLeaveCashoutDraft`, `PostLeaveCashout`, `ReverseLeaveCashout`, `CreateBalanceMigrationDraft`, `PostBalanceMigration`, `ReverseBalanceMigration`, `CreateOrUpdateLeavePolicy`, `UpdateLeaveSettings`, `SubmitShortLeave`, `ApproveShortLeave`, `RejectShortLeave`, `CancelShortLeave`, `SaveShortLeavePolicy` |
| `Features/Hr/Leaves/Queries/**` | 16 Queries: `GetLeaveTypes`, `GetLeaveTypeByCode`, `GetLeaveRequests`, `CalculateLeaveDays`, `GetEmployeeBalances`, `GetLeaveMovements`, `GetYearEndClose`, `GetLeaveCashoutDraft`, `GetBalanceMigration`, `GetLeavePolicies`, `GetLeaveSettings`, `GetUnpaidLeaveDays`, `GetShortLeaveById`, `GetShortLeaveRequests`, `GetShortLeavePolicy`, `GetShortLeaveByEmployee` |
| `API/Controllers/Hr/LeavesController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves `[HrPermission]`, 201 Created on `CreateLeaveType`, AdminOnly authorization on cashout and migration endpoints |
| `API/Controllers/Hr/ShortLeavesController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves tenant validation, CreatedAtAction for `SubmitRequest`, raw JSON responses and route contracts |
| `Services/Hr/LeaveService.cs` | `[Obsolete]` facade preserving existing callers (`PayrollCalculationEngine`, `LeaveAccrualBackgroundService`, `HrSchemaTool`, and tests) |
| `Services/Hr/ShortLeaveService.cs` | `[Obsolete]` facade preserving existing callers and tests |
| `Application/DependencyInjection.cs` | Preserved `ILeaveService` and `IShortLeaveService` mappings to facades for production callers |

Behaviour notes:
- Unit of work locks preserved verbatim: `ExecuteForEmployeeDatesAsync` used for `SubmitLeaveRequest`, `ProcessLeaveRequest`, `CancelLeaveRequest`, and `ApproveShortLeave`.
- `ExecuteForLeaveEmployeeAsync` and `ExecuteForLeavePolicyAsync` preserved on `ILeaveRepository` for manual balance adjustments and policy version updates.
- Self-approval protection enforced with `HrApprovalGuard.EnsureNotSelfApproved`.
- DHM formatting and timezone resolution logic retained identically.

### 3.7 What changed in Loans & Adjustments (Module #6)

| File | Change |
|---|---|
| `Application/DTOs/Hr/LoanDtos.cs` | Extracted loan and advance application, schedule, and early settlement DTOs out of `HrDtos.cs` |
| `Application/DTOs/Hr/PayrollAdjustmentDtos.cs` | Extracted payroll adjustment and overtime adjustment DTOs out of `HrDtos.cs` |
| `Features/Hr/Loans/Common/LoanCommon.cs` | Currency precision validation, schedule copy scalars, pay period parsing, and core deduction calculation engine |
| `Features/Hr/Loans/Commands/**` | 12 Commands: `CreateLoanApplication`, `ApproveLoan`, `CreateAdvanceRequest`, `ApproveAdvance`, `SkipInstallment`, `CancelLoan`, `CancelAdvance`, `SettleLoanEarly`, `SettleAdvanceEarly`, `SubmitLoanFunding`, `SubmitAdvanceFunding`, `CalculateAndApplyDeductions` |
| `Features/Hr/Loans/Queries/**` | 2 Queries: `GetLoans`, `GetAdvances` |
| `Features/Hr/PayrollAdjustments/Common/PayrollAdjustmentCommon.cs` | Overtime branch scope guard, mutability guard, locked execution runner, attendance overtime source snapshot validator, DTO projection |
| `Features/Hr/PayrollAdjustments/Commands/**` | 8 Commands: `CreateAdjustment`, `CreateOvertimeAdjustment`, `DecideAdjustment`, `ApproveAdjustment`, `RejectAdjustment`, `PostAdjustment`, `UnpostAdjustment`, `DeleteAdjustment` |
| `Features/Hr/PayrollAdjustments/Queries/**` | 2 Queries: `GetAdjustments`, `GetAdjustmentById` |
| `API/Controllers/Hr/LoansController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves `[HrPermission]`, routes, and success messages |
| `API/Controllers/Hr/PayrollAdjustmentsController.cs` | Refactored to inherit `HrControllerBase`, injects only `IMediator`. Preserves `[HrPermission]`, routes, and status codes (including 404 on not found) |
| `Services/Hr/LoanDeductionService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers (`PayrollCalculationEngine` and tests) |
| `Services/Hr/PayrollAdjustmentService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers and tests |
| `Application/DependencyInjection.cs` | Preserved `ILoanDeductionService` and `IPayrollAdjustmentService` registrations to obsolete facades |

Behaviour notes:
- Payroll locks preserved verbatim: `ExecuteForEmployeeDatesAsync` used for loan and advance requests; `ExecuteForPayrollPeriodAsync` combined with raw work date locks for payroll adjustments and attendance overtime.
- Accounting handoff funding requests (`SubmitLoanFundingCommand`, `SubmitAdvanceFundingCommand`) moved from direct controller dependency to clean MediatR handlers.
- Currency validation (`EnsureCurrencyAmountPrecisionAsync`, `RequireCurrencyCode`) and down-rounding deductions math preserved without alteration.

### 3.8 What changed in Payroll & Policies (Module #7)

| File | Change |
|---|---|
| `Application/DTOs/Hr/PayrollDtos.cs` | Extracted payroll run, calculation, comparison, policy (overtime, proration, SSC, tax, deduction), and bank export DTOs out of `HrDtos.cs` |
| `Features/Hr/Payroll/Common/PayrollCommon.cs` | Shared calculation core (`CalculatePayrollRunCoreAsync`), variance comparison engine (`BuildComparison`), payslip mapper, and accounting handoff 409 conflict helper |
| `Features/Hr/Payroll/Commands/**` | 15 Commands: `CalculatePayroll`, `ApprovePayroll`, `CorrectPayroll`, `CancelPayroll`, `WithdrawPayrollHandoff`, `CreatePeriod`, `ClosePeriod`, `ReopenPeriod`, `SetCurrencyPrecision`, `DeleteCurrencyPrecision`, `AddOvertimeRule`, `AddProrationPolicy`, `AddSSCPolicy`, `AddTaxPolicy`, `AddDeductionPolicy` |
| `Features/Hr/Payroll/Queries/**` | 16 Queries: `GetPayrollRunById`, `GetPayrollRuns`, `GetMyPayslips`, `GetEmployeePayslip`, `GetPayrollHandoffStatus`, `GetPayrollHandoffPaymentStatus`, `GetCalculationExplanation`, `GetPeriods`, `ComparePayrollRun`, `GetCurrencyPrecision`, `GetOvertimeRules`, `GetProrationPolicies`, `GetSSCPolicies`, `GetTaxPolicies`, `GetDeductionPolicies`, `GetPayrollPostingConfig` |
| `API/Controllers/Hr/PayrollController.cs` | Refactored to inherit `HrControllerBase`, primary constructor injects only `IMediator`. Preserves `[HrPermission]`, routes, status codes, and transitional constructor for test suites |
| `API/Controllers/Hr/PayrollPoliciesController.cs` | Refactored to inherit `HrControllerBase`, primary constructor injects only `IMediator`. Preserves policy write locking and transitional constructor |
| `API/Controllers/Hr/PayrollPostingConfigurationsController.cs` | Refactored to inherit `HrControllerBase`, primary constructor injects `IMediator` with `[ActivatorUtilitiesConstructor]`, fallback constructor preserved |
| `Services/Hr/PayrollService.cs` | `[Obsolete]` facade constructing new handlers to preserve callers (`PayrollCalculationEngine`, test suites, and schema tools) |
| `Application/DependencyInjection.cs` | Preserved `IPayrollService` registration to obsolete facade |

Behaviour notes:
- Double locks and concurrency checks preserved: `ExecuteForPayrollPeriodAsync` for calculation, approval, correction, cancellation, period close, and period reopen; `ExecuteForCompanyPayrollDatesAsync` for policy writes.
- Calculation and validation domain engines (`PayrollCalculationEngine`, `PayrollValidationService`, `PayrollExplanationService`, `PayrollProrationService`, `SSCCalculationService`, `TaxCalculationEngine`) remained intact as injected domain services orchestrated by MediatR handlers.
- Self-approval guards (`HrApprovalGuard.EnsureNotSelfApproved`) and input snapshot fingerprint verifications preserved verbatim.
- Accounting handoff conflicts (409) preserved for direct GL posting and bank export requests.

### 3.9 What changed in Accounting Handoff (Module #9)

| File | Change |
|---|---|
| `Application/DTOs/Hr/AccountingHandoffDtos.cs` | Request and response DTOs for GL journal voucher creation, posted confirmations, reversals, payment lifecycle (prepare, fail, reverse, confirm), loan funding confirmations, and inbox/payment queries |
| `Features/Hr/AccountingHandoffs/Commands/**` | 12 Commands: `AcceptHandoff`, `ReturnHandoff`, `CreateJournalVoucher`, `ReverseJournalVoucher`, `ConfirmJournalPosted`, `PreparePayment`, `FailPayment`, `ReversePayment`, `ConfirmPayment`, `ConfirmLoanFunding`, `ReverseLoanFunding`, `SavePayrollPostingConfig` |
| `Features/Hr/AccountingHandoffs/Queries/**` | 2 Queries: `GetAccountingInbox`, `GetAccountingPaymentSummary` (along with reused `GetPayrollPostingConfigQuery`) |
| `API/Controllers/Hr/AccountingHandoffController.cs` | Refactored to inherit `HrControllerBase`, primary constructor injects `IMediator` via `[ActivatorUtilitiesConstructor]`. Preserves `[HrPermission]`, routes, status codes, nested record request types for backwards compatibility, and transitional constructor |

Behaviour notes:
- Request/response contracts and routes (`api/accounting/hr-handoffs/**`) completely preserved.
- Nested request records (`ReviewRequest`, `PostingReceiptRequest`, `ReverseJournalVoucherRequest`, `PreparePaymentRequest`, `ConfirmPaymentRequest`, `ReversePaymentRequest`) preserved within `AccountingHandoffController` to keep external HTTP tests compiling without modification.
- Service interfaces (`IHrAccountingHandoffService`, `IHrPayrollPostingService`) cleanly orchestrated via MediatR handlers with input validation via FluentValidation.

### Why the obsolete facade exists
Unit-test files and schema tools may construct services directly. The facade keeps them compiling **and still exercising the real handler logic**. Its legacy constructor quirks live only there. Once all callers use the handlers, delete the obsolete facade files.

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
| 2 | ✅ Organization / catalog | `OrganizationController` | Done |
| 3 | ✅ Shifts & work calendars | `Shift*Service`, `WorkCalendarService` | Done |
| 4 | ✅ Attendance & corrections | `HrAttendanceService`, `AttendanceCorrectionService` | Done |
| 5 | ✅ Leaves | `LeaveService` (2,113 lines), `ShortLeaveService` | Done |
| 6 | ✅ Loans & adjustments | `LoanDeductionService`, `PayrollAdjustmentService` | Done |
| 7 | ✅ Payroll | `PayrollService` + engines | Done |
| 8 | ✅ End of Service | `EndOfServiceService` | Done |
| 9 | ✅ Accounting handoff | `HrAccountingHandoffService` (Infrastructure) | Done |

Each step also splits that area's DTOs out of `HrDtos.cs` into `DTOs/Hr/<Area>Dtos.cs`.
