# ThinkOnErp HR – Fix Estimate & Future Risks

_As of 15 Nov 2026 · Companion to `ThinkOnErp_HR_API_BA_Review_Report.html` (finding numbers F1–F26 refer to that report)_

## Short answer

One developer working with Claude needs about **4–6 weeks** to fix the 26 issues in the HR API review. Adding the missing HR features (documents, contracts, job history, approvals, reports) brings the total to **9–12 weeks**.

Claude writes the code fast. The time goes into reviewing it, testing against Oracle, migrating data in each tenant schema, and updating the front-end and mobile app.

## Time estimate (1 developer + Claude)

| Phase | What | Estimate | Why it takes that long |
|---|---|---|---|
| Sprint 1 – correctness | F1–F5, F7: termination only through End of Service, leave days calculated on the server, overlap check, no self-approval, payroll period request class | **4–6 days** | Small code changes. The time goes on tests and checking existing data (employees already marked TERMINATED). |
| Sprint 2 – usability | Remove `companyId`, payroll run list and payslip, timesheet, attendance and End of Service policy APIs, missing CRUD | **7–10 days** | New endpoints plus front-end changes. Removing `companyId` breaks current clients. |
| Sprint 3 – data integrity | Database foreign keys, pay period link, safer delete rules, unique attendance day per employee per date, confirm the multi-company model | **6–10 days** | The riskiest phase. Orphan data must be cleaned in **every tenant schema** before the constraints will apply. |
| Sprint 4 – polish | API style, permissions, statuses, loan rules | **4–6 days** | Low risk. |
| New features | Documents, contracts, job history, approval workflow, reports | **4–6 weeks** | Needs new business design, not just code. |

### Using Claude well on this repo

- Fix one issue per session and per pull request.
- Ask for the failing test first, then the fix.
- Keep `AGENTS.md` current so each session starts with the right context.
- Split very large services first: `LeaveService` is over 2,000 lines, which slows Claude down and invites mistakes.
- Run the existing ~130 HR test files on every change Claude makes.

## Problems you may face later with this structure and build

### 1. Two sources of truth for the database schema
18 HR tables are excluded from EF migrations and managed by a separate tool (`scripts/HrSchemaTool`, 83 files) with `DEV_TEMPLATE` hard-coded about 940 times. As tenants grow, schemas will drift: a deploy can work on one tenant and fail on another, and a new tenant may get an incomplete schema.
**Fix:** pick one schema-change mechanism (EF migrations or versioned SQL scripts) and remove the hard-coded schema name.

### 2. Deploys run against every tenant schema
There are 131 migrations, and each release must apply them to every company schema. One failure halfway leaves tenants on different versions.
**Fix:** a per-tenant migration runner that records each schema's version and can retry.

### 3. Security depends on property names
`HrAccessFilter` finds `CompanyId`, `BranchId`, `EmployeeCode` and `Approved` by name at runtime. Renaming a request field quietly removes a security check, and the compiler won't warn you.
**Fix:** tests that fail if a request class loses these checks.

### 4. Performance as data grows
Every HR query adds scope subqueries (`Employees.Any(...)`, `SysBranches.Any(...)`). 500 employees produce about 180,000 attendance days and 400,000+ raw punches a year.
**Fix:** indexes on the filter columns now; archiving or partitioning by date later.

### 5. Text codes as primary keys
Employee, department, position, leave type and shift are keyed by text codes. A code can never be renamed, letter-case mistakes cause duplicates, and two companies can't share a code.

### 6. No API versioning
Routes are `/api/hr/...` with no `v1`. Once the mobile app ships, every contract change in the report breaks old app versions.
**Fix:** add `/api/v1/hr` **before** Sprint 2.

### 7. Dates and time zones
The code mixes `DateTime`, `DateOnly` and `UtcNow`, while Amman is UTC+3. Punches or terminations near midnight can land on the wrong day, affecting attendance, leave and payroll proration.

### 8. Integrity enforced only in code
Imports, SQL scripts and future integrations (biometric devices, bank, other systems) skip application checks, so orphan data will build up.
**Fix:** database constraints, which protect you whichever way data comes in.

### 9. Project clutter
The project root has 251 markdown files plus several Dockerfiles, compose files and deploy scripts. It's hard to tell which deployment is current, easy to deploy the wrong config, and there's a real chance of committing secrets.
**Fix:** move documents into `docs/` and keep one deploy path.

### 10. Yearly legal changes
Social security rates, tax brackets and labour law change every year. Policies already have effective dates, which is good.
**Fix:** make sure every engine reads the policy in force on the payroll date, and add tests per year.
