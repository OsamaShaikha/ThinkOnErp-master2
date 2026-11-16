# HR work tracking

These instructions apply when working on the HR module or its ERP integrations.

- Read `HR_MASTER.md` before selecting the next HR task. It is the only HR document and the source of truth for current scope, agreed decisions, task IDs, priorities, gates and verification status. Do not create new HR markdown files; update `HR_MASTER.md` instead.
- When the user says to continue the HR plan, resume the current task or the next listed priority. Explain any necessary change of priority.
- Identify the task IDs covered by the implementation batch in the progress update.
- Before the final response, update the affected task rows (§6), the snapshot and current batch (§2), the BA findings table (§7) when relevant, and add one line to the change log (§15) in `HR_MASTER.md`.
- Mark a task complete only for its stated scope. Split completed work from remaining work instead of checking off a partially implemented module.
- Distinguish code completion, unit tests, Oracle integration/concurrency checks, schema application, tenant synchronization, and production deployment. Do not infer one from another.
- Preserve agreed company-configurable behavior. Do not reopen settled requirements or add fixed country rules as product constraints.
- Put new gaps in the appropriate section with stable task IDs and dependencies. Keep the main file a task map rather than appending repeated narrative updates.
- `docs/hr/_archive/` holds the old roadmap and HR documents for reference only. Nothing in it overrides `HR_MASTER.md` or the user's instructions.

For unrelated tasks, this HR-specific workflow does not apply.
