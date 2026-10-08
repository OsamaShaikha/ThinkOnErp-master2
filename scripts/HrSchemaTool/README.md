# DEV_TEMPLATE HR repair utility

This utility is deliberately restricted to `DEV_TEMPLATE`. It reads the existing API Oracle configuration or the `ConnectionStrings__OracleDb` environment variable without printing connection details. Run from the repository root.

```powershell
dotnet run --project scripts/HrSchemaTool -- --preflight
dotnet run --project scripts/HrSchemaTool -- --apply
dotnet run --project scripts/HrSchemaTool -- --repair
dotnet run --project scripts/HrSchemaTool -- --verify-scopes
```

- `--preflight`: read-only column, constraint, row-count and mapped SELECT checks.
- `--apply`: employee/user optional unique foreign key, with existence checks.
- `--repair`: attendance columns, payroll posting configuration table, legacy salary/shift copy retaining stable IDs, and HR screen/feature registrations. Does not grant permissions, link users, select posting accounts or remove legacy tables.
- `--verify-scopes`: read-only EF/Oracle checks for all mapped HR tables with branch scope and an impossible self-service employee identity. Reads at most one row per mapping; reports no personnel values.

Repair inserts missing legacy IDs transactionally. Oracle DDL commits independently, so a failed run can have applied earlier schema steps. Existence checks allow retry; inspect `artifacts/hr-audit/dev-template-schema.txt` and `hr-repair-applied.txt` first. The three destination identity columns use `BY DEFAULT ON NULL` to retain original IDs; their next values are advanced beyond existing rows.

The database repair is a focused utility rather than a full EF migration-chain update. The historical EF snapshot has unrelated drift, including existing HR tables not recorded in it. Do not regenerate or execute a broad migration against DEV_TEMPLATE as part of this repair. No application startup migration is enabled by these changes.

After copying, the application uses `HR_SALARY_STRUCTURE`, `HR_SALARY_STRUCTURE_LINE` and `HR_EMPLOYEE_SHIFT_ASSIGNMENT`. Retained legacy tables are historical copies; old integrations writing only to them must switch to the current tables. Re-running the utility is not continuous synchronization.
