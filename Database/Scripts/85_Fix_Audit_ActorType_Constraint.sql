-- =====================================================
-- Fix SYS_AUDIT_LOG ACTOR_TYPE Check Constraint
-- Description: Adds 'SYSTEM' to the allowed ACTOR_TYPE values
-- Issue: ORA-02290 check constraint violation when logging system events
-- Date: 2026-05-05
-- =====================================================

-- Drop the existing check constraint
DECLARE
    v_constraint_name VARCHAR2(128);
BEGIN
    -- Find the constraint name for ACTOR_TYPE check
    SELECT constraint_name INTO v_constraint_name
    FROM user_constraints
    WHERE table_name = 'SYS_AUDIT_LOG'
    AND constraint_type = 'C'
    AND search_condition LIKE '%ACTOR_TYPE%IN%';
    
    -- Drop the constraint
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_AUDIT_LOG DROP CONSTRAINT ' || v_constraint_name;
    
    DBMS_OUTPUT.PUT_LINE('Dropped constraint: ' || v_constraint_name);
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('No ACTOR_TYPE check constraint found');
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error dropping constraint: ' || SQLERRM);
        RAISE;
END;
/

-- Add the new check constraint with SYSTEM included
ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT CHK_AUDIT_LOG_ACTOR_TYPE 
    CHECK (ACTOR_TYPE IN ('SUPER_ADMIN', 'COMPANY_ADMIN', 'USER', 'SYSTEM'));

COMMENT ON COLUMN SYS_AUDIT_LOG.ACTOR_TYPE IS 'Type of actor: SUPER_ADMIN, COMPANY_ADMIN, USER, or SYSTEM';

-- Verify the constraint was added
SELECT constraint_name, constraint_type, search_condition
FROM user_constraints
WHERE table_name = 'SYS_AUDIT_LOG'
AND constraint_name = 'CHK_AUDIT_LOG_ACTOR_TYPE';

COMMIT;

PROMPT =====================================================
PROMPT SYS_AUDIT_LOG ACTOR_TYPE constraint updated successfully
PROMPT Now allows: SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM
PROMPT =====================================================
