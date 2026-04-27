-- Task 1.3: Add foreign key constraint for BRANCH_ID to SYS_BRANCH table
-- This script adds the foreign key constraint to ensure referential integrity
-- between the BRANCH_ID column in SYS_AUDIT_LOG and the SYS_BRANCH table

-- Check if the foreign key constraint already exists
DECLARE
    constraint_count NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO constraint_count
    FROM user_constraints
    WHERE constraint_name = 'FK_AUDIT_LOG_BRANCH'
    AND table_name = 'SYS_AUDIT_LOG';
    
    IF constraint_count = 0 THEN
        -- Add foreign key constraint if it doesn't exist
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT FK_AUDIT_LOG_BRANCH 
                          FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID)';
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_AUDIT_LOG_BRANCH added successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_AUDIT_LOG_BRANCH already exists.');
    END IF;
END;
/

-- Add comment to document the constraint purpose
COMMENT ON TABLE SYS_AUDIT_LOG IS 'Audit log table with comprehensive traceability support including multi-tenant branch context';

-- Verify the constraint was created
SELECT 
    constraint_name,
    constraint_type,
    table_name,
    r_constraint_name,
    status
FROM user_constraints
WHERE constraint_name = 'FK_AUDIT_LOG_BRANCH';

-- Verify the referenced table and column
SELECT 
    a.constraint_name,
    a.table_name AS child_table,
    a.column_name AS child_column,
    b.table_name AS parent_table,
    b.column_name AS parent_column
FROM user_cons_columns a
JOIN user_cons_columns b ON a.r_constraint_name = b.constraint_name
WHERE a.constraint_name = 'FK_AUDIT_LOG_BRANCH';

-- Test the constraint by checking if BRANCH_ID values in SYS_AUDIT_LOG 
-- reference valid ROW_ID values in SYS_BRANCH
SELECT 
    COUNT(*) AS total_audit_records,
    COUNT(CASE WHEN al.BRANCH_ID IS NOT NULL THEN 1 END) AS records_with_branch_id,
    COUNT(CASE WHEN al.BRANCH_ID IS NOT NULL AND b.ROW_ID IS NULL THEN 1 END) AS invalid_branch_references
FROM SYS_AUDIT_LOG al
LEFT JOIN SYS_BRANCH b ON al.BRANCH_ID = b.ROW_ID;

COMMIT;

-- Display completion message
DECLARE
    constraint_count NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO constraint_count
    FROM user_constraints
    WHERE constraint_name = 'FK_AUDIT_LOG_BRANCH'
    AND table_name = 'SYS_AUDIT_LOG'
    AND status = 'ENABLED';
    
    IF constraint_count = 1 THEN
        DBMS_OUTPUT.PUT_LINE('SUCCESS: Task 1.3 completed - Foreign key constraint FK_AUDIT_LOG_BRANCH is active and enforcing referential integrity.');
        DBMS_OUTPUT.PUT_LINE('- BRANCH_ID in SYS_AUDIT_LOG now references SYS_BRANCH.ROW_ID');
        DBMS_OUTPUT.PUT_LINE('- NULL values in BRANCH_ID are allowed (for audit events without branch context)');
        DBMS_OUTPUT.PUT_LINE('- Multi-tenant data integrity is now enforced at the database level');
    ELSE
        DBMS_OUTPUT.PUT_LINE('ERROR: Foreign key constraint was not created successfully.');
    END IF;
END;
/