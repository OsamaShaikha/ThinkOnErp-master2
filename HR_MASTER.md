# ThinkOnErp — HR Master File

> **The only HR document.** It replaces `HR_ROADMAP_AR.md`, the 51 files in `docs/hr/`, the 7 `docs/HR_API_REFERENCE_*.md` snapshots, `docs/HR_MODULE_STATUS`, `docs/HR_CLEAN_ARCHITECTURE_REFACTOR.md`, `docs/modules/05_HR_Payroll/*.md` and the fix-estimate note. Those files were moved to `docs/hr/_archive/` (nothing was deleted). The full endpoint guide is the companion report `ThinkOnErp_HR_API_Workflow_Guide.html`.
>
> **As of:** 16 Nov 2026 · Source: current code in `src/` (uncommitted working tree), roadmap state of 16 Nov, archived docs.

---

## 1. How to work with this file

These rules replace the old roadmap rules and are referenced from `AGENTS.md`.

1. Read this file before picking the next HR task. It is the source of truth for scope, decisions, task IDs, status and gates.
2. Every batch names the task IDs it covers. Before finishing, update the task rows in **§6**, the snapshot in **§2**, and add one line to the change log in **§15**. No long narrative paragraphs.
3. The task list is **frozen at 142 IDs**. A new gap is recorded under an existing ID (or under the BA findings in **§7**), not as a new ID, unless the owner asks.
4. Mark a task done only for its stated scope. Keep these separate and never infer one from another: code done · unit tests · HTTP tests · Oracle/concurrency check · schema applied to `DEV_TEMPLATE` · tenant sync · production.
5. Settled decisions (**§3**) are not reopened. Behaviour stays company-configurable; no fixed country rules inside the product.
6. Standing owner instructions: **do not run HR tests until the owner asks**; **no action on `TEST` or `THINKONERP_DEV02`**; any DDL needs a fresh Data Pump backup first; no automatic tenant sync.

---

## 2. Snapshot

| Item | State |
|---|---|
| Task map | **86 / 142 done (60.6%)** · 56 open = 50 backend/DB/acceptance + 6 UI (out of scope) |
| API surface | **208 endpoints in 23 controllers** (+ `HrControllerBase`), all `[TenantScoped]` + `[Authorize]` + `[HrPermission]` |
| Architecture | Clean Architecture done for all 9 HR areas (MediatR CQRS, thin controllers). Old service classes kept as obsolete facades. |
| Tenancy | Schema per company (owner decision 15 Nov). `COMPANY_ID` removed from 18 mapped HR tables (migration `20261115002400`). |
| Database | 41 tenant migrations applied to `DEV_TEMPLATE` after backup `20261115205428`. Migrations `20261115002200` (policy effective dates) and `20261115002300` (unique period code) **written, not applied**. `20261115002100` applied but not registered in history. |
| Build | API compiles with 0 errors (≈32 existing warnings). `ThinkOnErp.API.Tests` and `ThinkOnErp.Infrastructure.Tests` do **not** compile (old references). |
| Tests | Last full run 13 Nov: HR.Tests 2,730 + HR.Api.Tests 1,271 = **4,001/4,001 passed**. Frozen since then by owner; code has changed a lot since that run. |
| Tenant clients | No client schema synced. Bulk sync API exists but `TenantProvisioning:AllowExistingTenantSchemaSync=false`. |
| Front-end | Not in this repo. 6 UI tasks out of scope. |
| Production | Not deployed. |

**Current batch:** `EMP-02-ACCEPT / EMP-05 / LEV-12 / ATT-04 / ATT-08 / PAY-01 / PAY-03 / PAY-10 / PAY-11 / EOS-03 / OPS-16` + BA findings F1–F12.
**Next in this batch:** inventory every reader/writer of the text `PayPeriod` (`PayrollRun`, `LoanRepaymentSchedule`, `EmployeeAdvance`, `LeaveCashoutSettlement`), document the company guard per path, then decide FK vs `PayrollPeriodId` after checking historical data.

---

## 3. Settled decisions (do not reopen)

| Topic | Decision |
|---|---|
| Multi-tenancy | Same isolation as ERP: one Oracle schema per company; branch permissions respected. No `COMPANY_ID` in tenant-owned HR tables. Central `SYS_COMPANY/SYS_BRANCH/SYS_USERS` keep their `COMPANY_ID`. |
| Product flexibility | Company settings drive policies and values. No country rules or legal limits hard-coded. |
| Employee & manager | Use existing roles and permissions. Manager = job relationship + permissions; no forced new HR role. |
| Settings ownership | Admin, or whoever is granted the screen/feature. |
| Shifts | Per customer; no default shift. Fixed, variable and multiple shifts supported. |
| Shift priority | Daily roster > temporary assignment > base assignment. Two overlapping temporary shifts are rejected. |
| Roster | Direct daily assignment, plus generation from base/temporary with manual exceptions kept. |
| Night shift | Belongs to the date it starts. |
| Mobile punch | Check-in/out by GPS distance to branch, within employee permissions and scope. |
| Attendance & absence | Online presence counts. Required shift with no punch and no valid exception = absence. |
| Missing punch | Correction request applied after approval; trace kept; day recalculated. |
| Leave types | Annual / Sick / Unpaid are known codes; types are configured per company and can be added or disabled. |
| Leave entitlement | Company chooses annual quota, eligibility (from hire or after a period), and up-front grant or yearly/monthly/daily accrual. |
| Daily accrual | Calendar days of the leave year (365/366), not working days. |
| Balance display | Decimal or days/hours/minutes; workday hours from company setting (no 8 h assumption). |
| Leave year | Company chooses the start (e.g. September). |
| Year-end close | Per type: no carry, full carry, capped carry, must-use, or cash-out of excess. |
| Movements | Draft/unposted are editable; posted are final. Reversal respects dependants, permissions and audit. |
| Payroll ↔ Accounting (INT-01) | HR calculates, reviews, approves a frozen version and hands off a separate document. Accounting reviews, creates/posts the JV, pays and returns receipts. **HR never creates or reverses GL entries or payment vouchers.** Old direct HR posting endpoints return 409. |
| Loans / advances | Stay `APPROVED` in HR until Accounting confirms funding. Installment settlement happens with the payroll posting receipt, with a reservation that prevents use in two payrolls. |
| Short leave | Late arrival, mid-day exit, early departure. Different from leave and from termination. |
| End of service | Resignation, contract expiry, termination, retirement, death, job abandonment; settles all rights and open items. **Termination happens only through EOS.** |
| Overtime | Calculated from attendance + shift + company rules (defaults 1.25× normal, 1.50× weekend, 2.00× public holiday). Never auto-added to payroll; explicit approval needed (`PAY-17`). |

---

## 4. Architecture in one page

- **Layers:** `Domain/Entities/Hr` → `Application/Features/Hr/<Area>/{Commands,Queries}` (MediatR handlers) → `API/Controllers/Hr` (thin, inherit `HrControllerBase`, map `ApiResponse` to HTTP) → `Infrastructure` (EF Core Oracle, repositories, `HrUnitOfWork`, handoff service, background workers).
- **Areas (9):** Employees + salary components · Organization · Shifts & work calendars · Attendance & corrections · Leaves & short leaves · Loans & adjustments · Payroll & policies · End of Service · Accounting handoff.
- **Request pipeline:** JWT → tenant middleware checks company/schema against `THINKON_ERP.SYS_COMPANY` and sets `CURRENT_SCHEMA` → `HrAccessFilter` (screen/feature permission, branch grants, self-service scope, field-level access, validates `companyId`/`branchId`/`employeeCode` arguments) → handler.
- **Data scope:** EF global query filters by branch and self-service employee on every HR table.
- **Safety patterns:** payroll period locks (`HrUnitOfWork.ExecuteFor…`), input fingerprints (recalculate if inputs changed), idempotency keys, `If-Match` versions, concurrency tokens, leave ledger with reversals, outbox/inbox for Accounting.
- **Schema management:** EF tenant migrations **plus** `scripts/HrSchemaTool` (installers, verifiers, audits; see its README). 13+ HR tables are `ExcludeFromMigrations()` and owned by the tool.
- **Background workers:** leave accrual worker, handoff outbox relay (iterates active tenant schemas, resets `CURRENT_SCHEMA`), Prometheus metrics.

---

## 5. Module status

| Area | Endpoints | What works (code) | Still open |
|---|---|---|---|
| Employees & salary | 21 (19 + 2 components) | Profile CRUD in sections (work, bank, identity), field-level security, dependents, effective-dated salary structures, Excel template/validate/import/export with impact preview, user link, my team. Termination blocked outside EOS. | Acceptance on Oracle (`EMP-02/14/15-ACCEPT`), user-link HTTP on two schemas (`EMP-03`), salary component update/deactivate, dependent update. |
| Organization | 9 | Departments, positions, manager, cycle check, integrity report endpoint. | No DB FKs, headcount not enforced, no deactivate/delete endpoint. |
| Shifts & calendars | 22 | Shift definitions, base/temporary assignments, daily roster + generation, effective shift, UTC/DST timing, work calendars, holidays, working-day count. | Calendar/holiday update/delete; calendar not assigned per branch/employee. |
| Attendance | 29 | Mobile check-in/out with geofence + idempotent EventId, device raw events queue, day classification, multi-session days, corrections with approval, branch location, time-zone setting, **attendance policies (new)**, **timesheet + month summary (new)**. | Branch daily report (needs definition), Oracle/HTTP acceptance (`ATT-08`). |
| Leaves | 42 (34 + 8 short) | Types, policies (effective-dated), settings, requests with **server-calculated days** and **overlap check**, ledger, manual grants, accruals (daily/monthly/yearly + worker), year-end close draft/post/unpost, cash-out, balance migration, short leaves with quota and payroll effect. | Oracle acceptance for most LEV items; OPS-12 triggers block manual ledger testing. |
| Payroll | 43 (21 + 9 adj. + 13 policies) | Periods (open/close/reopen, DTO), calculate, explain, compare, approve (no self-approval), correction runs, cancel, **run list + payslip JSON + my payslips (new)**, bank export, adjustments (pending/approve/post/unpost), overtime adjustments, SSC/tax/proration/overtime/deduction/precision policies. | Payslip PDF, policy update/end-date, FORMULA components (deferred), Oracle payment lifecycle. |
| Loans & advances | 13 | Apply, approve (no self-approval), funding request, skip installment, cancel, early settlement, currency precision. | Reject action, GET by id, eligibility rules. |
| End of Service | 12 (8 + 4 policies) | Calculate draft, approve (employee → TERMINATED), cancel (restores ACTIVE), payout handoff, **EOS policies (new)**. | Oracle acceptance. |
| Accounting handoff | 15 (+2 legacy) | Inbox, accept/return, JV create/reverse, posting receipt, payments prepare/confirm/fail/reverse, loan funding receipt/reverse, branch posting config. | Oracle end-to-end payment cycle, bank file spec (deferred), multi-currency batches (deferred). |

---

## 6. Task map (142 IDs, frozen)

### 6.1 Done — 86

| Area | Done IDs |
|---|---|
| Base & docs | BASE-00, BASE-01, BASE-02, BASE-03, BASE-05, DOC-01 |
| Employees | EMP-01, EMP-02-IMPL, EMP-04, EMP-05, EMP-06, EMP-07, EMP-08, EMP-09, EMP-10, EMP-11, EMP-12, EMP-13, EMP-14-IMPL, EMP-15-IMPL |
| Settings | SET-01 |
| Shifts | SHIFT-01 … SHIFT-06 |
| Attendance | ATT-00 … ATT-07, ATT-09, ATT-10 |
| Leaves | LEV-01 … LEV-10, LEV-13, LEV-19 |
| Short leave | PERM-01 … PERM-05 |
| Payroll | PAY-01 … PAY-07, PAY-10 … PAY-16, PAY-18, PAY-21-BASE-IMPL |
| ERP / bank | ERP-01, ERP-02, ERP-03 |
| Integration | INT-01 |
| End of Service | EOS-01 … EOS-06 |
| Operations | OPS-00, OPS-01, OPS-02, OPS-04, OPS-06, OPS-13 |

### 6.2 Open — 56

Legend: **Code** = code state · **Remaining** = what closes it · 🚫 = blocked by an external input (see §10).

| ID | Title | Code | Remaining |
|---|---|---|---|
| BASE-04 | Isolation matrix | Partial | Prove real SQL + `CURRENT_SCHEMA` + HTTP read/write across two isolated Oracle schemas. |
| EMP-02-ACCEPT | Org hierarchy upgrade acceptance | Done in code | Apply migration, HTTP/permission/concurrency on two restorable schemas; run integrity endpoint. Depends on OPS-02/03/11/14/16. |
| EMP-03 | Employee ↔ login link | Partial | Oracle HTTP through tenant registry on two schemas. |
| EMP-14-ACCEPT | Dependent schema acceptance | Done in code | Migration on isolated schema + final tests. |
| EMP-15-ACCEPT | Salary line validation acceptance | Done in code | Unit/HTTP/Oracle in final HR test cycle. |
| SET-02…SET-06 | Settings UI pages (org, attendance, leave, payroll, ERP/bank) | UI | Out of scope (no front-end repo). |
| SET-07 | Effective-dated settings | Partial | Historical effective-date checks on Oracle. |
| SHIFT-07 | Shift & roster UI | UI | Out of scope. |
| ATT-08 | Real mobile & correction test | Partial | Oracle/tenant acceptance; branch daily report (F9). |
| LEV-11 | Automatic accrual | Partial | Run worker on a real tenant; monitoring (OPS-08). |
| LEV-12 | Consistent available balance | Partial | Tests after F3/F4/F5 changes; Oracle. |
| LEV-14 | Cancel request & restore balance | Partial | Oracle concurrency; tenant sync. |
| LEV-15 | Policy effective dates | Code done | Oracle concurrency of full flow; tenant sync. |
| LEV-16 | Migrate legacy balances | Code done | Per-client manual review + run (template in archive). |
| LEV-17 | Leave cash-out | Partial | Concurrency acceptance; user tests. |
| LEV-18 | Year-end close audit | Partial | 🚫 OPS-12 first, then Oracle DML check; cash-out with real approved payroll. |
| LEV-20 | Correct cash-out consumed by payroll | Partial | Oracle on live data. |
| PAY-08 | Review every payroll input writer | Partial | Live verification; recalc old CALCULATED runs after deploy. |
| PAY-09 | Undo financial approval (correction) | Partial | Full correction cycle with approved GL setup on isolated Oracle. |
| PAY-17 | Approve overtime into payroll | Partial | Oracle payment lifecycle (needs open GL period). |
| PAY-19 | Loan installment precision | Partial | Final tests. |
| PAY-20 | Loan/advance currency = payroll currency | Partial | Run audit on authorised DB; migration acceptance. |
| PAY-21-FORMULA | FORMULA salary components | Not started | 🚫 Deferred by owner (15 Nov). Draft contract in archive. |
| ERP-04 | Audit accounting posting | Partial | Owned by Accounting via INT-06/14. |
| ERP-05 | Reversal & no duplicate JV | Partial | Owned by Accounting via INT-10/11/14. |
| ERP-06 | Real bank file | Partial | 🚫 Temporarily deferred by owner; needs Accounting bank spec. |
| ERP-07 | Multi-currency payment batches | Partial | 🚫 Deferred by owner; needs Accounting decisions. |
| ERP-08 | Payment tracking | Backend done | Financial acceptance, Oracle multi-session. |
| ERP-09 | Legacy payment snapshots | Partial | Review old APPROVED run without handoff + old JV link. |
| INT-02 … INT-14 | Handoff document, snapshot, submit/withdraw, inbox, JV, PV, loan funding, installment reservation, outbox/inbox, reversal, permissions, legacy migration, end-to-end acceptance | Partial (code largely done) | Oracle atomicity/concurrency on two schemas, live payment cycle, client sync, UI contract. |
| OPS-03 | Pilot client sync | Code guard + plan | No sync done; needs OPS-11/12/15 decisions. |
| OPS-05 | HTTP permission tests | Partial | Operations/branches matrix on Oracle two schemas. |
| OPS-07 | Full foundation scenario | Partial | Approved financial setup + restorable schema (no open GL period found). |
| OPS-08 | Background jobs & monitoring | Partial | Docker daemon / promtool; run stack. |
| OPS-10 | Pilot client acceptance | Not started | After everything above. |
| OPS-11 | Restore drill | Partial | 🚫 Needs a dedicated restorable PDB. |
| OPS-12 | Cross-schema triggers on `HR_LEAVE_MOVEMENT` | Draft script | 🚫 Owner review of 3 external trigger owners. |
| OPS-14 | Column length alignment | DEV_TEMPLATE done | Tenant follow-up. |
| OPS-15 | Ownership of 23 unmapped HR tables | Inventory | 🚫 Business owner decision per table. |
| OPS-16 | Tenant schema upgrade source & model match | Partial | Update verifiers/fixtures that still expect `COMPANY_ID`; register `002100`; apply `002200/002300` after backup. |

### 6.3 Definition of "foundation release ready"

- [ ] Isolated pilot client with HR settings manageable through current permissions.
- [ ] Employee, hierarchy, effective shift and clear daily roster.
- [ ] Mobile punch, approved correction, attendance/absence tied to roster and time zone.
- [ ] Leave with automatic accrual, explainable usage/reversal, audited year-end close.
- [ ] Short leave tied to attendance with financial/balance effect per setting.
- [ ] Payroll calculated, reviewable, correctable and approved without silent input changes.
- [ ] Correct ERP entry (via Accounting handoff) and controlled payment data.
- [ ] Schema complete; integration, isolation, concurrency and full-scenario tests passed.

---

## 7. Business-analysis review (F1–F26) — status on 16 Nov

From the HR API review of 15 Nov (`ThinkOnErp_HR_API_BA_Review_Report.html`). Re-checked against today's code.

| # | Finding | Status | Evidence / what is left |
|---|---|---|---|
| F1 | DELETE employee terminated the employee and blocked EOS | ✅ Fixed | `DeleteEmployeeCommand` always refuses: "terminate through End of Service". **Left:** remove the endpoint or document it as 409/400; handler holds an unreachable success message. |
| F2 | PUT/create could set TERMINATED | ✅ Fixed | `EmployeeProfileWriter` + `EmployeeInputValidation` reject TERMINATED, termination date/reason (also in Excel import). **Left:** drop those fields from the DTOs. |
| F3 | Leave days trusted from client | ✅ Fixed | `SubmitLeaveRequestCommand` calculates working days server-side. |
| F4 | No overlap check | ✅ Fixed | Submit & approve reject overlapping PENDING/APPROVED leave or approved short leave. |
| F5 | No maker-checker / self-approval | ✅ Fixed | `HrApprovalGuard` in leave, short leave, correction, loan, advance, adjustment, payroll, EOS. |
| F6 | Redundant `companyId` | 🟡 Partial | Still 16 query params (`[FromQuery] long companyId`) in EOS list, adjustments, payroll periods, 6 payroll-policy GETs, work calendars ×3, short leaves ×2, attendance nearby-branches, leave calculate-days, plus `CompanyId` inside several bodies. Non-nullable ones give 403 when missing. |
| F7 | `CreatePeriod` bound the entity | ✅ Fixed | Uses `CreatePayrollPeriodDto`. |
| F8 | No payroll run list / payslip | ✅ Fixed (JSON) | `GET /payroll`, `GET /payroll/{id}/lines/{employeeCode}/payslip`, `GET /payroll/me/payslips`. **Left:** PDF payslip. |
| F9 | No timesheet | 🟡 Partial | `GET /attendance/days` (≤367 days), month summary. **Left:** branch daily report — waiting for definition of "branch" (check-in branch vs employee branch). |
| F10 | No attendance / EOS policy API | ✅ Fixed in code | `/attendance/policies`, `/end-of-service/policies`. **Left:** apply migration `002200`. |
| F11 | Org links not enforced | 🟡 Partial | `GET /organization/integrity` reports orphans. **Left:** DB FKs or scheduled check. |
| F12 | Pay period is free text | 🟡 Partial | Unique `PeriodCode` (migration `002300` not applied), adjustment/cash-out check open period. **Left:** other `PayPeriod` columns; FK vs id decision. |
| F13 | Cascade delete on salary history, balances, dependents | ❌ Open | Still `Cascade` in `EmployeeConfigurations` and `LeaveConfigurations`. |
| F14 | Missing FKs / unique (employee, date) on attendance | ❌ Open | No FK for AttendanceDay/ShiftAssignment/Correction → Employee; no unique day index in EF. |
| F15 | Multi-company in one schema | ✅ Resolved by decision | Schema per company confirmed; `COMPANY_ID` removed from 18 tables. |
| F16 | Incomplete CRUD | ❌ Open | Salary components (GET list + POST only), calendars/holidays (no update/delete), payroll policies (add only), dependents (no update), no GET-by-id for leave requests / loans / advances / corrections, no loan/advance reject. |
| F17 | Legacy `PayrollPostingConfigurationsController` | ❌ Open | Still present (AdminOnly, outside HR Swagger group). Remove. |
| F18 | Headcount not enforced | ❌ Open | |
| F19 | Status model too narrow | ❌ Open | Still ACTIVE / SUSPENDED / TERMINATED. |
| F20 | Rest-day sources & calendar assignment | ❌ Open | Document precedence; assign calendar per branch. |
| F21 | Loan eligibility rules | ❌ Open | No loan policy. |
| F22 | Duplicate employee update paths | ❌ Open | Full PUT still beside section PUTs. |
| F23 | Inconsistent conventions | ❌ Open | page/size vs pageIndex/pageSize, reason in query on unpost, etc. |
| F24 | Permission mapping | ❌ Open | Short leave submit still not self-service; no `hr-salary` screen. |
| F25 | 403 for unknown employee | ℹ️ By design | Document in Swagger. |
| F26 | Leave model gaps | ❌ Open | No half-day, delegate, real attachment upload, multi-level approval. |

**Summary:** 9 fixed or resolved · 4 partial · 12 open · 1 by design.

---

## 8. What is missing (not built)

| Priority | Capability | Note |
|---|---|---|
| High | **Employment contracts** | Legacy table `HR_EMPLOYMENT_CONTRACT` exists in `DEV_TEMPLATE` but is unmapped (OPS-15). |
| High | **Employee documents & expiry alerts** | Legacy `HR_EMPLOYEE_DOCUMENT` exists, unmapped. |
| High | **Job movement history** (promotion, transfer) | Legacy `HR_EMPLOYMENT_EVENT`, `HR_SALARY_REVISION` exist, unmapped. Salary is effective-dated; organisation fields are not. |
| High | **Payslip PDF + statutory reports** | SSC monthly report, income-tax annual statement, payroll register, department cost. |
| High | **Branch daily attendance report** | Part of F9; needs the branch definition. |
| Medium | **Approval workflow engine** | Multi-level / manager routing, delegation, notifications. Today: single approval + no-self-approval guard. |
| Medium | **Job grades & salary bands** | `JobGradeCode` on position; legacy `HR_JOB_GRADE` unmapped. |
| Medium | **Overtime pre-approval request** | Today overtime is created from attendance only. |
| Medium | **Org chart endpoint** | Tree of departments/positions/reporting lines. |
| Medium | **EOS provision accrual** | Legacy `HR_EOS_PROVISION_ACCRUAL` unmapped (monthly EOS liability). |
| Low | Recruitment, onboarding, assets, expense claims | Legacy `HR_CANDIDATE*`, `HR_JOB_REQUISITION`, `HR_ONBOARDING_TASK`, `HR_ASSET_ASSIGNMENT`, `HR_EXPENSE_CLAIM*` exist, unmapped. Future modules. |
| Low | FORMULA salary components | Deferred by owner (PAY-21-FORMULA). |
| — | All HR screens | Front-end is not in this repo (SET-02…06, SHIFT-07). |

---

## 9. Problems & risks

| # | Problem | Impact | Action |
|---|---|---|---|
| 1 | **Two schema sources** — EF tenant migrations + `HrSchemaTool` installers; 13+ tables excluded from migrations; `DEV_TEMPLATE` hard-coded widely in the tool. | Schema drift between tenants; new tenant may get incomplete schema. | Converge on one mechanism; parameterise schema name (OPS-16). |
| 2 | **Verifiers/fixtures still expect `COMPANY_ID`** after its removal. | Verifiers and some tests will fail when re-enabled. | Update before the next test cycle (OPS-16). |
| 3 | **Tests frozen; large refactor since last green run** (CQRS for 9 areas, F1–F12 changes). | Regressions are invisible until tests run. | Run HR.Tests + HR.Api.Tests as soon as the owner allows. |
| 4 | `ThinkOnErp.API.Tests` and `Infrastructure.Tests` **do not compile**. | No safety net outside HR suites. | Fix or remove stale tests. |
| 5 | **Obsolete service facades** beside new handlers (e.g. `LeaveService` 2,113 lines). | Two code paths; easy to fix one and miss the other. | Remove facades once tests pass. |
| 6 | **Migrations not applied / not registered** (`002100` unregistered, `002200`, `002300` pending). | Code expects columns/indexes that `DEV_TEMPLATE` may lack. | Backup → apply through the guarded path → register. |
| 7 | **3 invalid external triggers** on `DEV_TEMPLATE.HR_LEAVE_MOVEMENT` (owned by other schemas). | Manual leave-ledger writes may fail. | Owner review, then guarded repair script (OPS-12). |
| 8 | **No restorable PDB** for restore drill. | Cannot prove recovery before client sync. | Provide PDB (OPS-11). |
| 9 | **Security by property name** in `HrAccessFilter` (`CompanyId`, `BranchId`, `EmployeeCode`, `Approved`). | Renaming a DTO field silently drops a check. | Contract tests per request type. |
| 10 | **Performance** — scope subqueries on every HR query; attendance grows ~180k days/yr per 500 employees. | Slow payroll and reports at scale. | Indexes on filter columns; archive/partition later. |
| 11 | **No API versioning** (`/api/hr/...`). | Breaking mobile clients when contracts change (F6, F17, F22). | Introduce `/api/v1/hr` before removing params. |
| 12 | **Date/time mix** (`DateTime`, `DateOnly`, UTC) with Amman UTC+3. | Midnight punches/terminations on the wrong day. | Standardise on company-local `DateOnly` for business dates. |
| 13 | **Integrity only in code** (no DB FKs on org links, periods, attendance). | Orphans via imports, scripts, integrations. | F11–F14. |
| 14 | **Uncommitted working tree** (~158 changed files). | Risk of losing work; no review trail. | Commit in reviewed batches. |
| 15 | **Old HR data in DEV_TEMPLATE** — one APPROVED run without handoff, one old JV link without handoff. | Blocks automatic cut-over. | Manual Accounting review (ERP-09/INT-13). |

---

## 10. Stopped, deferred or blocked

| Item | Type | Waiting for |
|---|---|---|
| All HR test runs | Stopped by owner | Owner request to resume. |
| `TEST` and `THINKONERP_DEV02` | Off-limits | Owner instruction. |
| Tenant schema sync | Disabled (`AllowExistingTenantSchemaSync=false`) | OPS-03 plan + OPS-11 restore drill. |
| PAY-21-FORMULA | Deferred by owner (15 Nov) | Owner decision on formula contract. |
| ERP-06 bank file | Deferred by owner (temporary) | Accounting bank specification. |
| ERP-07 multi-currency batches | Deferred by owner | Accounting decisions. |
| OPS-11 restore drill | Blocked | Dedicated restorable PDB (connection is `CDB$ROOT`). |
| OPS-12 external triggers | Blocked | Review by owners of `THINKONERP_1122`, `THINKONERP_SALONDEMO`, `THINKONERP_THINKONCO`. |
| OPS-15 unmapped tables | Blocked | Business owner per table (23 tables). |
| OPS-07 / PAY-17 Oracle payment cycle | Blocked | Approved financial setup: currency + open fiscal year/period covering today. |
| F9 branch daily report | Waiting | Definition of branch (punch branch vs employee branch). |
| F12 FK on pay period | Deferred | Historical data and future-period check. |
| UI tasks (6) | Out of scope | Front-end repository. |

---

## 11. Database & schema state (`DEV_TEMPLATE` only)

- Latest backups: `artifacts/hr-audit/backups/20261115205428` (before COMPANY_ID removal), `20261115103656` (before accrual flag alignment). Manifests COMPLETED with SHA-256.
- Applied: 41 tenant migrations (history shows them; `20261115002100` applied without history row).
- Pending in code: `20261115002200_*` (attendance/EOS policy `EFFECTIVE_FROM/TO`), `20261115002300_*` (unique `PERIOD_CODE`).
- Checks last passed: 57/57 mapped SELECTs, 373/373 text length audit, verifiers for leave policy, accrual settings, department manager, payroll recovery accounts, EOS/short leave, accounting handoff.
- 23 HR tables in the schema have no EF mapping (OPS-15) — legacy contracts, documents, recruitment, job grade, expense claims, etc.
- Offline idempotent script for the 37-migration batch: `artifacts/hr-audit/hr-tenant-migrations-37-offline.sql` (not applied anywhere else).

---

## 12. Testing & verification state

| Suite / check | Last result | Note |
|---|---|---|
| HR.Tests (+ legacy POS) | 2,730/2,730 (13 Nov) | Before CQRS refactor and F1–F12. |
| HR.Api.Tests | 1,271/1,271 (13 Nov) | Includes Kestrel/JWT HTTP suites (`HrHttpOnly`, `HrAccessMatrixOnly`). |
| ThinkOnErp.API.Tests | Does not compile | Old `SysCurrency`/DTO references. |
| ThinkOnErp.Infrastructure.Tests | Does not compile | 315 errors, old signatures. |
| Oracle verifiers (HrSchemaTool) | Passed on DEV_TEMPLATE before COMPANY_ID removal | Need update for schema-per-company. |
| Oracle concurrency | Leave close, payroll input lock, journal serial locks passed | Payment lifecycle not proven (no open GL period). |
| Monitoring | Compose config valid | Docker daemon not available; not run. |

Verify before merging any HR batch:

```bash
dotnet build ThinkOnErp.sln
dotnet test tests/ThinkOnErp.HR.Tests        # only when the owner allows
dotnet test tests/ThinkOnErp.HR.Api.Tests    # only when the owner allows
```

---

## 13. Next steps (priority order)

1. Finish current batch: `PayPeriod` inventory and guards (F12).
2. Close quick API fixes: remove F17 controller, remove `companyId` params (F6) behind `/api/v1`, drop termination fields from DTOs (F2), DELETE employee endpoint decision (F1).
3. Delete-behaviour and FK migration: RESTRICT on salary/balances/dependents, attendance FKs + unique day (F13, F14) — after backup.
4. Missing CRUD (F16) and loan/advance reject + GET by id.
5. Update verifiers/fixtures for schema-per-company (OPS-16), apply `002200/002300`, register `002100`.
6. Ask owner to resume tests; fix the two non-compiling test projects; remove obsolete facades.
7. Unblock gates: PDB (OPS-11), trigger owners (OPS-12), table owners (OPS-15), financial setup for payment cycle (OPS-07).
8. Plan the next feature wave from §8 (contracts, documents, job history, payslip PDF/statutory reports, approval workflow).

---

## 14. Archived documents (moved to `docs/hr/_archive/`)

| Group | Files | Content now summarised in |
|---|---|---|
| Old roadmap | `HR_ROADMAP_AR.md` (514 KB, incl. long change log) | §2, §3, §6, §10, §11, §12 |
| Implementation history | `HR_IMPLEMENTATION_HISTORY_AR.md` | Historical only |
| Employees & organisation | `HR_EMPLOYEE_*` (11), `HR_ORGANIZATION_*` (2), `HR_DEPARTMENT_MANAGER_CONTRACT` | §5, §6 (EMP-*) |
| Shifts & attendance | `HR_SHIFT_*` (3), `HR_EFFECTIVE_SHIFT`, `HR_DAILY_ROSTER`, `HR_ROSTER_GENERATION`, `HR_ATTENDANCE_*` (4), `HR_RAW_ATTENDANCE_PROCESSING`, `HR_MOBILE_*` (2), `HR_BRANCH_ATTENDANCE_LOCATION` | §5, §6 (SHIFT-*, ATT-*) |
| Leaves | `HR_LEAVE_LEDGER`, `HR_CLOSED_CYCLE_GUARD`, `HR_LEAVE_BALANCE_MIGRATION_REVIEW_TEMPLATE` (still useful for LEV-16) | §5, §6 (LEV-*) |
| Payroll & accounting | `HR_ACCOUNTING_HANDOFF_ARCHITECTURE` (INT-01 contract), `HR_PAYROLL_*` (6 incl. FORMULA and multi-currency drafts), `HR_JOURNAL_SERIAL_CONCURRENCY` | §3, §5, §6 (PAY-*, ERP-*, INT-*) |
| Schema & operations | `HR_SCHEMA_APPLICATION_PLAN`, `HR_DEV_TEMPLATE_APPLIED`, `HR_TENANT_*` (5), `HR_UNMAPPED_*` (2), `HR_COLUMN_LENGTH_AUDIT`, `HR_CROSS_SCHEMA_TRIGGER_AUDIT`, `HR_RELATIONSHIP_MAP`, `HR_ACCESS_ACTION_MATRIX` | §9, §10, §11 |
| API snapshots | `docs/HR_API_REFERENCE_2026-11-02 … 11-13` (7) | Replaced by `ThinkOnErp_HR_API_Workflow_Guide.html` |
| Module docs | `docs/modules/05_HR_Payroll/busniss.md`, `logic.md`, `docs/HR_MODULE_STATUS_2026-11-02.md`, `docs/HR_CLEAN_ARCHITECTURE_REFACTOR.md`, `docs/HR_AUDIT/REPAIR_DEV_TEMPLATE_2026-10-08.md` | §3, §4 |
| Notes | `ThinkOnErp_HR_Fix_Estimate_And_Risks.md` | §9 |

Kept in place: `scripts/HrSchemaTool/README.md` (tool usage), `docs/HR_API_INVENTORY_*.json` (machine data), `docs/hr/OPS12_EXTERNAL_TRIGGER_REPAIR_DRAFT_AR.sql`, the two HTML reports.

---

## 15. Change log

| Date | Change |
|---|---|
| 16 Nov 2026 | Created this master file from the roadmap and all HR docs; archived 66 markdown files; added BA findings status, missing capabilities, risks and gates. API guide regenerated from current code (208 endpoints). |
