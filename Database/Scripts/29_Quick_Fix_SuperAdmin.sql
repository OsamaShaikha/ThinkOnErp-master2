-- =====================================================
-- Quick Fix: Create SuperAdmin Account
-- =====================================================
-- This script will create the superadmin account directly
-- Use this if the seed data script didn't work
-- =====================================================

SET SERVEROUTPUT ON;

DECLARE
    v_count NUMBER;
    v_new_id NUMBER;
    v_password_hash VARCHAR2(100) := '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918';
BEGIN
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    DBMS_OUTPUT.PUT_LINE('Quick Fix: Creating SuperAdmin Account');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    
    -- Check if account already exists
    SELECT COUNT(*) INTO v_count 
    FROM SYS_SUPER_ADMIN 
    WHERE USER_NAME = 'superadmin';
    
    IF v_count > 0 THEN
        DBMS_OUTPUT.PUT_LINE('Account "superadmin" already exists.');
        DBMS_OUTPUT.PUT_LINE('Updating password and activating account...');
        
        -- Update existing account
        UPDATE SYS_SUPER_ADMIN
        SET PASSWORD = v_password_hash,
            IS_ACTIVE = '1',
            UPDATE_DATE = SYSDATE,
            UPDATE_USER = 'SYSTEM'
        WHERE USER_NAME = 'superadmin';
        
        COMMIT;
        
        DBMS_OUTPUT.PUT_LINE('✓ Account updated successfully');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Creating new superadmin account...');
        
        -- Get next ID from sequence
        SELECT SEQ_SYS_SUPER_ADMIN.NEXTVAL INTO v_new_id FROM DUAL;
        
        -- Insert new account
        INSERT INTO SYS_SUPER_ADMIN (
            ROW_ID,
            ROW_DESC,
            ROW_DESC_E,
            USER_NAME,
            PASSWORD,
            EMAIL,
            PHONE,
            TWO_FA_ENABLED,
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE
        ) VALUES (
            v_new_id,
            'مدير النظام الرئيسي',
            'Main System Administrator',
            'superadmin',
            v_password_hash,
            'superadmin@thinkonerp.com',
            '+966501234567',
            '0',
            '1',
            'SYSTEM',
            SYSDATE
        );
        
        COMMIT;
        
        DBMS_OUTPUT.PUT_LINE('✓ Account created successfully with ID: ' || v_new_id);
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    DBMS_OUTPUT.PUT_LINE('Login Credentials:');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    DBMS_OUTPUT.PUT_LINE('Username: superadmin');
    DBMS_OUTPUT.PUT_LINE('Password: SuperAdmin123!');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Test with:');
    DBMS_OUTPUT.PUT_LINE('POST /api/auth/superadmin/login');
    DBMS_OUTPUT.PUT_LINE('{"userName":"superadmin","password":"SuperAdmin123!"}');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('ERROR: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('Possible issues:');
        DBMS_OUTPUT.PUT_LINE('1. Table SYS_SUPER_ADMIN does not exist');
        DBMS_OUTPUT.PUT_LINE('   → Run: @Database/Scripts/08_Create_Permissions_Tables.sql');
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('2. Sequence SEQ_SYS_SUPER_ADMIN does not exist');
        DBMS_OUTPUT.PUT_LINE('   → Run: @Database/Scripts/09_Create_Permissions_Sequences.sql');
        RAISE;
END;
/

-- Verify the account
SELECT 
    ROW_ID,
    USER_NAME,
    ROW_DESC_E AS NAME,
    EMAIL,
    CASE WHEN IS_ACTIVE = '1' THEN 'Active' ELSE 'Inactive' END AS STATUS,
    SUBSTR(PASSWORD, 1, 20) || '...' AS PASSWORD_HASH,
    CREATION_DATE
FROM SYS_SUPER_ADMIN
WHERE USER_NAME = 'superadmin';

-- =====================================================
-- Script Complete
-- =====================================================
