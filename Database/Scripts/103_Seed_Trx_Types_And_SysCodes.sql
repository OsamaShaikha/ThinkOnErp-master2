-- =============================================================================
-- ThinkOn ERP — Seed Script: TRX_DOC_TYPE & TRX_TRANSACTION_TYPE
-- Script 103: Populates master configuration for documents and transactions
-- =============================================================================

DECLARE
    PROCEDURE upsert_doc_type(
        p_code NUMBER, p_key VARCHAR2, p_name_ar VARCHAR2, p_name_en VARCHAR2,
        p_mod VARCHAR2, p_prefix VARCHAR2, p_reset VARCHAR2
    ) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM TRX_DOC_TYPE WHERE TYPE_CODE = p_code;
        IF v_count = 0 THEN
            INSERT INTO TRX_DOC_TYPE (
                TYPE_CODE, TYPE_KEY, TYPE_NAME_AR, TYPE_NAME_EN,
                MODULE_CODE, DOC_PREFIX, RESET_POLICY, IS_SYSTEM_RESERVED, IS_ACTIVE,
                CREATION_USER, CREATION_DATE
            ) VALUES (
                p_code, p_key, p_name_ar, p_name_en,
                p_mod, p_prefix, p_reset, 1, 1,
                'SEED_TRX', SYSTIMESTAMP
            );
        END IF;
    END;

    PROCEDURE upsert_trx_type(
        p_code NUMBER, p_doc_code NUMBER, p_key VARCHAR2, p_name_ar VARCHAR2, p_name_en VARCHAR2,
        p_aff_stk NUMBER, p_dir NUMBER, p_req_wh NUMBER, p_aff_gl NUMBER, p_aff_party NUMBER,
        p_rule VARCHAR2, p_req_party NUMBER, p_req_price NUMBER, p_req_cost NUMBER
    ) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM TRX_TRANSACTION_TYPE WHERE TRX_CODE = p_code;
        IF v_count = 0 THEN
            INSERT INTO TRX_TRANSACTION_TYPE (
                TRX_CODE, DOC_TYPE_CODE, TRX_KEY, TRX_NAME_AR, TRX_NAME_EN,
                AFFECTS_STOCK, STOCK_DIRECTION, REQUIRES_WAREHOUSE, AFFECTS_GL, AFFECTS_PARTY_BALANCE,
                POSTING_RULE_CODE, REQUIRES_PARTY, REQUIRES_PRICE, REQUIRES_COST,
                IS_ACTIVE, CREATION_USER, CREATION_DATE
            ) VALUES (
                p_code, p_doc_code, p_key, p_name_ar, p_name_en,
                p_aff_stk, p_dir, p_req_wh, p_aff_gl, p_aff_party,
                p_rule, p_req_party, p_req_price, p_req_cost,
                1, 'SEED_TRX', SYSTIMESTAMP
            );
        END IF;
    END;
BEGIN
    -- 1. Document Types (TRX_DOC_TYPE)
    upsert_doc_type(100, 'SALES_INVOICE',   'فواتير المبيعات',       'Sales Invoices',          'SALES',       'SINV-', 'YEARLY');
    upsert_doc_type(150, 'SALES_RETURN',    'مردودات المبيعات',      'Sales Returns',           'SALES',       'SRET-', 'YEARLY');
    upsert_doc_type(200, 'PURCHASE_BILL',   'فواتير المشتريات',     'Purchase Bills',          'PURCHASING',  'PINV-', 'YEARLY');
    upsert_doc_type(250, 'PURCHASE_RETURN', 'مردودات المشتريات',    'Purchase Returns',        'PURCHASING',  'PRET-', 'YEARLY');
    upsert_doc_type(300, 'STOCK_VOUCHER',   'سندات المخزون',         'Stock Vouchers',          'INVENTORY',   'STK-',  'YEARLY');
    upsert_doc_type(350, 'STOCK_TRANSFER',  'التحويلات المخزنية',    'Stock Transfers',         'INVENTORY',   'TR-',   'YEARLY');
    upsert_doc_type(400, 'PRE_SALES',       'عروض وأوامر البيع',     'Quotations & Sales Orders','SALES',      'QUOT-', 'YEARLY');
    upsert_doc_type(450, 'PRE_PURCHASE',    'طلبات وأوامر الشراء',   'Purchase Requisitions & PO','PURCHASING','PO-',   'YEARLY');

    -- 2. Transaction Types (TRX_TRANSACTION_TYPE)
    -- Sales (1000s)
    upsert_trx_type(1001, 100, 'LOCAL_CASH_SALES',   'مبيعات محلية نقدية',    'Local Cash Sales',         1, -1, 1, 1, 0, 'INV_COGS', 0, 1, 1);
    upsert_trx_type(1002, 100, 'LOCAL_CREDIT_SALES', 'مبيعات محلية ذمم/آجل',  'Local Credit Sales',       1, -1, 1, 1, 1, 'INV_COGS', 1, 1, 1);
    upsert_trx_type(1003, 100, 'EXPORT_SALES',       'مبيعات تصدير خارجي',   'Export Sales',             1, -1, 1, 1, 1, 'INV_COGS', 1, 1, 1);
    upsert_trx_type(1004, 100, 'POS_SALES',          'مبيعات نقاط بيع تجزئة', 'POS Retail Sales',         1, -1, 1, 1, 0, 'INV_COGS', 0, 1, 1);

    -- Sales Returns (1500s)
    upsert_trx_type(1501, 150, 'SALES_RETURN_RESTOCK','مردود مبيعات سليم',     'Sales Return (Restock)',   1,  1, 1, 1, 1, 'SALES_RETURN_GOOD', 1, 1, 1);
    upsert_trx_type(1502, 150, 'SALES_RETURN_SCRAP',  'مردود مبيعات تالف/هالك','Sales Return (Scrap)',     0,  0, 0, 1, 1, 'SALES_RETURN_DAMAGED', 1, 1, 0);

    -- Purchases (2000s)
    upsert_trx_type(2001, 200, 'LOCAL_PURCHASE',     'مشتريات محلية بضاعة',   'Local Purchases',          1,  1, 1, 1, 1, 'INV_RECEIPT', 1, 1, 1);
    upsert_trx_type(2003, 200, 'IMPORT_PURCHASE',    'مشتريات استيراد خارجي', 'Import Purchases',         1,  1, 1, 1, 1, 'INV_RECEIPT', 1, 1, 1);

    -- Purchase Returns (2500s)
    upsert_trx_type(2501, 250, 'PURCHASE_RETURN',    'مردود مشتريات لمورد',   'Purchase Return',          1, -1, 1, 1, 1, 'INV_RECEIPT', 1, 1, 1);

    -- Warehouse Movements (3000s)
    upsert_trx_type(3001, 300, 'OPENING_STOCK',      'سند إدخال رصيد افتتاحي','Opening Stock Inbound',    1,  1, 1, 1, 0, 'INV_OPENING_BALANCE', 0, 0, 1);
    upsert_trx_type(3002, 300, 'STOCK_SURPLUS',      'سند إدخال تسوية زيادة', 'Stock Count Surplus',      1,  1, 1, 1, 0, 'INV_ADJUST_SURPLUS', 0, 0, 1);
    upsert_trx_type(3003, 300, 'FREE_SAMPLES_IN',    'سند إدخال عينات وهدايا','Free Samples Inbound',     1,  1, 1, 1, 0, 'INV_RECEIPT', 0, 0, 1);
    upsert_trx_type(3011, 300, 'STOCK_SHORTAGE',     'سند إخراج تسوية عجز',   'Stock Count Shortage',     1, -1, 1, 1, 0, 'INV_ADJUST_SHORTAGE', 0, 0, 1);
    upsert_trx_type(3012, 300, 'SCRAP_WRITEOFF',     'سند إخراج إتلاف وهالك', 'Scrap & Damage Write-Off', 1, -1, 1, 1, 0, 'INV_WRITEOFF', 0, 0, 1);
    upsert_trx_type(3014, 300, 'INTERNAL_USE',       'سند إخراج استهلاك أقسام','Internal Consumption',    1, -1, 1, 1, 0, 'INV_WRITEOFF', 0, 0, 1);

    -- Transfers (3500s)
    upsert_trx_type(3501, 350, 'INTERNAL_TRANSFER',  'تحويل بين مستودعات',    'Internal Warehouse Transfer',1, 0, 1, 1, 0, 'BRANCH_TRANSFER', 0, 0, 1);

    -- Pre-Transactions (4000s)
    upsert_trx_type(4001, 400, 'SALES_QUOTATION',    'عرض أسعار لعميل',       'Sales Quotation',          0,  0, 0, 0, 0, NULL, 1, 1, 0);
    upsert_trx_type(4004, 450, 'PURCHASE_ORDER',     'أمر شراء لمورد',        'Purchase Order',           0,  0, 0, 0, 0, NULL, 1, 1, 0);

    COMMIT;
END;
/
