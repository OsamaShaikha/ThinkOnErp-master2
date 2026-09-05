# ThinkOn ERP - HR & Payroll Module
**Complete Technical Roadmap & Build Specification for the Backend Team**  
*Document Date: August 28, 2026*

---


## Project Implementation Progress Tracker

| Phase | Description | Status | Progress | Notes / Next Step |
| :--- | :--- | :--- | :--- | :--- |
| **HR-0** | Foundational Decisions & Statutory Rules | `[x] COMPLETE` | 100% | Seeded `StatutoryRule` schema & Jordan 2026 parameter engine |
| **HR-1** | Organization Structure | `[x] COMPLETE` | 100% | `Department` wired with GL Cost Centers & Position org tree |
| **HR-2** | Employee Master & Lifecycle | `[x] COMPLETE` | 100% | Profile, dependents, documents & immutable `EmploymentEvent` |
| **HR-3** | Recruitment & Onboarding (Light ATS) | `[x] COMPLETE` | 100% | Requisitions, candidate pipelines, hire conversion & onboarding |
| **HR-4** | Attendance & Time Tracking | `[x] COMPLETE` | 100% | Shifts, clock-in/out, late/early leave & overtime workflows |
| **HR-5** | Leave Management | `[x] COMPLETE` | 100% | Jordan statutory leave types, tiered policies & accrual engine |
| **HR-6** | Compensation & Salary Structure | `[x] COMPLETE` | 100% | Salary components, contracts & minimum wage validation (290 JOD) |
| **HR-7** | Payroll Gross-to-Net Engine | `[x] COMPLETE` | 100% | Gross-to-Net (SSC ceiling 3349, ISTD progressive tax brackets, surcharge) |
| **HR-8** | End-of-Service & Final Settlement | `[x] COMPLETE` | 100% | Monthly EOS provision accrual & final settlement engine |
| **HR-9** | Statutory Filing & Payment Files | `[x] COMPLETE` | 100% | SSC Form 1 Return, ISTD Monthly Tax & Bank WPS file export |
| **HR-10** | Benefits Administration | `[x] COMPLETE` | 100% | Benefit plans & payroll deduction component mapping |
| **HR-11** | Expense Claims & Asset Assignment | `[x] COMPLETE` | 100% | Multi-line expense claims & asset assignment tracking |
| **HR-12** | Accounting Posting Matrix & SoD | `[x] COMPLETE` | 100% | Double-entry GL Journal Voucher integration & SoD controls |
| **HR-13** | Reporting & Self-Service (ESS/MSS) | `[x] COMPLETE` | 100% | Employee & Manager Self-Service endpoints & KPIs |

---

## 1. Purpose & Design Philosophy

This document serves as the complete technical build specification for the **HR & Payroll Module** within ThinkOn ERP. It provides the same level of architectural depth and rigor as the Accounting and Inventory roadmaps—covering detailed entity schemas, REST API contracts, business validation rules, and explicit Definitions of Done (DoD) for every implementation phase.

The specification was compiled via two core pillars:
1. **Benchmarking against mature global HR/HCM platforms** (SAP SuccessFactors, Workday, Oracle HCM, BambooHR, Odoo HR/Payroll).
2. **Deep localization for Jordan's statutory regulatory framework** (Social Security Corporation / SSC contribution rules, Income and Sales Tax Department / ISTD income tax brackets, and Jordan Labour Law leave regulations).

---

### 1.1 Where This Module Outperforms Global Benchmarks

| Feature / Area | Known Gap in Mature Global Systems | ThinkOn ERP Architectural Solution |
| :--- | :--- | :--- |
| **Jordan Statutory Compliance** | Workday, SAP SuccessFactors, and Oracle HCM treat Jordan as a "payroll partner required" market; native statutory calculation is missing and relies on expensive 3rd-party BPO plugins. | Jordan SSC contribution rules, progressive income tax brackets, and national contribution surcharges are native to the core payroll rule engine (§3.1, §10.1). |
| **Statutory Rate Flexibility** | Most global platforms hardcode statutory rates in application logic, requiring code deployments whenever ceilings or brackets change. | All statutory metrics (SSC ceilings, contribution percentages, tax brackets, minimum wage) are modeled as dated, versioned rule rows (`effectiveFrom` / `effectiveTo`). Updating rates is purely administrative data entry. |
| **Regional Leave Rules** | Leave engines assume a rigid standard set (annual/sick/parental) and fail to handle region-specific statutory categories cleanly (Hajj pilgrimage, SSC-administered maternity, unpaid iddah/bereavement). | The leave engine (§8) is driven by a flexible, configurable policy table. Custom accruals, eligibility tiers, and statutory pay treatments require no schema modifications. |
| **General Ledger Synchronization** | HR and Accounting systems operate in silos, requiring manual CSV/journal export-import reconciliations that cause GL drift. | Every payroll run posts natively via the unified Vouchers / Journal Entry engine (§14). There is zero ledger discrepancy. |
| **Compensation Data Privacy** | Salary data is protected only by generic role-based permissions, often leaking compensation figures across standard administrative screens. | A dedicated, multi-tier compensation permission model (§15.2) enforces field-level security across all APIs, reports, and logs. |
| **End-of-Service / Severance** | SMB systems assume a single hardcoded Gulf-style gratuity formula (employer-funded lump-sum per year of service). | Jordan's SSC-pension framework is distinct from Gulf gratuity. The engine provides a configurable calculation rule table (§11.1) to avoid hardcoded legal assumptions. |

---

### 1.2 Non-Negotiable Architectural Principles

1. **Posted Payroll Runs Are Strictly Immutable:** Corrections must always be executed through an adjustment run or explicit reversing voucher—never via in-place mutation of past payroll records.
2. **Zero Direct Ledger Posting:** Payroll and HR financial events post exclusively through the standard Vouchers API using configured posting rules (§14), identical to Sales, Purchasing, and Inventory.
3. **Statutory Correctness as Versioned Data:** SSC rates, tax brackets, minimum wage, and leave entitlements live in effective-dated database tables, never hardcoded application constants.
4. **Fail Loudly and Block:** An unbalanced payroll run, missing SSC registration number, or negative net pay must immediately block execution and alert administrators. Plausible-looking incorrect figures must never be posted.
5. **Default-Deny Compensation Privacy:** All endpoints, data models, and reports exposing salary figures must enforce field-level compensation permissions (§15.2) from day one.

---

## 2. Full Feature Catalog

| Category | Capabilities & Scope |
| :--- | :--- |
| **Organization Structure** | Legal entity/company, branch structure, department tree, position catalog, job grades & salary bands, hierarchical org chart & reporting lines. |
| **Employee Master & Lifecycle** | Core employee profile (personal, demographic, banking), document management with expiry tracking, dependent tracking, immutable append-only employment event log (hires, probations, promotions, transfers, terminations). |
| **Recruitment & Onboarding (Light ATS)** | Job requisitions, candidate pipeline management, offer tracking, candidate-to-employee conversion, automated onboarding task checklists. |
| **Attendance & Time Tracking** | Shift schedules, multi-channel clock-in/out (mobile, biometric sync, manual), overtime computation, exception handling (late, early departure, missed punch), timesheet approvals. |
| **Leave Management** | Configurable leave policies, monthly/lump-sum accrual engine, multi-tier approval workflows, balance tracking, carry-forward rollover rules, Jordan statutory categories. |
| **Compensation & Salary Structure** | Configurable catalog of salary components (earnings/deductions), formula-based and percentage-based calculations, contract management, effective-dated revision history. |
| **Payroll Engine (Gross-to-Net)** | Full gross-to-net execution, Jordan SSC employer/employee deductions with ceiling caps, progressive income tax withholding, national contribution surcharge, deduction processing, payslips. |
| **End-of-Service & Severance** | Configurable settlement calculation engine, monthly balance-sheet provision accrual, final settlement processing (gratuity, unused leave encashment, loan offsets). |
| **Statutory Filing & Remittance** | Monthly SSC contribution electronic portal file generation, ISTD tax withholding remittance summaries, automated WPS/bank bulk payment transfer files. |
| **Benefits Administration** | Company benefit plans (medical, life), employer/employee cost-sharing splits, payroll deduction mapping, dependent coverage tracking. |
| **Expense Claims & Assets** | Employee expense reimbursement (via payroll earning or AP voucher), company asset issuance/return tracking (laptops, access cards, vehicles). |
| **Performance Management** | Lightweight goal setting, periodic performance review cycles, manager/self-evaluations (staged for phased rollout). |
| **Self-Service Portals** | Employee Self-Service (ESS: payslips, leave requests, document submission) and Manager Self-Service (MSS: team calendar, leave approvals, attendance sign-off). |
| **Document & Compliance** | Employment contract templates, disciplinary records, policy acknowledgments, automated document expiry alerts (IDs, passports, work permits). |
| **Reporting & KPIs** | Headcount demographics, turnover/attrition rates, absenteeism metrics, overtime cost analysis, departmental payroll cost rollups, leave liability valuations. |
| **Controls & Audit** | Comprehensive audit trail on all profile and compensation changes, segregation of duties (SoD), strict field-level data protection. |

---

## 3. Foundational Decisions (Phase HR-0)

*Make these foundational architectural decisions before writing any domain code.*

### 3.1 Statutory Rule Engine
All government statutory values must be modeled as dated rule rows in a `StatutoryRule` table:
* **Fields:** `ruleType` (Enum: `SSC_EMPLOYER_RATE`, `SSC_EMPLOYEE_RATE`, `SSC_HIGH_RISK_SURCHARGE`, `SSC_CEILING`, `INCOME_TAX_BRACKET`, `NATIONAL_CONTRIBUTION_THRESHOLD`, `NATIONAL_CONTRIBUTION_RATE`, `MINIMUM_WAGE`), `value` / `bracketLow` / `bracketHigh` / `ratePercent`, `effectiveFrom`, `effectiveTo`.
* **Architecture Directive:** Build this table and its lookup resolver before any payroll calculation code. No payroll logic may reference application constants.

### 3.2 Pay Frequency & Payroll Calendar
Jordan standard operating procedure is a monthly payroll cycle. If specific employee categories (e.g., daily-wage warehouse labor) require alternate schedules, `PayrollCalendar` must support multiple concurrent run cycles per company.

### 3.3 Legal Entity Modeling
Following the Accounting module design, ThinkOn operates as a single legal entity with multiple operational branches. HR assigns employees directly to `branchId`.

### 3.4 Salary Payment Method
Salaries are disbursed via bank transfer. A WPS-style structured bulk bank payment file (§11.3) must be generated at the conclusion of each approved payroll cycle.

### 3.5 End-of-Service Legal Treatment
Jordan's statutory framework centers on Social Security Corporation old-age pension contributions rather than standalone employer-funded lump-sum gratuities. Do not hardcode a Gulf-style severance formula; implement calculation rules as configurable strategies (§11.1) validated against local legal counsel.

---

## 4. Phase HR-1: Organization Structure

### 4.1 Schema
* **Department:** `departmentCode`, `nameAr`, `nameEn`, `parentDepartmentCode`, `branchId`, `costCenterCode` (Foreign Key linking directly to Accounting Cost Center tree).
* **Position:** `positionCode`, `titleAr`, `titleEn`, `departmentCode`, `jobGradeCode`, `reportsToPositionCode` (Self-referencing foreign key creating the organization hierarchy).
* **JobGrade:** `gradeCode`, `nameAr`, `nameEn`, `minSalary`, `midSalary`, `maxSalary`, `level`.

### 4.2 REST API Contracts
* `GET / POST / PUT /hr/departments`
* `GET /hr/departments/tree`
* `GET / POST / PUT /hr/positions`
* `GET / POST / PUT /hr/job-grades`
* `GET /hr/org-chart?branchId={branchId}`

### 4.3 Definition of Done (DoD)
- [x] Department records successfully link to pre-existing Accounting Cost Centers.
- [x] Positions link to valid departments and reference parent positions.
- [x] The organizational chart renders accurately from `reportsToPositionCode` relationships.

---

## 5. Phase HR-2: Employee Master & Lifecycle

### 5.1 Schema
* **Employee:** `employeeCode`, `nameAr`, `nameEn`, `nationalId`, `nationality`, `dateOfBirth`, `gender`, `hireDate`, `positionCode`, `branchId`, `employmentType` (`FULL_TIME`, `PART_TIME`, `CONTRACT`, `DAILY_WAGE`), `employmentStatus` (`PROBATION`, `ACTIVE`, `SUSPENDED`, `ON_LEAVE`, `TERMINATED`), `sscNumber` (Mandatory before payroll run), `taxExemptionCount`, `bankAccountNumber`, `bankName`, `probationEndDate`, `terminationDate`, `terminationReason`, `managerEmployeeCode`.
* **EmployeeDependent:** `employeeCode`, `nameAr`, `nameEn`, `relationship`, `dateOfBirth` (Drives tax exemptions and benefit enrollments).
* **EmployeeDocument:** `employeeCode`, `documentType` (`NATIONAL_ID`, `PASSPORT`, `CONTRACT`, `CERTIFICATE`, `WORK_PERMIT`), `fileReference`, `expiryDate`, `issuedDate`.
* **EmploymentEvent (Immutable / Append-Only):** `employeeCode`, `eventType` (`HIRE`, `PROBATION_CONFIRM`, `TRANSFER`, `PROMOTION`, `DEMOTION`, `SUSPENSION`, `TERMINATION`, `REHIRE`), `effectiveDate`, `fromValue`, `toValue`, `approvedBy`, `reason`, `createdAt`.

### 5.2 Immutable Lifecycle Event Architecture
In alignment with `StockLedgerEntry` and `JournalLine`, the `Employee` table is a derived cache of the latest event state. Status updates, promotions, and transfers are permanently recorded as `EmploymentEvent` rows.

### 5.3 Document Expiry Tracking
Automated scanning identifies compliance-critical documents (work permits, national IDs) nearing expiration:
* `GET /hr/reports/documents-expiring-soon?withinDays=30`

### 5.4 REST API Contracts
* `GET / POST / PUT /hr/employees`
* `PATCH /hr/employees/{code}/status`
* `POST /hr/employees/{code}/events` (Records immutable lifecycle event)
* `GET /hr/employees/{code}/events` (Full historical audit trail)
* `POST /hr/employees/{code}/documents`
* `GET / POST /hr/employees/{code}/dependents`

### 5.5 Definition of Done (DoD)
- [x] Full employee lifecycle history is 100% reconstructable solely from `EmploymentEvent` entries.
- [x] Expiring documents reliably trigger alerts prior to lapse dates.

---

## 6. Phase HR-3: Recruitment & Onboarding (Light ATS)

### 6.1 Schema
* **JobRequisition:** `requisitionCode`, `positionCode`, `departmentCode`, `requestedBy`, `approvedBy`, `status` (`DRAFT`, `APPROVED`, `OPEN`, `FILLED`, `CANCELLED`), `targetHireDate`.
* **Candidate:** `candidateCode`, `nameAr`, `nameEn`, `contactInfo`, `resumeFileReference`, `source` (`REFERRAL`, `WEBSITE`, `AGENCY`).
* **CandidateApplication:** `candidateCode`, `requisitionCode`, `stage` (`APPLIED`, `SCREENING`, `INTERVIEW`, `OFFER`, `HIRED`, `REJECTED`), `notes`.
* **OnboardingChecklist / Task:** `employeeCode`, `taskName`, `assignedTo`, `dueDate`, `status` (`PENDING`, `DONE`) (e.g., IT hardware issue, SSC registration, contract signing).

### 6.2 REST API Contracts
* `POST /hr/requisitions`
* `POST /hr/requisitions/{id}/candidates`
* `PATCH /hr/candidates/{code}/applications/{id}/stage`
* `POST /hr/requisitions/{id}/applications/{id}/hire` (Converts Candidate to Employee, generates `HIRE` event, spawns onboarding checklist, closes requisition)
* `GET / POST /hr/employees/{code}/onboarding-checklist`

### 6.3 Definition of Done (DoD)
- [x] Candidate hiring seamlessly initializes the Employee Master, creates the `HIRE` event, and generates onboarding checklist tasks without manual re-entry.

---

## 7. Phase HR-4: Attendance & Time Tracking

### 7.1 Schema
* **ShiftSchedule:** `shiftCode`, `nameAr`, `nameEn`, `startTime`, `endTime`, `breakMinutes`, `workingDays[]`.
* **EmployeeShiftAssignment:** `employeeCode`, `shiftCode`, `effectiveFrom`, `effectiveTo`.
* **AttendanceRecord:** `employeeCode`, `date`, `clockIn`, `clockOut`, `source` (`MOBILE`, `BIOMETRIC`, `MANUAL`, `WEB`), `status` (`ON_TIME`, `LATE`, `EARLY_LEAVE`, `ABSENT`, `MISSED_PUNCH`), `correctedBy`, `correctionReason`.
* **OvertimeRecord:** `employeeCode`, `date`, `hours`, `approvedBy`, `rateMultiplier`.

### 7.2 Mobile & Biometric Offline-First Sync
Mobile punch operations cache punches locally and sync upon reconnection. Requests include unique idempotency keys to prevent duplicate punches.

### 7.3 REST API Contracts
* `GET / POST / PUT /hr/shifts`
* `POST /hr/attendance/clock-in` / `POST /hr/attendance/clock-out`
* `GET /hr/attendance?employeeCode={code}&fromDate={from}&toDate={to}`
* `POST /hr/attendance/{id}/correct` (Requires auditable mandatory reason)
* `POST /hr/timesheets/{employeeCode}/{period}/submit-for-approval`

### 7.4 Definition of Done (DoD)
- [x] Offline mobile punches synchronize without loss or duplicate records.
- [x] Attendance exceptions (late/missed punch) require formal resolution prior to timesheet sign-off.

---

## 8. Phase HR-5: Leave Management

### 8.1 Schema
* **LeaveType:** `leaveTypeCode`, `nameAr`, `nameEn`, `isPaid`, `isStatutory`, `requiresDocumentation`, `maxDaysPerYear`, `carryForwardAllowed`, `carryForwardCapDays`.
* **LeavePolicy:** `leaveTypeCode`, `applicableTo`, `accrualMethod` (`ANNUAL_LUMP_SUM`, `MONTHLY_ACCRUAL`, `SERVICE_TIERED`), `accrualRate`.
* **LeaveBalance:** `employeeCode`, `leaveTypeCode`, `year`, `accruedDays`, `usedDays`, `carriedForwardDays`, `remainingDays`.
* **LeaveRequest:** `employeeCode`, `leaveTypeCode`, `startDate`, `endDate`, `daysRequested`, `status` (`PENDING`, `APPROVED`, `REJECTED`, `CANCELLED`), `approvedBy`.

### 8.2 Jordan Statutory Leave Types
* **Annual Leave:** Service-tiered (baseline 14 days, increasing to 21 days after consecutive service threshold).
* **Sick Leave:** Statutory allowance with tiered compensation rules.
* **Maternity Leave:** Paid benefit integrated with SSC maternity fund.
* **Special Leave:** Hajj pilgrimage leave, bereavement/iddah leave.

### 8.3 Accrual Engine Algorithm
```python
def run_monthly_accrual():
    for employee in get_active_employees():
        for policy in get_applicable_policies(employee):
            accrue_balance(employee, policy, method=policy.accrualMethod)
            apply_year_end_carry_forward_caps(employee, policy)
```

### 8.4 REST API Contracts
* `GET / POST / PUT /hr/leave-types`
* `GET / POST / PUT /hr/leave-policies`
* `GET /hr/employees/{code}/leave-balance`
* `POST /hr/leave-requests`
* `POST /hr/leave-requests/{id}/approve` / `POST /hr/leave-requests/{id}/reject`

### 8.5 Definition of Done (DoD)
- [x] Monthly accruals execute accurately and prevent unallocated over-draws.
- [x] Year-end rollover strictly enforces carry-forward caps.

---

## 9. Phase HR-6: Compensation & Salary Structure

### 9.1 Schema
* **SalaryComponent:** `componentCode`, `nameAr`, `nameEn`, `componentType` (`EARNING`, `DEDUCTION`), `isTaxable`, `isSscApplicable`, `calculationType` (`FIXED_AMOUNT`, `PERCENT_OF_BASIC`, `FORMULA`).
* **EmployeeSalaryStructure / Line:** `employeeCode`, `effectiveFrom`, `effectiveTo`, `basicSalary`, `componentCode`, `amount`, `percent`.
* **SalaryRevision (Immutable):** `employeeCode`, `effectiveDate`, `oldBasicSalary`, `newBasicSalary`, `reason`, `approvedBy`.
* **EmploymentContract:** `employeeCode`, `contractType` (`LIMITED`, `UNLIMITED`), `startDate`, `endDate`, `fileReference`, `status`.

### 9.2 Definition of Done (DoD)
- [x] Gross pay is dynamically computed from active component lines with proper tax/SSC flags.
- [x] Salary adjustments generate auditable `SalaryRevision` entries and effective-dated structures without mutating history.

---

## 10. Phase HR-7: Payroll Engine (Gross-to-Net)

### 10.1 Jordan Statutory Payroll Reference

#### Social Security Corporation (SSC) Contributions
* **Employer Contribution:** `14.25%` of SSC-eligible gross (+`1.00%` for certified hazardous/high-risk roles).
* **Employee Contribution:** `7.50%` of SSC-eligible gross.
* **Contribution Salary Ceiling:** `JOD 3,349 / month` (Subject to periodic statutory adjustments).
* **Minimum Wage Enforcement:** `JOD 290 / month` (Engine blocks lower base structures).

#### Progressive Annual Income Tax Brackets (ISTD)
Tax calculated on annualized taxable income after personal and dependent exemptions:

| Annual Taxable Income Bracket (JOD) | Marginal Tax Rate |
| :--- | :--- |
| **0 - 5,000** | 5% |
| **5,001 - 10,000** | 10% |
| **10,001 - 15,000** | 15% |
| **15,001 - 20,000** | 20% |
| **20,001 - 1,000,000** | 25% |
| **Over 1,000,000** | 30% |

* **National Contribution Surcharge:** Additional `1%` on annual taxable income exceeding `JOD 200,000`.
* **Statutory Personal Exemptions:** Self-exemption, dependent allowances, documented medical/education/mortgage interest expenses.

---

### 10.2 Payroll Run Schema
* **PayrollRun:** `id`, `branchId`, `payPeriod` (YYYY-MM), `status` (`DRAFT`, `CALCULATED`, `APPROVED`, `POSTED`, `PAID`), `runDate`.
* **PayrollRunLine:** `payrollRunId`, `employeeCode`, `grossSalary`, `totalEarnings`, `totalDeductions`, `sscEmployeeContribution`, `sscEmployerContribution`, `incomeTaxWithheld`, `nationalContributionWithheld`, `otherDeductions`, `netPay`, `journalEntryId`.
* **PayrollRunLineComponent:** `payrollRunLineId`, `componentCode`, `amount`.

---

### 10.3 Gross-to-Net Calculation Algorithm

```python
def calculate_payroll(employee_code, pay_period):
    components = get_active_salary_components(employee_code, pay_period)
    gross_salary = sum(c.amount for c in components if c.type == 'EARNING')
    
    # 1. SSC Calculation with statutory ceiling
    ssc_eligible_salary = min(
        sum(c.amount for c in components if c.isSscApplicable),
        get_statutory_rule('SSC_CEILING', pay_period)
    )
    ssc_employee = ssc_eligible_salary * get_statutory_rule('SSC_EMPLOYEE_RATE', pay_period)
    ssc_employer = ssc_eligible_salary * (
        get_statutory_rule('SSC_EMPLOYER_RATE', pay_period) + 
        (get_statutory_rule('SSC_HIGH_RISK_SURCHARGE', pay_period) if is_high_risk(employee_code) else 0.0)
    )
    
    # 2. Income Tax Withholding (Annualized with Progressive Brackets)
    annual_taxable_gross = sum(c.amount for c in components if c.isTaxable) * 12
    annual_exemptions = get_personal_exemptions(employee_code, pay_period)
    annual_taxable_net = max(0, annual_taxable_gross - annual_exemptions)
    
    annual_tax = apply_progressive_brackets(annual_taxable_net)
    income_tax_monthly = annual_tax / 12.0
    
    # 3. National Contribution Surcharge (> JOD 200,000/yr)
    if annual_taxable_net > 200000:
        national_contrib_monthly = ((annual_taxable_net - 200000) * 0.01) / 12.0
    else:
        national_contrib_monthly = 0.0
        
    # 4. Other Deductions & Net Pay
    other_deductions = sum(c.amount for c in components if c.type == 'DEDUCTION') + get_loan_deductions(employee_code)
    net_pay = gross_salary - (ssc_employee + income_tax_monthly + national_contrib_monthly + other_deductions)
    
    assert net_pay >= 0, f"Negative net pay detected for employee {employee_code}!"
    return PayrollRunLine(...)
```

---

### 10.4 Run Lifecycle & State Transitions

```text
DRAFT ──(Calculate)──► CALCULATED ──(Approve)──► APPROVED ──(Post to GL)──► POSTED ──(Disburse)──► PAID
```

* Recalculations in `DRAFT` / `CALCULATED` replace line items idempotently.
* Once `POSTED`, lines are permanently locked.

### 10.5 REST API Contracts
* `POST /payroll/runs`
* `POST /payroll/runs/{id}/calculate`
* `POST /payroll/runs/{id}/submit-for-approval`
* `POST /payroll/runs/{id}/approve`
* `POST /payroll/runs/{id}/post` (Posts GL Voucher via §14)
* `GET /payroll/runs/{id}/lines`
* `GET /payroll/employees/{code}/payslips?period={period}`

### 10.6 Definition of Done (DoD)
- [x] Gross-to-net calculations correctly compute SSC, income tax, and surcharge against ceiling caps.
- [x] Changes to statutory rule tables apply exclusively to subsequent periods.
- [x] Negative net pay scenarios block processing and throw explicit validation alerts.

---

## 11. Phases HR-8 & HR-9: End-of-Service, Statutory Filing & Payment

### 11.1 End-of-Service & Severance
* **EndOfServiceCalculationRule:** `ruleCode`, `formula`, `appliesToTerminationReason` (`RESIGNATION`, `DISMISSAL`, `CONTRACT_END`, `RETIREMENT`), `effectiveFrom`, `effectiveTo`.
* **EndOfServiceProvisionAccrual:** `employeeCode`, `period`, `accruedAmount`, `journalEntryId` (Posts monthly debit to expense and credit to `223101 End-of-Service Provision`).
* **FinalSettlement:** `employeeCode`, `terminationDate`, `endOfServiceAmount`, `unusedLeaveEncashment`, `otherDeductions`, `netSettlement`, `journalEntryId`.

### 11.2 Statutory Portal Filings
* `POST /payroll/runs/{id}/ssc-submission-file` (Generates electronic file formatted for SSC portal submission).
* `POST /payroll/runs/{id}/tax-remittance-summary` (Aggregates monthly ISTD tax withholding summary).

### 11.3 Bulk Salary Payment File
* `GET /payroll/runs/{id}/bank-payment-file?format=WPS` (Generates standard bank clearing file).

### 11.4 Definition of Done (DoD)
- [x] Final settlements combine end-of-service, unused leave encashments, and debt recoveries into an auditable record.
- [x] Bank payment file total strictly balances with the run's aggregate `netPay`.

---

## 12. Phase HR-10: Benefits Administration

### 12.1 Schema & Payroll Integration
* **BenefitPlan:** `planCode`, `nameAr`, `nameEn`, `planType` (`MEDICAL`, `LIFE`, `OTHER`), `employerCostPerEmployee`, `employeeCostPerEmployee`, `providerName`.
* **EmployeeBenefitEnrollment:** `employeeCode`, `planCode`, `dependentsCovered[]`, `effectiveFrom`, `effectiveTo`, `status`.
* **Rule:** Employee cost shares map directly into a deduction `SalaryComponent` (§9.1).

### 12.2 Definition of Done (DoD)
- [x] Benefit enrollments with employee cost shares automatically insert deduction lines into subsequent payroll runs.

---

## 13. Phase HR-11: Expense Claims & Asset Assignment

### 13.1 Expense Claims
* **ExpenseClaim / Line:** `employeeCode`, `category`, `amount`, `receiptFileReference`, `status` (`SUBMITTED`, `APPROVED`, `REJECTED`, `REIMBURSED`), `reimbursementMethod` (`NEXT_PAYROLL_RUN`, `DIRECT_AP_PAYMENT`).
* Payroll-reimbursed claims attach as one-off `EARNING` components; AP-reimbursed claims generate standard supplier payment vouchers.

### 13.2 Asset Assignment Tracking
* **AssetAssignment:** `employeeCode`, `assetDescription`, `assetTag`, `issuedDate`, `expectedReturnDate`, `returnedDate`, `condition`.

### 13.3 Definition of Done (DoD)
- [x] Approved payroll-routed expense claims process as earnings on the next pay cycle.
- [x] Asset returns update assignment records without hard dependency on the full Fixed Asset Register.

---

## 14. Phase HR-12: Accounting Integration (Full Posting Matrix)

Every HR/Payroll event posts through the standard Vouchers API using the configured COA V2 Chart of Accounts:

| Event Description | Debit Account | Credit Account | Rule Code |
| :--- | :--- | :--- | :--- |
| **Gross Salary Expense** | `621101` Salaries & Wages / `621103` Bonuses / `621105` Benefits | `213101` Salaries Payable | `PAYROLL_ACCRUE` |
| **Employer SSC Expense** | `621102` Employer Social Security | `213201` Social Security Payable | `PAYROLL_ACCRUE` |
| **Employee SSC Withheld** | (Netted in `213101` Salaries Payable) | `213201` Social Security Payable | `PAYROLL_ACCRUE` |
| **Income Tax Withheld** | (Netted in `213101` Salaries Payable) | `214203` Employees Income Tax Payable | `PAYROLL_ACCRUE` |
| **Other Employee Deductions** | (Netted in `213101` Salaries Payable) | `213202` Other Employee Deductions | `PAYROLL_ACCRUE` |
| **Leave / Bonus Provision** | `621101` Salaries & Wages | `213102` Accrued Leave & Bonuses | `PAYROLL_ACCRUE` |
| **Salary Payment Disbursement** | `213101` Salaries Payable | `111101` / `111201` Bank / Cash | `PAYROLL_PAY` |
| **SSC Monthly Remittance** | `213201` Social Security Payable | `111101` / `111201` Bank / Cash | `SSC_REMIT` |
| **Tax Remittance to ISTD** | `214203` Employees Income Tax Payable | `111101` / `111201` Bank / Cash | `TAX_REMIT` |
| **Monthly End-of-Service Accrual** | `621101` Salaries (or dedicated provision expense) | `223101` End-of-Service Provision | `EOS_ACCRUE` |
| **Final Settlement Payout** | `223101` End-of-Service Provision | `111101` / `111201` Bank / Cash | `EOS_SETTLE` |
| **Employee Advance Issued** | `113101` Employee Advances | `111101` / `111201` Bank / Cash | `EMPLOYEE_ADVANCE` |
| **Advance Recovered via Payroll** | `213101` Salaries Payable | `113101` Employee Advances | `PAYROLL_ACCRUE` |
| **Expense Claim Reimbursed via Pay**| Relevant Expense Account | `213101` Salaries Payable | `PAYROLL_ACCRUE` |

### Definition of Done (DoD)
- [x] Posting a payroll run generates balanced journal vouchers grouped by branch and cost center without creating per-employee voucher noise.

---

## 15. Non-Functional Requirements & Controls

### 15.1 Concurrency & Idempotency
- Recalculating payroll runs in `DRAFT` or `CALCULATED` status overwrites prior run calculations cleanly without record duplication.
- `POSTED` runs reject any mutation or recalculation calls.

### 15.2 Compensation Permission Tier & Segregation of Duties (SoD)
* `PAYROLL_VIEW_OWN`: Employee self-service access to personal payslips and compensation only.
* `PAYROLL_VIEW_TEAM_LIMITED`: Managers can view team attendance, timesheets, and leave balances—**excluding** salary data.
* `PAYROLL_VIEW_TEAM_COMPENSATION`: Explicit privilege required to view team salary figures.
* `PAYROLL_RUN_CALCULATE` / `PAYROLL_RUN_APPROVE` / `PAYROLL_RUN_POST`: Mandatory segregation of duties (the user who calculates a run cannot approve or post it).
* `HR_EMPLOYEE_TERMINATE`: Restricted termination authorization.

### 15.3 Auditability
All lifecycle transitions, salary adjustments, and payroll runs are immutable and append-only.

### 15.4 Localization
All monetary amounts are denominated in Jordanian Dinar (JOD) and driven by versioned statutory rules.

---

## 16. Reporting & KPIs

| Report Name | Computation Logic & Source |
| :--- | :--- |
| **Headcount Analysis** | Active headcount breakdown by Department, Branch, and Job Grade. |
| **Turnover / Attrition Rate** | $(\text{Terminations} / \text{Average Active Headcount}) \times 100$ over period. |
| **Absenteeism Rate** | $(\text{Unplanned Absence Days} / \text{Total Standard Work Days}) \times 100$. |
| **Overtime Cost Analysis** | $\sum(\text{Overtime Hours} \times \text{Rate Multiplier} \times \text{Hourly Rate})$ by Cost Center. |
| **Departmental Payroll Cost** | Aggregated `PayrollRunLine` totals grouped via `Department.costCenterCode`. |
| **Leave Liability Valuation** | $\sum(\text{Accrued Unused Leave Days} \times \text{Daily Basic Salary Rate})$ (Balance sheet relevant). |
| **Time-to-Hire** | Elapsed calendar days from `JobRequisition.status = OPEN` to `HIRE` event. |
| **Benefits Cost Breakdown** | Total employer cost share across active `EmployeeBenefitEnrollment` records. |
| **Document Compliance Ratio** | Percentage of active employees with zero expired or expiring compliance documents. |

---

## 17. Suggested Sprint Implementation Sequence

```text
[HR-0: Foundational Rules & Calendar]
           │
           ▼
[HR-1: Org Structure] ───► [HR-12: Permissions & SoD (Seed Early)]
           │
           ▼
[HR-2: Employee Master & Lifecycle]
           │
           ├───► [HR-3: Light ATS & Onboarding]
           │
           ▼
[HR-4: Attendance & Time Tracking]
           │
           ▼
[HR-5: Leave Management Engine]
           │
           ▼
[HR-6: Compensation & Salary Catalog]
           │
           ▼
[HR-7: Payroll Gross-to-Net Engine] (MVP Core Milestone)
           │
           ├───► [HR-8 & 9: End-of-Service, Portals & Bank Payment Files]
           ├───► [HR-10: Benefits Administration]
           ├───► [HR-11: Expense Claims & Asset Assignment]
           ├───► [HR-12: Full Accounting Voucher Posting]
           └───► [HR-13: Reports & KPI Dashboards]
```

---

## 18. Post-MVP Deferred Scope

| Deferred Module | Rationale for Post-MVP Phasing |
| :--- | :--- |
| **Full ATS (AI Parsing & Multi-Job Board)** | Light requisition and hiring pipeline is sufficient to close the hiring loop for MVP. |
| **Learning Management System (LMS)** | Training tracking becomes relevant as headcount and formal compliance requirements scale. |
| **Succession Planning & Talent Pools** | Pertains to later organizational maturity phases. |
| **Multi-Country Statutory Payroll** | Each country requires independent statutory rule engines; keep MVP focused on Jordan. |
| **Hardware Biometric Terminals** | Start with mobile and manual punch logging; integrate biometric hardware per site demand. |
| **Workforce Demand Forecasting** | Requires historical baseline data before forecasting models become effective. |
| **360-Degree Performance Calibration** | Lightweight goal and review cycles are sufficient for initial releases. |

---

## 19. Important Compliance Note

> **CRITICAL LEGAL NOTICE:**  
> Statutory figures referenced in this roadmap (SSC contribution percentages, monthly ceilings, progressive ISTD income tax brackets, minimum wage, and exemption thresholds) reflect confirmed Jordanian legislation as of August 2026. Because payroll calculation carries significant legal and financial liabilities, all statutory rates in §10.1 and leave rules in §8.2 must be independently verified with official Social Security Corporation (SSC) guidelines, ISTD directives, or local legal/HR counsel prior to production go-live.
