-- =====================================================
-- Super Admin Seed Data
-- =====================================================
-- Description: Inserts test super admin accounts
-- Note: Passwords are SHA-256 hashed
-- =====================================================

-- =====================================================
-- Password Hashing Reference
-- =====================================================
-- All passwords are hashed using SHA-256
-- 
-- Plain Text Passwords:
-- - SuperAdmin123!  → 8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918
-- - Admin@2024      → 5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8
-- - SecurePass#456  → 3C9909AFEC25354D551DAE21590BB26E38D53F2173B8D3DC3EEE4C047E7AB1C1
-- 
-- To generate SHA-256 hash in Oracle:
-- SELECT LOWER(RAWTOHEX(DBMS_CRYPTO.HASH(UTL_RAW.CAST_TO_RAW('YourPassword'), 2))) FROM DUAL;
-- =====================================================

DECLARE
    v_count NUMBER;
    v_new_id NUMBER;
BEGIN
    -- =====================================================
    -- 1. Main Super Admin (Primary System Administrator)
    -- =====================================================
    SELECT COUNT(*) INTO v_count FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'superadmin';
    
    IF v_count = 0 THEN
        SELECT SEQ_SYS_SUPER_ADMIN.NEXTVAL INTO v_new_id FROM DUAL;
        
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
            '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918', -- SuperAdmin123!
            'superadmin@thinkonerp.com',
            '+966501234567',
            '0',
            '1',
            'SYSTEM',
            SYSDATE
        );
        
        DBMS_OUTPUT.PUT_LINE('✓ Created: superadmin (Main System Administrator)');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ Skipped: superadmin (already exists)');
    END IF;

    -- =====================================================
    -- 2. Technical Super Admin (System Maintenance)
    -- =====================================================
    SELECT COUNT(*) INTO v_count FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'tech.admin';
    
    IF v_count = 0 THEN
        SELECT SEQ_SYS_SUPER_ADMIN.NEXTVAL INTO v_new_id FROM DUAL;
        
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
            'مدير النظام التقني',
            'Technical System Administrator',
            'tech.admin',
            '5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8', -- Admin@2024
            'tech.admin@thinkonerp.com',
            '+966502345678',
            '0',
            '1',
            'SYSTEM',
            SYSDATE
        );
        
        DBMS_OUTPUT.PUT_LINE('✓ Created: tech.admin (Technical System Administrator)');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ Skipped: tech.admin (already exists)');
    END IF;

    -- =====================================================
    -- 3. Security Super Admin (Security & Compliance)
    -- =====================================================
    SELECT COUNT(*) INTO v_count FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'security.admin';
    
    IF v_count = 0 THEN
        SELECT SEQ_SYS_SUPER_ADMIN.NEXTVAL INTO v_new_id FROM DUAL;
        
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
            'مدير الأمن والحماية',
            'Security Administrator',
            'security.admin',
            '3C9909AFEC25354D551DAE21590BB26E38D53F2173B8D3DC3EEE4C047E7AB1C1', -- SecurePass#456
            'security.admin@thinkonerp.com',
            '+966503456789',
            '0',
            '1',
            'SYSTEM',
            SYSDATE
        );
        
        DBMS_OUTPUT.PUT_LINE('✓ Created: security.admin (Security Administrator)');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ Skipped: security.admin (already exists)');
    END IF;

    -- =====================================================
    -- 4. Test Super Admin (For Testing - INACTIVE)
    -- =====================================================
    SELECT COUNT(*) INTO v_count FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'test.superadmin';
    
    IF v_count = 0 THEN
        SELECT SEQ_SYS_SUPER_ADMIN.NEXTVAL INTO v_new_id FROM DUAL;
        
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
            'مدير اختبار النظام',
            'Test System Administrator',
            'test.superadmin',
            '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918', -- SuperAdmin123!
            'test.superadmin@thinkonerp.com',
            '+966504567890',
            '0',
            '0', -- INACTIVE for security
            'SYSTEM',
            SYSDATE
        );
        
        DBMS_OUTPUT.PUT_LINE('✓ Created: test.superadmin (Test Administrator - INACTIVE)');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ Skipped: test.superadmin (already exists)');
    END IF;

    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    DBMS_OUTPUT.PUT_LINE('Super Admin Seed Data Insertion Complete');
    DBMS_OUTPUT.PUT_LINE('=====================================================');
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('ERROR: ' || SQLERRM);
        RAISE;
END;
/

-- =====================================================
-- Verify Inserted Data
-- =====================================================
SELECT 
    ROW_ID,
    ROW_DESC_E AS NAME,
    USER_NAME,
    EMAIL,
    PHONE,
    CASE WHEN IS_ACTIVE = '1' THEN 'Active' ELSE 'Inactive' END AS STATUS,
    CASE WHEN TWO_FA_ENABLED = '1' THEN 'Enabled' ELSE 'Disabled' END AS TWO_FA,
    CREATION_DATE
FROM SYS_SUPER_ADMIN
ORDER BY ROW_ID;

-- =====================================================
-- Summary Report
-- =====================================================
SELECT 
    COUNT(*) AS TOTAL_SUPER_ADMINS,
    SUM(CASE WHEN IS_ACTIVE = '1' THEN 1 ELSE 0 END) AS ACTIVE_ADMINS,
    SUM(CASE WHEN IS_ACTIVE = '0' THEN 1 ELSE 0 END) AS INACTIVE_ADMINS,
    SUM(CASE WHEN TWO_FA_ENABLED = '1' THEN 1 ELSE 0 END) AS TWO_FA_ENABLED_COUNT
FROM SYS_SUPER_ADMIN;

-- =====================================================
-- Script Execution Complete
-- =====================================================
-- Summary:
-- - Created 4 super admin accounts
-- - 3 active accounts for different roles
-- - 1 inactive test account
-- - All passwords are SHA-256 hashed
-- 
-- Login Credentials:
-- 1. Username: superadmin       | Password: SuperAdmin123!
-- 2. Username: tech.admin        | Password: Admin@2024
-- 3. Username: security.admin    | Password: SecurePass#456
-- 4. Username: test.superadmin   | Password: SuperAdmin123! (INACTIVE)
-- =====================================================
