-- =====================================================
-- ThinkOnErp - Inventory Permissions Seed Script
-- =====================================================

DECLARE
    V_SYSTEM_ID NUMBER;
BEGIN
    -- Ensure inventory system exists, else get its ID
    BEGIN
        SELECT ROW_ID INTO V_SYSTEM_ID FROM SYS_SYSTEM WHERE SYSTEM_CODE = 'inventory';
    EXCEPTION WHEN NO_DATA_FOUND THEN
        INSERT INTO SYS_SYSTEM (ROW_ID, SYSTEM_CODE, SYSTEM_NAME, SYSTEM_NAME_E, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SYSTEM.NEXTVAL, 'inventory', 'نظام المخزون', 'Inventory System', 'إدارة المنتجات والمخازن', 'Manage products and warehouses', 'warehouse', 2, '1', 'SYSTEM', SYSDATE)
        RETURNING ROW_ID INTO V_SYSTEM_ID;
    END;
    
    -- Products
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'products' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'products', 'المنتجات', 'Products', '/inventory/products', 'إدارة المنتجات', 'Manage products', 'package', 1, '1', 'SYSTEM', SYSDATE);

    -- Warehouses
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'warehouses' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'warehouses', 'المخازن', 'Warehouses', '/inventory/warehouses', 'إدارة المخازن', 'Manage warehouses', 'home', 2, '1', 'SYSTEM', SYSDATE);
        
    -- Stock Movements
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'stock_movements' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'stock_movements', 'حركات المخزون', 'Stock Movements', '/inventory/stock-movements', 'إدارة حركات المخزون', 'Manage stock movements', 'truck', 3, '1', 'SYSTEM', SYSDATE);
        
    -- Transfers
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'transfers' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'transfers', 'التحويلات', 'Transfers', '/inventory/transfers', 'إدارة التحويلات', 'Manage transfers', 'repeat', 4, '1', 'SYSTEM', SYSDATE);
        
    -- Adjustments
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'adjustments' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'adjustments', 'التسويات', 'Adjustments', '/inventory/adjustments', 'إدارة التسويات', 'Manage adjustments', 'sliders', 5, '1', 'SYSTEM', SYSDATE);
        
    -- Counting
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'counting' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'counting', 'الجرد', 'Counting', '/inventory/counting', 'إدارة الجرد', 'Manage counting', 'check-square', 6, '1', 'SYSTEM', SYSDATE);
        
    -- Opening Balance
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'opening_balance' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'opening_balance', 'الرصيد الافتتاحي', 'Opening Balance', '/inventory/opening-balance', 'إدارة الرصيد الافتتاحي', 'Manage opening balance', 'play-circle', 7, '1', 'SYSTEM', SYSDATE);
        
    -- Reports
    MERGE INTO SYS_SCREEN tgt
    USING (SELECT 'inventory_reports' AS SCREEN_CODE FROM DUAL) src
    ON (tgt.SCREEN_CODE = src.SCREEN_CODE)
    WHEN NOT MATCHED THEN
        INSERT (ROW_ID, SYSTEM_ID, PARENT_SCREEN_ID, SCREEN_CODE, SCREEN_NAME, SCREEN_NAME_E, ROUTE, DESCRIPTION, DESCRIPTION_E, ICON, DISPLAY_ORDER, IS_ACTIVE, CREATION_USER, CREATION_DATE)
        VALUES (SEQ_SYS_SCREEN.NEXTVAL, V_SYSTEM_ID, NULL, 'inventory_reports', 'تقارير المخزون', 'Inventory Reports', '/inventory/reports', 'عرض تقارير المخزون', 'View inventory reports', 'pie-chart', 8, '1', 'SYSTEM', SYSDATE);

    COMMIT;
END;
/
