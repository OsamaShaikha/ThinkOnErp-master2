-- =====================================================
-- SuperAdmin Troubleshooting Script
-- =====================================================
-- Run this script to diagnose SuperAdmin login issues
-- =====================================================

SET SERVEROUTPUT ON;

DECLARE
    v_count NUMBER;
    v_table_exists NUMBER;
    v_sequence_exists NUMBER;
    v_procedure_exists NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    DBMS_OUTPUT.PUT_LINE('SuperAdmin Troubleshooting Report');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- 1. Check if SYS_SUPER_ADMIN table exists
    -- =====================================================
    SELECT COUNT(*) INTO v_table_exists
    FROM USER_TABLES
    WHERE TABLE_NAME = 'SYS_SUPER_ADMIN';
    
    DBMS_OUTPUT.PUT_LINE('1. TABLE CHECK:');
    IF v_table_exists > 0 THEN
        DBMS_OUTPUT.PUT_LINE('   ✓ SYS_SUPER_ADMIN table EXISTS');
    ELSE
        DBMS_OUTPUT.PUT_LINE('   ✗ SYS_SUPER_ADMIN table DOES NOT EXIST');
        DBMS_OUTPUT.PUT_LINE('   → Run: @Database/Scripts/08_Create_Permissions_Tables.sql');
    END IF;
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- 2. Check if sequence exists
    -- =====================================================
    SELECT COUNT(*) INTO v_sequence_exists
    FROM USER_SEQUENCES
    WHERE SEQUENCE_NAME = 'SEQ_SYS_SUPER_ADMIN';
    
    DBMS_OUTPUT.PUT_LINE('2. SEQUENCE CHECK:');
    IF v_sequence_exists > 0 THEN
        DBMS_OUTPUT.PUT_LINE('   ✓ SEQ_SYS_SUPER_ADMIN sequence EXISTS');
    ELSE
        DBMS_OUTPUT.PUT_LINE('   ✗ SEQ_SYS_SUPER_ADMIN sequence DOES NOT EXIST');
        DBMS_OUTPUT.PUT_LINE('   → Run: @Database/Scripts/09_Create_Permissions_Sequences.sql');
    END IF;
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- 3. Check if stored procedures exist
    -- =====================================================
    SELECT COUNT(*) INTO v_procedure_exists
    FROM USER_PROCEDURES
    WHERE OBJECT_NAME = 'SP_SYS_SUPER_ADMIN_LOGIN';
    
    DBMS_OUTPUT.PUT_LINE('3. STORED PROCEDURE CHECK:');
    IF v_procedure_exists > 0 THEN
        DBMS_OUTPUT.PUT_LINE('   ✓ SP_SYS_SUPER_ADMIN_LOGIN procedure EXISTS');
    ELSE
        DBMS_OUTPUT.PUT_LINE('   ✗ SP_SYS_SUPER_ADMIN_LOGIN procedure DOES NOT EXIST');
        DBMS_OUTPUT.PUT_LINE('   → Run: @Database/Scripts/26_Add_SuperAdmin_Login_Procedure.sql');
    END IF;
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- 4. Check if any super admin accounts exist
    -- =====================================================
    IF v_table_exists > 0 THEN
        SELECT COUNT(*) INTO v_count FROM SYS_SUPER_ADMIN;
        
        DBMS_OUTPUT.PUT_LINE('4. SUPER ADMIN ACCOUNTS:');
        DBMS_OUTPUT.PUT_LINE('   Total accounts: ' || v_count);
        
        IF v_count = 0 THEN
            DBMS_OUTPUT.PUT_LINE('   ✗ NO super admin accounts found');
            DBMS_OUTPUT.PUT_LINE('   → Run: @Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql');
        ELSE
            DBMS_OUTPUT.PUT_LINE('   ✓ Super admin accounts found');
        END IF;
        DBMS_OUTPUT.PUT_LINE('');
        
        -- =====================================================
        -- 5. Check specific superadmin account
        -- =====================================================
        SELECT COUNT(*) INTO v_count 
        FROM SYS_SUPER_ADMIN 
        WHERE USER_NAME = 'superadmin';
        
        DBMS_OUTPUT.PUT_LINE('5. SUPERADMIN ACCOUNT CHECK:');
        IF v_count > 0 THEN
            DBMS_OUTPUT.PUT_LINE('   ✓ Account "superadmin" EXISTS');
            
            -- Check if active
            SELECT COUNT(*) INTO v_count 
            FROM SYS_SUPER_ADMIN 
            WHERE USER_NAME = 'superadmin' AND IS_ACTIVE = '1';
            
            IF v_count > 0 THEN
                DBMS_OUTPUT.PUT_LINE('   ✓ Account is ACTIVE');
            ELSE
                DBMS_OUTPUT.PUT_LINE('   ✗ Account is INACTIVE');
                DBMS_OUTPUT.PUT_LINE('   → Activate: UPDATE SYS_SUPER_ADMIN SET IS_ACTIVE = ''1'' WHERE USER_NAME = ''superadmin'';');
            END IF;
        ELSE
            DBMS_OUTPUT.PUT_LINE('   ✗ Account "superadmin" DOES NOT EXIST');
            DBMS_OUTPUT.PUT_LINE('   → Run: @Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql');
        END IF;
        DBMS_OUTPUT.PUT_LINE('');
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    DBMS_OUTPUT.PUT_LINE('End of Troubleshooting Report');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    
END;
/

-- =====================================================
-- Display all super admin accounts (if table exists)
-- =====================================================
DECLARE
    v_table_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_exists
    FROM USER_TABLES
    WHERE TABLE_NAME = 'SYS_SUPER_ADMIN';
    
    IF v_table_exists > 0 THEN
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('=====================================================');
        DBMS_OUTPUT.PUT_LINE('Current Super Admin Accounts:');
        DBMS_OUTPUT.PUT_LINE('=====================================================');
    END IF;
END;
/

-- Show accounts if table exists
SELECT 
    ROW_ID,
    USER_NAME,
    ROW_DESC_E AS NAME,
    EMAIL,
    CASE WHEN IS_ACTIVE = '1' THEN 'Active' ELSE 'Inactive' END AS STATUS,
    SUBSTR(PASSWORD, 1, 20) || '...' AS PASSWORD_HASH,
    CREATION_DATE
FROM SYS_SUPER_ADMIN
ORDER BY ROW_ID;

-- =====================================================
-- Test password hash for "SuperAdmin123!"
-- =====================================================
DECLARE
    v_table_exists NUMBER;
    v_expected_hash VARCHAR2(100) := '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918';
    v_actual_hash VARCHAR2(100);
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_exists
    FROM USER_TABLES
    WHERE TABLE_NAME = 'SYS_SUPER_ADMIN';
    
    IF v_table_exists > 0 THEN
        SELECT COUNT(*) INTO v_count
        FROM SYS_SUPER_ADMIN
        WHERE USER_NAME = 'superadmin';
        
        IF v_count > 0 THEN
            SELECT PASSWORD INTO v_actual_hash
            FROM SYS_SUPER_ADMIN
            WHERE USER_NAME = 'superadmin';
            
            DBMS_OUTPUT.PUT_LINE('');
            DBMS_OUTPUT.PUT_LINE('=====================================================');
            DBMS_OUTPUT.PUT_LINE('Password Hash Verification:');
            DBMS_OUTPUT.PUT_LINE('=====================================================');
            DBMS_OUTPUT.PUT_LINE('Expected hash: ' || v_expected_hash);
            DBMS_OUTPUT.PUT_LINE('Actual hash:   ' || v_actual_hash);
            
            IF v_actual_hash = v_expected_hash THEN
                DBMS_OUTPUT.PUT_LINE('✓ Password hash MATCHES');
            ELSE
                DBMS_OUTPUT.PUT_LINE('✗ Password hash DOES NOT MATCH');
                DBMS_OUTPUT.PUT_LINE('');
                DBMS_OUTPUT.PUT_LINE('Fix with:');
                DBMS_OUTPUT.PUT_LINE('UPDATE SYS_SUPER_ADMIN');
                DBMS_OUTPUT.PUT_LINE('SET PASSWORD = ''' || v_expected_hash || '''');
                DBMS_OUTPUT.PUT_LINE('WHERE USER_NAME = ''superadmin'';');
                DBMS_OUTPUT.PUT_LINE('COMMIT;');
            END IF;
        END IF;
    END IF;
END;
/

-- =====================================================
-- Quick Fix Commands
-- =====================================================
PROMPT
PROMPT =====================================================
PROMPT Quick Fix Commands (if needed):
PROMPT =====================================================
PROMPT
PROMPT -- If table doesn't exist:
PROMPT @Database/Scripts/08_Create_Permissions_Tables.sql
PROMPT
PROMPT -- If sequence doesn't exist:
PROMPT @Database/Scripts/09_Create_Permissions_Sequences.sql
PROMPT
PROMPT -- If login procedure doesn't exist:
PROMPT @Database/Scripts/26_Add_SuperAdmin_Login_Procedure.sql
PROMPT
PROMPT -- If no accounts exist:
PROMPT @Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql
PROMPT
PROMPT -- If account is inactive:
PROMPT UPDATE SYS_SUPER_ADMIN SET IS_ACTIVE = '1' WHERE USER_NAME = 'superadmin';
PROMPT COMMIT;
PROMPT
PROMPT =====================================================
