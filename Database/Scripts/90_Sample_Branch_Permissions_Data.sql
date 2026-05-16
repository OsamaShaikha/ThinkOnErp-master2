-- =====================================================
-- Sample Data for Branch Permissions
-- This script populates test data for branch permissions
-- =====================================================

-- =====================================================
-- Note: Execute after all table creation and procedure scripts
-- =====================================================

-- Insert sample branch system permissions
INSERT INTO SYS_BRANCH_SYSTEM (
	ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	NOTES, CREATION_USER, CREATION_DATE
) VALUES (
	SEQ_SYS_BRANCH_SYSTEM.NEXTVAL, 1, 1, '1', 1, SYSDATE,
	'Accounting system granted to Main Branch', 'admin', SYSDATE
);

INSERT INTO SYS_BRANCH_SYSTEM (
	ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	NOTES, CREATION_USER, CREATION_DATE
) VALUES (
	SEQ_SYS_BRANCH_SYSTEM.NEXTVAL, 1, 2, '1', 1, SYSDATE,
	'Inventory system granted to Main Branch', 'admin', SYSDATE
);

INSERT INTO SYS_BRANCH_SYSTEM (
	ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	NOTES, CREATION_USER, CREATION_DATE
) VALUES (
	SEQ_SYS_BRANCH_SYSTEM.NEXTVAL, 1, 3, '1', 1, SYSDATE,
	'HR system granted to Main Branch', 'admin', SYSDATE
);

-- Insert sample branch screen permissions for branch 1
INSERT INTO SYS_BRANCH_SCREEN (
	ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	NOTES, CREATION_USER, CREATION_DATE
) VALUES (
	SEQ_SYS_BRANCH_SCREEN.NEXTVAL, 1, 1, '1', 1, SYSDATE,
	'Invoices list screen granted', 'admin', SYSDATE
);

INSERT INTO SYS_BRANCH_SCREEN (
	ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	NOTES, CREATION_USER, CREATION_DATE
) VALUES (
	SEQ_SYS_BRANCH_SCREEN.NEXTVAL, 1, 2, '1', 1, SYSDATE,
	'Invoices create screen granted', 'admin', SYSDATE
);

INSERT INTO SYS_BRANCH_SCREEN (
	ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	NOTES, CREATION_USER, CREATION_DATE
) VALUES (
	SEQ_SYS_BRANCH_SCREEN.NEXTVAL, 1, 3, '1', 1, SYSDATE,
	'Inventory items list screen granted', 'admin', SYSDATE
);

INSERT INTO SYS_BRANCH_SCREEN (
	ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	NOTES, CREATION_USER, CREATION_DATE
) VALUES (
	SEQ_SYS_BRANCH_SCREEN.NEXTVAL, 1, 4, '1', 1, SYSDATE,
	'HR employees list screen granted', 'admin', SYSDATE
);

-- Example of revoked permission
INSERT INTO SYS_BRANCH_SYSTEM (
	ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	REVOKED_DATE, NOTES, CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
) VALUES (
	SEQ_SYS_BRANCH_SYSTEM.NEXTVAL, 2, 4, '0', 1, SYSDATE - 30, SYSDATE - 5,
	'CRM system revoked from Branch 2', 'admin', SYSDATE - 30, 'admin', SYSDATE - 5
);

-- Example of revoked screen permission
INSERT INTO SYS_BRANCH_SCREEN (
	ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED, GRANTED_BY, GRANTED_DATE, 
	REVOKED_DATE, NOTES, CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
) VALUES (
	SEQ_SYS_BRANCH_SCREEN.NEXTVAL, 2, 5, '0', 1, SYSDATE - 15, SYSDATE - 2,
	'Reports screen revoked from Branch 2', 'admin', SYSDATE - 15, 'admin', SYSDATE - 2
);

COMMIT;

-- =====================================================
-- Verification Queries (can be used to verify inserted data)
-- =====================================================

-- Verify branch system permissions
-- SELECT * FROM SYS_BRANCH_SYSTEM WHERE BRANCH_ID = 1;

-- Verify branch screen permissions
-- SELECT * FROM SYS_BRANCH_SCREEN WHERE BRANCH_ID = 1;

-- Verify systems with their branch permissions
-- SELECT bs.*, sys.SYSTEM_NAME 
-- FROM SYS_BRANCH_SYSTEM bs
-- JOIN SYS_SYSTEM sys ON bs.SYSTEM_ID = sys.ROW_ID
-- WHERE bs.BRANCH_ID = 1
-- ORDER BY sys.DISPLAY_ORDER;

-- Verify screens with their systems
-- SELECT bscr.*, scr.SCREEN_NAME, sys.SYSTEM_NAME 
-- FROM SYS_BRANCH_SCREEN bscr
-- JOIN SYS_SCREEN scr ON bscr.SCREEN_ID = scr.ROW_ID
-- JOIN SYS_SYSTEM sys ON scr.SYSTEM_ID = sys.ROW_ID
-- WHERE bscr.BRANCH_ID = 1
-- ORDER BY sys.DISPLAY_ORDER, scr.DISPLAY_ORDER;
