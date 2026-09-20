-- ============================================================================
-- SCRIPT 109: Move Modifiers from POS to Inventory Module (Tables & Constraints)
-- ============================================================================

DECLARE
    v_count NUMBER;
BEGIN
    -- 1. Rename POS_MODIFIER_GROUP to INV_MODIFIER_GROUP
    SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'POS_MODIFIER_GROUP';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE POS_MODIFIER_GROUP RENAME TO INV_MODIFIER_GROUP';
        DBMS_OUTPUT.PUT_LINE('Renamed POS_MODIFIER_GROUP to INV_MODIFIER_GROUP');
    END IF;

    -- 2. Rename POS_MODIFIER_OPTION to INV_MODIFIER_OPTION
    SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'POS_MODIFIER_OPTION';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE POS_MODIFIER_OPTION RENAME TO INV_MODIFIER_OPTION';
        DBMS_OUTPUT.PUT_LINE('Renamed POS_MODIFIER_OPTION to INV_MODIFIER_OPTION');
    END IF;

    -- 3. Rename POS_ITEM_MODIFIER_GROUP to INV_ITEM_MODIFIER_GROUP
    SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'POS_ITEM_MODIFIER_GROUP';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE POS_ITEM_MODIFIER_GROUP RENAME TO INV_ITEM_MODIFIER_GROUP';
        DBMS_OUTPUT.PUT_LINE('Renamed POS_ITEM_MODIFIER_GROUP to INV_ITEM_MODIFIER_GROUP');
    END IF;
END;
/
