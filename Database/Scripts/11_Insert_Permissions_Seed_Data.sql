-- =====================================================
-- Permissions System - Seed Data Script
-- Phase 2: Insert demo systems, screens, and test data
-- =====================================================

-- =====================================================
-- 1. Insert Systems
-- =====================================================

-- Accounting System
INSERT INTO SYS_SYSTEM (ROW_ID, SYSTEM_CODE, SYSTEM_NAME, SYSTEM_NAME_E, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_SYSTEM.NEXTVAL, 'accounting', 'نظام المحاسبة', 'Accounting System', 'إدارة الحسابات والمعاملات المالية', 'Manage accounts and financial transactions', 'calculator', 1, '1', 'SYSTEM', SYSDATE);

-- Inventory System
INSERT INTO SYS_SYSTEM (ROW_ID, SYSTEM_CODE, SYSTEM_NAME, SYSTEM_NAME_E, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_SYSTEM.NEXTVAL, 'inventory', 'نظام المخزون', 'Inventory System', 'إدارة المنتجات والمخازن', 'Manage products and warehouses', 'warehouse', 2, '1', 'SYSTEM', SYSDATE);

-- HR System
INSERT INTO SYS_SYSTEM (ROW_ID, SYSTEM_CODE, SYSTEM_NAME, SYSTEM_NAME_E, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_SYSTEM.NEXTVAL, 'hr', 'نظام الموارد البشرية', 'HR System', 'إدارة الموظفين والرواتب', 'Manage employees and payroll', 'users', 3, '1', 'SYSTEM', SYSDATE);

-- CRM System
INSERT INTO SYS_SYSTEM (ROW_ID, SYSTEM_CODE, SYSTEM_NAME, SYSTEM_NAME_E, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_SYSTEM.NEXTVAL, 'crm', 'نظام إدارة العملاء', 'CRM System', 'إدارة العملاء والمبيعات', 'Manage customers and sales', 'user-check', 4, '1', 'SYSTEM', SYSDATE);

-- POS System
INSERT INTO SYS_SYSTEM (ROW_ID, SYSTEM_CODE, SYSTEM_NAME, SYSTEM_NAME_E, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
VALUES (SEQ_SYS_SYSTEM.NEXTVAL, 'pos', 'نظام نقاط البيع', 'POS System', 'إدارة نقاط البيع والمبيعات اليومية', 'Manage point of sale and daily sales', 'shopping-cart', 5, '1', 'SYSTEM', SYSDATE);

COMMIT;

-- =====================================================
-- 2. Insert Screens for Accounting System
-- =====================================================

DECLARE
    V_SYSTEM_ID NUMBER;
BEGIN
    SELECT ROW_ID INTO V_SYSTEM_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'accounting';
    
    -- Chart of Accounts
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'chart_of_accounts', 'دليل الحسابات', 'Chart of Accounts', '/accounting/chart-of-accounts', 'إدارة دليل الحسابات', 'Manage chart of accounts', 'list', 1, '1', 'SYSTEM', SYSDATE);
    
    -- Journal Entries
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'journal_entries', 'القيود اليومية', 'Journal Entries', '/accounting/journal-entries', 'إدارة القيود اليومية', 'Manage journal entries', 'book', 2, '1', 'SYSTEM', SYSDATE);
    
    -- Invoices
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'invoices', 'الفواتير', 'Invoices', '/accounting/invoices', 'إدارة الفواتير', 'Manage invoices', 'file-text', 3, '1', 'SYSTEM', SYSDATE);
    
    -- Payments
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'payments', 'المدفوعات', 'Payments', '/accounting/payments', 'إدارة المدفوعات', 'Manage payments', 'dollar-sign', 4, '1', 'SYSTEM', SYSDATE);
    
    -- Financial Reports
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'financial_reports', 'التقارير المالية', 'Financial Reports', '/accounting/reports', 'عرض التقارير المالية', 'View financial reports', 'bar-chart', 5, '1', 'SYSTEM', SYSDATE);
    
    COMMIT;
END;
/

-- =====================================================
-- 3. Insert Screens for Inventory System
-- =====================================================

DECLARE
    V_SYSTEM_ID NUMBER;
BEGIN
    SELECT ROW_ID INTO V_SYSTEM_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'inventory';
    
    -- Products
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'products', 'المنتجات', 'Products', '/inventory/products', 'إدارة المنتجات', 'Manage products', 'package', 1, '1', 'SYSTEM', SYSDATE);
    
    -- Warehouses
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'warehouses', 'المخازن', 'Warehouses', '/inventory/warehouses', 'إدارة المخازن', 'Manage warehouses', 'home', 2, '1', 'SYSTEM', SYSDATE);
    
    -- Stock Movements
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'stock_movements', 'حركات المخزون', 'Stock Movements', '/inventory/stock-movements', 'إدارة حركات المخزون', 'Manage stock movements', 'truck', 3, '1', 'SYSTEM', SYSDATE);
    
    -- Purchase Orders
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'purchase_orders', 'أوامر الشراء', 'Purchase Orders', '/inventory/purchase-orders', 'إدارة أوامر الشراء', 'Manage purchase orders', 'shopping-bag', 4, '1', 'SYSTEM', SYSDATE);
    
    -- Stock Reports
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'stock_reports', 'تقارير المخزون', 'Stock Reports', '/inventory/reports', 'عرض تقارير المخزون', 'View stock reports', 'pie-chart', 5, '1', 'SYSTEM', SYSDATE);
    
    COMMIT;
END;
/

-- =====================================================
-- 4. Insert Screens for HR System
-- =====================================================

DECLARE
    V_SYSTEM_ID NUMBER;
BEGIN
    SELECT ROW_ID INTO V_SYSTEM_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'hr';
    
    -- Employees
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'employees', 'الموظفين', 'Employees', '/hr/employees', 'إدارة الموظفين', 'Manage employees', 'user', 1, '1', 'SYSTEM', SYSDATE);
    
    -- Payroll
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'payroll', 'الرواتب', 'Payroll', '/hr/payroll', 'إدارة الرواتب', 'Manage payroll', 'credit-card', 2, '1', 'SYSTEM', SYSDATE);
    
    -- Leave Requests
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'leave_requests', 'طلبات الإجازة', 'Leave Requests', '/hr/leave-requests', 'إدارة طلبات الإجازة', 'Manage leave requests', 'calendar', 3, '1', 'SYSTEM', SYSDATE);
    
    -- Attendance
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'attendance', 'الحضور والانصراف', 'Attendance', '/hr/attendance', 'إدارة الحضور والانصراف', 'Manage attendance', 'clock', 4, '1', 'SYSTEM', SYSDATE);
    
    -- HR Reports
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'hr_reports', 'تقارير الموارد البشرية', 'HR Reports', '/hr/reports', 'عرض تقارير الموارد البشرية', 'View HR reports', 'file', 5, '1', 'SYSTEM', SYSDATE);
    
    COMMIT;
END;
/

-- =====================================================
-- 5. Insert Screens for CRM System
-- =====================================================

DECLARE
    V_SYSTEM_ID NUMBER;
BEGIN
    SELECT ROW_ID INTO V_SYSTEM_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'crm';
    
    -- Customers
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'customers', 'العملاء', 'Customers', '/crm/customers', 'إدارة العملاء', 'Manage customers', 'users', 1, '1', 'SYSTEM', SYSDATE);
    
    -- Leads
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'leads', 'العملاء المحتملين', 'Leads', '/crm/leads', 'إدارة العملاء المحتملين', 'Manage leads', 'user-plus', 2, '1', 'SYSTEM', SYSDATE);
    
    -- Opportunities
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'opportunities', 'الفرص', 'Opportunities', '/crm/opportunities', 'إدارة الفرص', 'Manage opportunities', 'target', 3, '1', 'SYSTEM', SYSDATE);
    
    -- Sales Reports
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'sales_reports', 'تقارير المبيعات', 'Sales Reports', '/crm/reports', 'عرض تقارير المبيعات', 'View sales reports', 'trending-up', 4, '1', 'SYSTEM', SYSDATE);
    
    COMMIT;
END;
/

-- =====================================================
-- 6. Insert Screens for POS System
-- =====================================================

DECLARE
    V_SYSTEM_ID NUMBER;
BEGIN
    SELECT ROW_ID INTO V_SYSTEM_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'pos';
    
    -- Point of Sale
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'point_of_sale', 'نقطة البيع', 'Point of Sale', '/pos/sale', 'شاشة نقطة البيع', 'Point of sale screen', 'monitor', 1, '1', 'SYSTEM', SYSDATE);
    
    -- Daily Sales
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'daily_sales', 'المبيعات اليومية', 'Daily Sales', '/pos/daily-sales', 'عرض المبيعات اليومية', 'View daily sales', 'calendar-check', 2, '1', 'SYSTEM', SYSDATE);
    
    -- Cash Drawer
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'cash_drawer', 'الصندوق', 'Cash Drawer', '/pos/cash-drawer', 'إدارة الصندوق', 'Manage cash drawer', 'briefcase', 3, '1', 'SYSTEM', SYSDATE);
    
    -- POS Reports
    INSERT INTO SYS_SCREEN (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'pos_reports', 'تقارير نقاط البيع', 'POS Reports', '/pos/reports', 'عرض تقارير نقاط البيع', 'View POS reports', 'activity', 4, '1', 'SYSTEM', SYSDATE);
    
    COMMIT;
END;
/

-- =====================================================
-- 7. Demo Company System Assignments
-- =====================================================

-- Note: This assumes you have companies with ROW_ID 1 and 2 from previous seed data
-- Adjust the company IDs based on your actual data

DECLARE
    V_ACCOUNTING_ID NUMBER;
    V_INVENTORY_ID NUMBER;
    V_HR_ID NUMBER;
    V_CRM_ID NUMBER;
    V_POS_ID NUMBER;
BEGIN
    -- Get system IDs
    SELECT ROW_ID INTO V_ACCOUNTING_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'accounting';
    SELECT ROW_ID INTO V_INVENTORY_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'inventory';
    SELECT ROW_ID INTO V_HR_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'hr';
    SELECT ROW_ID INTO V_CRM_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'crm';
    SELECT ROW_ID INTO V_POS_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'pos';
    
    -- Company 1: Allow Accounting, Inventory, CRM
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 1, V_ACCOUNTING_ID, '1', NULL, SYSDATE, 'Initial setup', 'SYSTEM', SYSDATE);
    
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 1, V_INVENTORY_ID, '1', NULL, SYSDATE, 'Initial setup', 'SYSTEM', SYSDATE);
    
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 1, V_CRM_ID, '1', NULL, SYSDATE, 'Initial setup', 'SYSTEM', SYSDATE);
    
    -- Company 1: Block HR, POS
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 1, V_HR_ID, '0', NULL, SYSDATE, 'Not subscribed', 'SYSTEM', SYSDATE);
    
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 1, V_POS_ID, '0', NULL, SYSDATE, 'Not subscribed', 'SYSTEM', SYSDATE);
    
    -- Company 2: Allow Accounting, POS
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 2, V_ACCOUNTING_ID, '1', NULL, SYSDATE, 'Initial setup', 'SYSTEM', SYSDATE);
    
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 2, V_POS_ID, '1', NULL, SYSDATE, 'Initial setup', 'SYSTEM', SYSDATE);
    
    -- Company 2: Block Inventory, HR, CRM
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 2, V_INVENTORY_ID, '0', NULL, SYSDATE, 'Not subscribed', 'SYSTEM', SYSDATE);
    
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 2, V_HR_ID, '0', NULL, SYSDATE, 'Not subscribed', 'SYSTEM', SYSDATE);
    
    INSERT INTO SYS_COMPANY_SYSTEM (ROW_ID, COMPANY_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY_SYSTEM.NEXTVAL, 2, V_CRM_ID, '0', NULL, SYSDATE, 'Not subscribed', 'SYSTEM', SYSDATE);
    
    COMMIT;
END;
/

-- =====================================================
-- 8. Demo Role Screen Permissions
-- =====================================================

-- Note: This assumes you have roles from previous seed data
-- Example: Assign permissions to role with ROW_ID = 7 (from test data)

DECLARE
    V_ROLE_ID NUMBER := 7;  -- Adjust based on your actual role ID
    V_SCREEN_ID NUMBER;
BEGIN
    -- Give full permissions to Invoices screen
    SELECT ROW_ID INTO V_SCREEN_ID FROM SYS_SCREEN WHERE SCREEN_CODE = 'invoices';
    INSERT INTO SYS_ROLE_SCREEN_PERMISSION (ROW_ID, ROLE_ID, SCREEN_ID, CAN_VIEW, CAN_INSERT, CAN_UPDATE, CAN_DELETE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_ROLE_SCREEN_PERM.NEXTVAL, V_ROLE_ID, V_SCREEN_ID, '1', '1', '1', '1', 'SYSTEM', SYSDATE);
    
    -- Give view-only permissions to Financial Reports
    SELECT ROW_ID INTO V_SCREEN_ID FROM SYS_SCREEN WHERE SCREEN_CODE = 'financial_reports';
    INSERT INTO SYS_ROLE_SCREEN_PERMISSION (ROW_ID, ROLE_ID, SCREEN_ID, CAN_VIEW, CAN_INSERT, CAN_UPDATE, CAN_DELETE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_ROLE_SCREEN_PERM.NEXTVAL, V_ROLE_ID, V_SCREEN_ID, '1', '0', '0', '0', 'SYSTEM', SYSDATE);
    
    -- Give view and insert permissions to Products
    SELECT ROW_ID INTO V_SCREEN_ID FROM SYS_SCREEN WHERE SCREEN_CODE = 'products';
    INSERT INTO SYS_ROLE_SCREEN_PERMISSION (ROW_ID, ROLE_ID, SCREEN_ID, CAN_VIEW, CAN_INSERT, CAN_UPDATE, CAN_DELETE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_ROLE_SCREEN_PERM.NEXTVAL, V_ROLE_ID, V_SCREEN_ID, '1', '1', '0', '0', 'SYSTEM', SYSDATE);
    
    COMMIT;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('Role or screen not found. Adjust IDs in script.');
        ROLLBACK;
END;
/

-- =====================================================
-- 9. Demo User Role Assignments
-- =====================================================

-- Note: This assumes you have users from previous seed data
-- Example: Assign role to user with ROW_ID = 0 (admin user)

DECLARE
    V_USER_ID NUMBER := 0;  -- Adjust based on your actual user ID
    V_ROLE_ID NUMBER := 7;  -- Adjust based on your actual role ID
BEGIN
    INSERT INTO SYS_USER_ROLE (ROW_ID, USER_ID, ROLE_ID, ASSIGNED_BY, ASSIGNED_DATE, CREATION_USER, CREATION_DATE)
    VALUES (SEQ_SYS_USER_ROLE.NEXTVAL, V_USER_ID, V_ROLE_ID, NULL, SYSDATE, 'SYSTEM', SYSDATE);
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        DBMS_OUTPUT.PUT_LINE('User role assignment already exists.');
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('User or role not found. Adjust IDs in script.');
        ROLLBACK;
END;
/

COMMIT;

-- =====================================================
-- Script Execution Complete
-- =====================================================

-- Summary of inserted data:
-- - 5 Systems (Accounting, Inventory, HR, CRM, POS)
-- - 24 Screens across all systems
-- - Demo company system assignments for 2 companies
-- - Demo role screen permissions
-- - Demo user role assignments
