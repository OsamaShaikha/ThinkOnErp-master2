# Comprehensive Architecture & Implementation Report: Dynamic HR & Payroll Module
**ThinkOnErp - Dynamic HR & Payroll Engine Implementation**

---

## 1. Architectural Vision & Core Principles

The Dynamic HR & Payroll module has been implemented following enterprise-grade Clean Architecture and best industry ERP standards with **100% dynamic policy execution**:

1. **Zero Hardcoding**:
   - No statutory deduction rates, progressive tax brackets, social security wage ceilings, overtime multipliers, attendance grace periods, weekend/holiday schedules, or proration divisors are hard-coded in C#. Everything is resolved dynamically at runtime from database policies.
2. **Company-Level Scoping & Effective Dating**:
   - Every policy entity is scoped by `CompanyId` and contains `EffectiveFrom` and `EffectiveTo` date intervals. This allows companies to introduce regulatory policy changes without altering or corrupting past historical payroll runs.
3. **Automatic Deduction Capping & Carry-Forward**:
   - When total scheduled loan and advance installments exceed allowable net pay deduction limits (e.g. 33% of disposable income), the system automatically caps the deduction and defers the remaining unpaid balance (`CarriedForwardAmount`) to subsequent pay periods without halting payroll calculation.
4. **Dynamic Proration Methods**:
   - Proration calculations for mid-month hires and terminations dynamically adapt to the company's active policy:
     - **`CALENDAR_DAYS`**: Divides by the actual number of days in the specific month (28, 29, 30, 31).
     - **`FIXED_30`**: Commercial standard dividing by a fixed 30-day base.
     - **`WORKING_DAYS`**: Divides by the exact working days derived from the company's work calendar.
5. **Balanced Double-Entry GL Integration**:
   - Automatically generates balanced General Ledger Journal Vouchers (`GlVoucherHeader` & `GlVoucherDetail`) upon payroll posting, with support for payroll cancellation and automated reversing journal vouchers.
6. **Payroll Calculation Snapshot & Auditing**:
   - Stores an immutable audit snapshot (`PayrollCalculationSnapshot`) for every employee payslip line, preserving the exact rates, brackets, days, and factors applied during calculation.

---

## 2. Domain Entities

The following entities were added under `src/ThinkOnErp.Domain/Entities/Hr/`:

| Entity | Purpose & Responsibility |
| :--- | :--- |
| **`WorkCalendar`** | Defines multi-company working calendars, regular working days, custom weekends, and shift hours. |
| **`WorkCalendarDay`** | Detailed daily calendar records (Date, DayOfWeek, IsWorkingDay, IsHoliday, IsWeekend). |
| **`PublicHoliday`** | Public and company holidays with paid/unpaid flags and recurring indicators. |
| **`AttendancePolicy`** | Grace periods for late arrival/early leave, punch rounding rules, and missing punch penalties. |
| **`RawAttendance`** | Ingested biometric punch logs with SHA-256 deduplication hashing (`PunchHash`). |
| **`AttendanceDay`** | Daily calculated attendance (scheduled hours, actual worked hours, late minutes, early leave, overtime). |
| **`AttendanceCorrectionRequest`** | Missing punch correction requests with approval/rejection workflow. |
| **`OvertimeRule`** | Tiered overtime multiplier rules for regular workdays, weekends, and public holidays. |
| **`ProrationPolicy`** | Dynamic proration calculation policies (`CALENDAR_DAYS`, `FIXED_30`, `WORKING_DAYS`). |
| **`DeductionPolicy`** | Maximum deduction cap percentages, net pay guarantees, and auto-carry-forward flags. |
| **`TaxPolicy`** | Progressive tax rules: personal/dependent exemptions, national solidarity thresholds. |
| **`TaxBracket`** | Progressive income tax brackets (LowerLimit, UpperLimit, RatePercent). |
| **`SSCPolicy`** | Social security rules: employee/employer contribution rates, monthly ceiling cap, high-risk surcharge. |
| **`EmployeeLoan`** | Long-term and medium-term employee loans with status and schedule tracking. |
| **`LoanRepaymentSchedule`** | Monthly installment schedules: scheduled amount, paid amount, carried-forward amount, status. |
| **`EmployeeAdvance`** | Short-term salary advances targeting a specific deduction pay period. |
| **`PayrollPeriod`** | Monthly payroll periods (Open, Processing, Closed). |
| **`PayrollCalculationSnapshot`** | Audit snapshot capturing all resolved policy parameters for each employee payslip line. |

---

## 3. Infrastructure & Oracle EF Core Mapping

### 1. Entity Configurations:
Created 18 Fluent API configuration classes under `src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/` tailored for **Oracle Database**:
- Identity sequences and primary key generation.
- Precision mappings for monetary amounts `(18, 3)` and percentage rates `(5, 4)`.
- Foreign key constraints with delete behaviors (`Restrict` / `Cascade`).
- Indexes and unique constraints preventing duplicate periods, punches, or policy overlaps.

### 2. Repositories:
Implemented and registered the following repositories via Dependency Injection:
- `WorkCalendarRepository` & `IWorkCalendarRepository`
- `AttendanceCorrectionRepository` & `IAttendanceCorrectionRepository`
- `PolicyRepository` & `IPolicyRepository`
- `LoanRepository` & `ILoanRepository`
- `PayrollPeriodRepository` & `IPayrollPeriodRepository`

---

## 4. Application Calculation Engines & Services

Implemented under `src/ThinkOnErp.Application/Services/Hr/`:

### 1. `WorkCalendarService`
- Generates annual calendar days dynamically based on company working day definitions.
- Evaluates working days, weekends, and public holidays for any given date.
- Calculates exact working day counts between dates for the proration engine.

### 2. `AttendanceCalculationEngine`
- Ingests raw biometric punches with SHA-256 hash deduplication.
- Matches punches against employee shift schedules.
- Handles overnight shifts across midnight boundaries.
- Computes late arrival and early departure after deducting policy grace periods.
- Calculates actual worked hours and approved overtime hours.

### 3. `AttendanceCorrectionService`
- Manages missing punch correction requests and approvals.
- Automatically updates daily attendance summaries upon approval.

### 4. `OvertimeCalculationService`
- Resolves active overtime rule multipliers for regular, weekend, and holiday work.
- Derives hourly employee wage from base salary and standard working hours.
- Computes total overtime monetary earnings.

### 5. `PayrollProrationService`
- Evaluates mid-month hires and mid-month terminations.
- Dynamically resolves active company proration policy (`CALENDAR_DAYS`, `FIXED_30`, `WORKING_DAYS`) and computes exact proration factors.

### 6. `SSCCalculationService`
- Resolves statutory and company-configured social security contribution rates.
- Enforces maximum salary ceiling caps.
- Adds high-risk role surcharges (+1% to employer share) when applicable.

### 7. `TaxCalculationEngine`
- Applies personal and dependent tax exemptions.
- Computes progressive income tax across all active tax brackets.
- Computes national solidarity contributions (1%) for ultra-high income brackets.

### 8. `LoanDeductionService`
- Automates loan installment schedule generation and short-term advances.
- **Enforces deduction capping & carry-forward**: If installments exceed disposable salary capacity, caps the deduction, updates the schedule status to `PARTIAL`, and carries forward the unpaid balance to the next installment.

### 9. `PayrollCalculationEngine`
- Orchestrates the full payroll calculation pipeline:
  1. Base salary & recurring allowances.
  2. Proration factor application.
  3. Dynamic overtime earnings additions.
  4. Unpaid leave & absence deductions.
  5. Gross salary aggregation.
  6. Social security employee/employer deductions.
  7. Progressive income tax & national contribution withholding.
  8. Loan/advance deductions with auto-capping and carry-forward.
  9. Net pay calculation and `PayrollCalculationSnapshot` generation.

### 10. `PayrollValidationService`
- Pre-calculation and pre-approval validations:
  - Detects missing salary structures or negative net pay.
  - Flags unapproved attendance corrections and missing punches.
  - Verifies existence of active tax and SSC policies.

### 11. `PayrollExplanationService`
- Generates a transparent, step-by-step breakdown explaining how an employee's net pay was derived.

### 12. `PayrollService`
- Manages the payroll lifecycle: `DRAFT` ➔ `CALCULATED` ➔ `APPROVED` ➔ `POSTED_TO_GL` ➔ `PAID` / `CANCELLED`.
- **GL Posting**: Generates balanced double-entry General Ledger Journal Vouchers (Debiting Salary/SSC Expense accounts, Crediting Tax/SSC/Loan/Payable accounts).
- **Payroll Reversal**: Generates balanced reversing journal vouchers upon run cancellation.

---

## 5. REST API Endpoints

Implemented in `src/ThinkOnErp.API/Controllers/Hr/`:

### 1. Work Calendars & Holidays (`WorkCalendarsController`):
- `GET /api/v1/hr/work-calendars` - List company calendars.
- `POST /api/v1/hr/work-calendars` - Create a work calendar.
- `POST /api/v1/hr/work-calendars/{id}/generate-days` - Generate full year calendar days.
- `GET /api/v1/hr/work-calendars/holidays` - List public holidays.
- `POST /api/v1/hr/work-calendars/holidays` - Add a public holiday.

### 2. Attendance & Corrections (`AttendanceCorrectionsController`):
- `GET /api/v1/hr/attendance-corrections` - List correction requests.
- `POST /api/v1/hr/attendance-corrections` - Submit a missing punch correction request.
- `POST /api/v1/hr/attendance-corrections/{id}/approve` - Approve a correction request.
- `POST /api/v1/hr/attendance-corrections/{id}/reject` - Reject a correction request.
- `GET /api/v1/hr/attendance-corrections/daily-summary` - View processed daily attendance.

### 3. Dynamic Policies (`PayrollPoliciesController`):
- CRUD operations for `attendance-policy`, `overtime-rules`, `proration-policy`, `deduction-policy`, `tax-policy`, and `ssc-policy`.

### 4. Loans & Advances (`LoansController`):
- `GET /api/v1/hr/loans` - List employee loans and schedules.
- `POST /api/v1/hr/loans` - Create loan application with installment generation.
- `POST /api/v1/hr/loans/{id}/approve` - Approve loan.
- `GET /api/v1/hr/loans/advances` - List salary advances.
- `POST /api/v1/hr/loans/advances` - Request salary advance.
- `POST /api/v1/hr/loans/advances/{id}/approve` - Approve advance.

### 5. Payroll Runs & Payslips (`PayrollController`):
- `POST /api/v1/hr/payroll/runs` - Create a new payroll run.
- `POST /api/v1/hr/payroll/runs/{id}/calculate` - Execute dynamic calculation engine for all employees.
- `POST /api/v1/hr/payroll/runs/{id}/approve` - Approve payroll run.
- `POST /api/v1/hr/payroll/runs/{id}/post-gl` - Post balanced journal voucher to General Ledger.
- `POST /api/v1/hr/payroll/runs/{id}/cancel` - Cancel run and rollback GL vouchers.
- `GET /api/v1/hr/payroll/runs/{id}/validate` - Pre-approval verification check.
- `GET /api/v1/hr/payroll/explanation` - Net pay calculation explanation breakdown.
- `GET /api/v1/hr/payroll/payslips` - Employee payslip retrieval.

---

## 6. Automated Unit Testing & Verification

### 1. Build Verification:
- Compiled via `dotnet build src/ThinkOnErp.API/ThinkOnErp.API.csproj` — **0 Errors, Build Succeeded**.

### 2. Unit Test Results:
Executed `dotnet test tests/ThinkOnErp.HR.Tests/ThinkOnErp.HR.Tests.csproj`:
- **Total Tests**: 27
- **Passed**: 27 (**100% Pass Rate**)
- **Failed**: 0
- **Skipped**: 0

```text
Passed!  - Failed: 0, Passed: 27, Skipped: 0, Total: 27, Duration: 250 ms - ThinkOnErp.HR.Tests.dll (net8.0)
```

---

## 7. Summary

The Dynamic HR & Payroll module is now **multi-tenant enterprise-ready**, completely decoupled from hardcoded business constraints, and fully prepared for production use and front-end integration.
