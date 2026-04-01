-- =============================================
-- ThinkOnErp Test Data Script
-- Description: Inserts sample data for testing
-- =============================================

-- Clear existing data (optional - uncomment if needed)
-- DELETE FROM SYS_USERS;
-- DELETE FROM SYS_BRANCH;
-- DELETE FROM SYS_COMPANY;
-- DELETE FROM SYS_CURRENCY;
-- DELETE FROM SYS_ROLE;

-- =============================================
-- Insert Roles
-- =============================================
INSERT INTO SYS_ROLE (ROW_ID, ROW_DESC, ROW_DESC_E, NOTE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_ROLE.NEXTVAL, 'مدير النظام', 'System Administrator', 'Full system access', '1', 'system', SYSDATE);

INSERT INTO SYS_ROLE (ROW_ID, ROW_DESC, ROW_DESC_E, NOTE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_ROLE.NEXTVAL, 'مدير', 'Manager', 'Department manager', '1', 'system', SYSDATE);

INSERT INTO SYS_ROLE (ROW_ID, ROW_DESC, ROW_DESC_E, NOTE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_ROLE.NEXTVAL, 'محاسب', 'Accountant', 'Financial operations', '1', 'system', SYSDATE);

INSERT INTO SYS_ROLE (ROW_ID, ROW_DESC, ROW_DESC_E, NOTE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_ROLE.NEXTVAL, 'موظف', 'Employee', 'Regular employee', '1', 'system', SYSDATE);

INSERT INTO SYS_ROLE (ROW_ID, ROW_DESC, ROW_DESC_E, NOTE, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_ROLE.NEXTVAL, 'مراجع', 'Auditor', 'Internal auditor', '1', 'system', SYSDATE);

-- =============================================
-- Insert Currencies
-- =============================================
INSERT INTO SYS_CURRENCY (
    ROW_ID, ROW_DESC, ROW_DESC_E, SHORT_DESC, SHORT_DESC_E,
    SINGLER_DESC, SINGLER_DESC_E, DUAL_DESC, DUAL_DESC_E,
    SUM_DESC, SUM_DESC_E, FRAC_DESC, FRAC_DESC_E,
    CURR_RATE, CURR_RATE_DATE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_CURRENCY.NEXTVAL, 'دولار أمريكي', 'US Dollar', '$', 'USD',
    'دولار', 'Dollar', 'دولاران', 'Dollars', 'دولارات', 'Dollars',
    'سنت', 'Cent', 1.00, SYSDATE, 'system', SYSDATE
);

INSERT INTO SYS_CURRENCY (
    ROW_ID, ROW_DESC, ROW_DESC_E, SHORT_DESC, SHORT_DESC_E,
    SINGLER_DESC, SINGLER_DESC_E, DUAL_DESC, DUAL_DESC_E,
    SUM_DESC, SUM_DESC_E, FRAC_DESC, FRAC_DESC_E,
    CURR_RATE, CURR_RATE_DATE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_CURRENCY.NEXTVAL, 'يورو', 'Euro', '€', 'EUR',
    'يورو', 'Euro', 'يوروان', 'Euros', 'يوروات', 'Euros',
    'سنت', 'Cent', 1.08, SYSDATE, 'system', SYSDATE
);

INSERT INTO SYS_CURRENCY (
    ROW_ID, ROW_DESC, ROW_DESC_E, SHORT_DESC, SHORT_DESC_E,
    SINGLER_DESC, SINGLER_DESC_E, DUAL_DESC, DUAL_DESC_E,
    SUM_DESC, SUM_DESC_E, FRAC_DESC, FRAC_DESC_E,
    CURR_RATE, CURR_RATE_DATE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_CURRENCY.NEXTVAL, 'ريال سعودي', 'Saudi Riyal', 'ر.س', 'SAR',
    'ريال', 'Riyal', 'ريالان', 'Riyals', 'ريالات', 'Riyals',
    'هللة', 'Halala', 0.27, SYSDATE, 'system', SYSDATE
);

INSERT INTO SYS_CURRENCY (
    ROW_ID, ROW_DESC, ROW_DESC_E, SHORT_DESC, SHORT_DESC_E,
    SINGLER_DESC, SINGLER_DESC_E, DUAL_DESC, DUAL_DESC_E,
    SUM_DESC, SUM_DESC_E, FRAC_DESC, FRAC_DESC_E,
    CURR_RATE, CURR_RATE_DATE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_CURRENCY.NEXTVAL, 'جنيه إسترليني', 'British Pound', '£', 'GBP',
    'جنيه', 'Pound', 'جنيهان', 'Pounds', 'جنيهات', 'Pounds',
    'بنس', 'Pence', 1.27, SYSDATE, 'system', SYSDATE
);

-- =============================================
-- Insert Companies
-- =============================================
INSERT INTO SYS_COMPANY (ROW_ID, ROW_DESC, ROW_DESC_E, COUNTRY_ID, CURR_ID, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_COMPANY.NEXTVAL, 'شركة ثينك أون', 'ThinkOn Company', 1, 1, '1', 'system', SYSDATE);

INSERT INTO SYS_COMPANY (ROW_ID, ROW_DESC, ROW_DESC_E, COUNTRY_ID, CURR_ID, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_COMPANY.NEXTVAL, 'مؤسسة التقنية المتقدمة', 'Advanced Technology Corporation', 1, 3, '1', 'system', SYSDATE);

INSERT INTO SYS_COMPANY (ROW_ID, ROW_DESC, ROW_DESC_E, COUNTRY_ID, CURR_ID, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_COMPANY.NEXTVAL, 'شركة الحلول الذكية', 'Smart Solutions Inc', 2, 2, '1', 'system', SYSDATE);

-- =============================================
-- Insert Branches
-- =============================================
INSERT INTO SYS_BRANCH (
    ROW_ID, PAR_ROW_ID, ROW_DESC, ROW_DESC_E, PHONE, MOBILE, FAX, EMAIL,
    IS_HEAD_BRANCH, IS_ACTIVE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_BRANCH.NEXTVAL, 1, 'المقر الرئيسي - الرياض', 'Head Office - Riyadh',
    '+966112345678', '+966501234567', '+966112345679', 'riyadh@thinkon.com',
    '1', '1', 'system', SYSDATE
);

INSERT INTO SYS_BRANCH (
    ROW_ID, PAR_ROW_ID, ROW_DESC, ROW_DESC_E, PHONE, MOBILE, FAX, EMAIL,
    IS_HEAD_BRANCH, IS_ACTIVE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_BRANCH.NEXTVAL, 1, 'فرع جدة', 'Jeddah Branch',
    '+966122345678', '+966502234567', '+966122345679', 'jeddah@thinkon.com',
    '0', '1', 'system', SYSDATE
);

INSERT INTO SYS_BRANCH (
    ROW_ID, PAR_ROW_ID, ROW_DESC, ROW_DESC_E, PHONE, MOBILE, FAX, EMAIL,
    IS_HEAD_BRANCH, IS_ACTIVE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_BRANCH.NEXTVAL, 1, 'فرع الدمام', 'Dammam Branch',
    '+966132345678', '+966503234567', '+966132345679', 'dammam@thinkon.com',
    '0', '1', 'system', SYSDATE
);

INSERT INTO SYS_BRANCH (
    ROW_ID, PAR_ROW_ID, ROW_DESC, ROW_DESC_E, PHONE, MOBILE, FAX, EMAIL,
    IS_HEAD_BRANCH, IS_ACTIVE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_BRANCH.NEXTVAL, 2, 'المقر الرئيسي - الخبر', 'Head Office - Khobar',
    '+966133345678', '+966504234567', '+966133345679', 'khobar@advtech.com',
    '1', '1', 'system', SYSDATE
);

INSERT INTO SYS_BRANCH (
    ROW_ID, PAR_ROW_ID, ROW_DESC, ROW_DESC_E, PHONE, MOBILE, FAX, EMAIL,
    IS_HEAD_BRANCH, IS_ACTIVE, CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_BRANCH.NEXTVAL, 3, 'المقر الرئيسي - دبي', 'Head Office - Dubai',
    '+971442345678', '+971501234567', '+971442345679', 'dubai@smartsolutions.com',
    '1', '1', 'system', SYSDATE
);

-- =============================================
-- Insert Users
-- Note: Password is 'Admin@123' hashed with SHA-256
-- Hash: 8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918
-- =============================================

-- Admin User
INSERT INTO SYS_USERS (
    ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME, PASSWORD, PHONE, PHONE2,
    ROLE, BRANCH_ID, EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
    CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_USERS.NEXTVAL, 'مدير النظام', 'System Admin', 'admin',
    '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918',
    '+966501234567', '+966112345678', 1, 1, 'admin@thinkon.com',
    NULL, '1', '1', 'system', SYSDATE
);

-- Manager User (Password: Manager@123)
-- Hash: 5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8
INSERT INTO SYS_USERS (
    ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME, PASSWORD, PHONE, PHONE2,
    ROLE, BRANCH_ID, EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
    CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_USERS.NEXTVAL, 'أحمد محمد', 'Ahmed Mohammed', 'ahmed.mohammed',
    '5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8',
    '+966502234567', '+966122345678', 2, 2, 'ahmed@thinkon.com',
    NULL, '1', '0', 'admin', SYSDATE
);

-- Accountant User (Password: Account@123)
-- Hash: 9AF15B336E6A9619928537DF30B2E6A2376569FCF9D7E773ECCEDE65606529A0
INSERT INTO SYS_USERS (
    ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME, PASSWORD, PHONE, PHONE2,
    ROLE, BRANCH_ID, EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
    CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_USERS.NEXTVAL, 'فاطمة علي', 'Fatima Ali', 'fatima.ali',
    '9AF15B336E6A9619928537DF30B2E6A2376569FCF9D7E773ECCEDE65606529A0',
    '+966503234567', '+966132345678', 3, 3, 'fatima@thinkon.com',
    NULL, '1', '0', 'admin', SYSDATE
);

-- Employee User (Password: Employee@123)
-- Hash: 8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92
INSERT INTO SYS_USERS (
    ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME, PASSWORD, PHONE, PHONE2,
    ROLE, BRANCH_ID, EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
    CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_USERS.NEXTVAL, 'خالد سعيد', 'Khaled Saeed', 'khaled.saeed',
    '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92',
    '+966504234567', '+966133345678', 4, 4, 'khaled@advtech.com',
    NULL, '1', '0', 'admin', SYSDATE
);

-- Auditor User (Password: Auditor@123)
-- Hash: 5906AC361A137E2D286465CD6588EDD5A2E5A08BB366B5D1F8F9C8E6E7F8A9B0
INSERT INTO SYS_USERS (
    ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME, PASSWORD, PHONE, PHONE2,
    ROLE, BRANCH_ID, EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
    CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_USERS.NEXTVAL, 'سارة حسن', 'Sara Hassan', 'sara.hassan',
    '5906AC361A137E2D286465CD6588EDD5A2E5A08BB366B5D1F8F9C8E6E7F8A9B0',
    '+971501234567', '+971442345678', 5, 5, 'sara@smartsolutions.com',
    NULL, '1', '0', 'admin', SYSDATE
);

-- Inactive User for testing (Password: Test@123)
-- Hash: 7C4A8D09CA3762AF61E59520943DC26494F8941B
INSERT INTO SYS_USERS (
    ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME, PASSWORD, PHONE, PHONE2,
    ROLE, BRANCH_ID, EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
    CREATION_USER, CREATION_DATE
) VALUES (
    SEQ_SYS_USERS.NEXTVAL, 'مستخدم معطل', 'Inactive User', 'inactive.user',
    '7C4A8D09CA3762AF61E59520943DC26494F8941B',
    '+966505234567', NULL, 4, 1, 'inactive@thinkon.com',
    NULL, '0', '0', 'admin', SYSDATE
);

COMMIT;

-- =============================================
-- Verify Data
-- =============================================
SELECT 'Roles Count: ' || COUNT(*) AS INFO FROM SYS_ROLE WHERE IS_ACTIVE = '1';
SELECT 'Currencies Count: ' || COUNT(*) AS INFO FROM SYS_CURRENCY;
SELECT 'Companies Count: ' || COUNT(*) AS INFO FROM SYS_COMPANY WHERE IS_ACTIVE = '1';
SELECT 'Branches Count: ' || COUNT(*) AS INFO FROM SYS_BRANCH WHERE IS_ACTIVE = '1';
SELECT 'Users Count: ' || COUNT(*) AS INFO FROM SYS_USERS WHERE IS_ACTIVE = '1';

-- Display sample data
SELECT 'Sample Roles:' AS INFO FROM DUAL;
SELECT ROW_ID, ROW_DESC_E, NOTE FROM SYS_ROLE WHERE IS_ACTIVE = '1' ORDER BY ROW_ID;

SELECT 'Sample Users:' AS INFO FROM DUAL;
SELECT ROW_ID, ROW_DESC_E, USER_NAME, IS_ADMIN FROM SYS_USERS WHERE IS_ACTIVE = '1' ORDER BY ROW_ID;
