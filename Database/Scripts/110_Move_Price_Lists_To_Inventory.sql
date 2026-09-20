-- ============================================================================
-- SCRIPT 110: Move Price Lists from POS to Inventory Module (Tables & Constraints)
-- ============================================================================

DECLARE
    v_count NUMBER;
BEGIN
    -- 1. Rename POS_PRICE_LIST to INV_PRICE_LIST
    SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'POS_PRICE_LIST';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE POS_PRICE_LIST RENAME TO INV_PRICE_LIST';
        DBMS_OUTPUT.PUT_LINE('Renamed POS_PRICE_LIST to INV_PRICE_LIST');
    END IF;

    -- 2. Rename POS_PRICE_LIST_ITEM to INV_PRICE_LIST_ITEM
    SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'POS_PRICE_LIST_ITEM';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE POS_PRICE_LIST_ITEM RENAME TO INV_PRICE_LIST_ITEM';
        DBMS_OUTPUT.PUT_LINE('Renamed POS_PRICE_LIST_ITEM to INV_PRICE_LIST_ITEM');
    END IF;
END;
/
