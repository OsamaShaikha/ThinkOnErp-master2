-- =============================================================================
-- Script 108: Rename INV_ITEM_GROUP to INV_ITEM_CATEGORY and update columns
-- =============================================================================

DECLARE
    v_cnt NUMBER;
BEGIN
    -- 1. Rename table INV_ITEM_GROUP to INV_ITEM_CATEGORY if it exists
    SELECT COUNT(*) INTO v_cnt FROM user_tables WHERE table_name = 'INV_ITEM_GROUP';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM_GROUP RENAME TO INV_ITEM_CATEGORY';
    END IF;

    -- 2. Rename columns in INV_ITEM_CATEGORY
    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM_CATEGORY' AND column_name = 'PARENT_GROUP_ID';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM_CATEGORY RENAME COLUMN PARENT_GROUP_ID TO PARENT_CATEGORY_ID';
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM_CATEGORY' AND column_name = 'GROUP_CODE';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM_CATEGORY RENAME COLUMN GROUP_CODE TO CATEGORY_CODE';
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM_CATEGORY' AND column_name = 'GROUP_NAME_LOCAL';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM_CATEGORY RENAME COLUMN GROUP_NAME_LOCAL TO CATEGORY_NAME_LOCAL';
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM_CATEGORY' AND column_name = 'GROUP_NAME_AR';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM_CATEGORY RENAME COLUMN GROUP_NAME_AR TO CATEGORY_NAME_LOCAL';
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM_CATEGORY' AND column_name = 'GROUP_NAME_EN';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM_CATEGORY RENAME COLUMN GROUP_NAME_EN TO CATEGORY_NAME_EN';
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM_CATEGORY' AND column_name = 'GROUP_LEVEL';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM_CATEGORY RENAME COLUMN GROUP_LEVEL TO CATEGORY_LEVEL';
    END IF;

    -- 3. Rename columns in INV_ITEM
    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM' AND column_name = 'MAIN_GROUP_ID';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM RENAME COLUMN MAIN_GROUP_ID TO MAIN_CATEGORY_ID';
    END IF;

    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'INV_ITEM' AND column_name = 'SUB_GROUP_ID';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INV_ITEM RENAME COLUMN SUB_GROUP_ID TO SUB_CATEGORY_ID';
    END IF;

    -- 4. Rename column in POS_PRINTER_ROUTING
    SELECT COUNT(*) INTO v_cnt FROM user_tab_columns WHERE table_name = 'POS_PRINTER_ROUTING' AND column_name = 'ITEM_GROUP_ID';
    IF v_cnt > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE POS_PRINTER_ROUTING RENAME COLUMN ITEM_GROUP_ID TO ITEM_CATEGORY_ID';
    END IF;

END;
/
