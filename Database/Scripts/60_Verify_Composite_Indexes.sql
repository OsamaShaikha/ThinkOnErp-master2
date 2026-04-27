-- Verification script for Task 1.6 Composite Indexes
-- This script checks if all required composite indexes exist and are valid

PROMPT ========================================
PROMPT Task 1.6: Composite Indexes Verification
PROMPT ========================================

-- Check if all required composite indexes exist
PROMPT
PROMPT Checking composite indexes status...
PROMPT

SELECT 
    CASE 
        WHEN index_name = 'IDX_AUDIT_LOG_COMPANY_DATE' THEN '1. Company + Date Index'
        WHEN index_name = 'IDX_AUDIT_LOG_ACTOR_DATE' THEN '2. Actor + Date Index'
        WHEN index_name = 'IDX_AUDIT_LOG_ENTITY_DATE' THEN '3. Entity + Date Index'
        WHEN index_name = 'IDX_AUDIT_LOG_BRANCH_DATE' THEN '4. Branch + Date Index'
        WHEN index_name = 'IDX_AUDIT_LOG_CATEGORY_DATE' THEN '5. Category + Date Index'
        WHEN index_name = 'IDX_AUDIT_LOG_SEVERITY_DATE' THEN '6. Severity + Date Index'
        WHEN index_name = 'IDX_AUDIT_COMPANY_BRANCH_DATE' THEN '7. Multi-tenant Index'
        ELSE index_name
    END AS "Index Description",
    index_name AS "Index Name",
    status AS "Status",
    CASE 
        WHEN status = 'VALID' THEN '✓'
        ELSE '✗'
    END AS "OK"
FROM user_indexes 
WHERE index_name IN (
    'IDX_AUDIT_LOG_COMPANY_DATE',
    'IDX_AUDIT_LOG_ACTOR_DATE', 
    'IDX_AUDIT_LOG_ENTITY_DATE',
    'IDX_AUDIT_LOG_BRANCH_DATE',
    'IDX_AUDIT_LOG_CATEGORY_DATE',
    'IDX_AUDIT_LOG_SEVERITY_DATE',
    'IDX_AUDIT_COMPANY_BRANCH_DATE'
)
ORDER BY index_name;

PROMPT
PROMPT Composite Index Column Details:
PROMPT

SELECT 
    ic.index_name AS "Index Name",
    ic.column_name AS "Column",
    ic.column_position AS "Position"
FROM user_ind_columns ic
WHERE ic.index_name IN (
    'IDX_AUDIT_LOG_COMPANY_DATE',
    'IDX_AUDIT_LOG_ACTOR_DATE', 
    'IDX_AUDIT_LOG_ENTITY_DATE',
    'IDX_AUDIT_LOG_BRANCH_DATE',
    'IDX_AUDIT_LOG_CATEGORY_DATE',
    'IDX_AUDIT_LOG_SEVERITY_DATE',
    'IDX_AUDIT_COMPANY_BRANCH_DATE'
)
ORDER BY ic.index_name, ic.column_position;

PROMPT
PROMPT Summary of Required Composite Indexes:
PROMPT
PROMPT ✓ IDX_AUDIT_LOG_COMPANY_DATE (COMPANY_ID, CREATION_DATE)
PROMPT ✓ IDX_AUDIT_LOG_ACTOR_DATE (ACTOR_ID, CREATION_DATE)
PROMPT ✓ IDX_AUDIT_LOG_ENTITY_DATE (ENTITY_TYPE, ENTITY_ID, CREATION_DATE)
PROMPT ✓ IDX_AUDIT_LOG_BRANCH_DATE (BRANCH_ID, CREATION_DATE)
PROMPT ✓ IDX_AUDIT_LOG_CATEGORY_DATE (EVENT_CATEGORY, CREATION_DATE)
PROMPT ✓ IDX_AUDIT_LOG_SEVERITY_DATE (SEVERITY, CREATION_DATE)
PROMPT ✓ IDX_AUDIT_COMPANY_BRANCH_DATE (COMPANY_ID, BRANCH_ID, CREATION_DATE)
PROMPT
PROMPT Task 1.6 Status: Ready for execution
PROMPT