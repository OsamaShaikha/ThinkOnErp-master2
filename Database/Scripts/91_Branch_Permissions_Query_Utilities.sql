-- =====================================================
-- Branch Permissions - Query Scripts & Utilities
-- Helper queries for branch permissions management
-- =====================================================

-- =====================================================
-- 1. VIEW ALL BRANCH SYSTEMS WITH DETAILS
-- Shows all systems assigned to each branch with audit trail
-- =====================================================
SELECT 
	bs.ROW_ID,
	'SYSTEM' as PERMISSION_TYPE,
	br.BRANCH_NAME,
	br.ROW_ID as BRANCH_ID,
	sys.SYSTEM_NAME,
	sys.SYSTEM_CODE,
	sys.ROW_ID as SYSTEM_ID,
	CASE bs.IS_ALLOWED WHEN '1' THEN 'Allowed' ELSE 'Blocked' END as ACCESS_STATUS,
	sa.ROW_DESC_E as GRANTED_BY_NAME,
	bs.GRANTED_DATE,
	bs.REVOKED_DATE,
	bs.NOTES,
	bs.CREATION_USER,
	bs.CREATION_DATE,
	bs.UPDATE_DATE
FROM SYS_BRANCH_SYSTEM bs
INNER JOIN SYS_BRANCH br ON bs.BRANCH_ID = br.ROW_ID
INNER JOIN SYS_SYSTEM sys ON bs.SYSTEM_ID = sys.ROW_ID
LEFT JOIN SYS_SUPER_ADMIN sa ON bs.GRANTED_BY = sa.ROW_ID
ORDER BY br.BRANCH_NAME, sys.DISPLAY_ORDER;

-- =====================================================
-- 2. VIEW ALL BRANCH SCREENS WITH DETAILS
-- Shows all screens assigned to each branch with hierarchy
-- =====================================================
SELECT 
	bscr.ROW_ID,
	'SCREEN' as PERMISSION_TYPE,
	br.BRANCH_NAME,
	br.ROW_ID as BRANCH_ID,
	sys.SYSTEM_NAME,
	sys.SYSTEM_CODE,
	sys.ROW_ID as SYSTEM_ID,
	scr.SCREEN_NAME,
	scr.SCREEN_CODE,
	scr.ROW_ID as SCREEN_ID,
	scr.ROUTE,
	CASE bscr.IS_ALLOWED WHEN '1' THEN 'Allowed' ELSE 'Blocked' END as ACCESS_STATUS,
	sa.ROW_DESC_E as GRANTED_BY_NAME,
	bscr.GRANTED_DATE,
	bscr.REVOKED_DATE,
	bscr.NOTES,
	bscr.CREATION_USER,
	bscr.CREATION_DATE,
	bscr.UPDATE_DATE
FROM SYS_BRANCH_SCREEN bscr
INNER JOIN SYS_BRANCH br ON bscr.BRANCH_ID = br.ROW_ID
INNER JOIN SYS_SCREEN scr ON bscr.SCREEN_ID = scr.ROW_ID
INNER JOIN SYS_SYSTEM sys ON scr.SYSTEM_ID = sys.ROW_ID
LEFT JOIN SYS_SUPER_ADMIN sa ON bscr.GRANTED_BY = sa.ROW_ID
ORDER BY br.BRANCH_NAME, sys.DISPLAY_ORDER, scr.DISPLAY_ORDER;

-- =====================================================
-- 3. BRANCHES WITHOUT ACCESS TO SPECIFIC SYSTEM
-- Find all branches that don't have access to a system (adjust system ID as needed)
-- =====================================================
SELECT 
	br.ROW_ID as BRANCH_ID,
	br.BRANCH_NAME,
	sys.ROW_ID as SYSTEM_ID,
	sys.SYSTEM_NAME,
	'NO ACCESS' as STATUS
FROM SYS_BRANCH br
CROSS JOIN SYS_SYSTEM sys
WHERE sys.IS_ACTIVE = '1'
AND NOT EXISTS (
	SELECT 1 FROM SYS_BRANCH_SYSTEM bs
	WHERE bs.BRANCH_ID = br.ROW_ID
	AND bs.SYSTEM_ID = sys.ROW_ID
	AND bs.IS_ALLOWED = '1'
)
ORDER BY br.BRANCH_NAME, sys.DISPLAY_ORDER;

-- =====================================================
-- 4. PERMISSION AUDIT TRAIL FOR A SPECIFIC BRANCH
-- Shows the complete permission history for a branch
-- =====================================================
SELECT 
	br.BRANCH_NAME,
	'SYSTEM' as OBJECT_TYPE,
	sys.SYSTEM_NAME as OBJECT_NAME,
	CASE bs.IS_ALLOWED WHEN '1' THEN 'GRANTED' ELSE 'REVOKED' END as ACTION,
	sa.ROW_DESC_E as ACTED_BY,
	CASE 
		WHEN bs.IS_ALLOWED = '1' THEN bs.GRANTED_DATE 
		ELSE bs.REVOKED_DATE 
	END as ACTION_DATE,
	bs.NOTES
FROM SYS_BRANCH_SYSTEM bs
INNER JOIN SYS_BRANCH br ON bs.BRANCH_ID = br.ROW_ID
INNER JOIN SYS_SYSTEM sys ON bs.SYSTEM_ID = sys.ROW_ID
LEFT JOIN SYS_SUPER_ADMIN sa ON bs.GRANTED_BY = sa.ROW_ID
-- WHERE br.ROW_ID = ? -- Uncomment and add branch ID to filter
ORDER BY ACTION_DATE DESC;

-- =====================================================
-- 5. FIND ALL ALLOWED SYSTEMS FOR A BRANCH
-- Quickly find systems a specific branch can access
-- =====================================================
SELECT DISTINCT
	sys.ROW_ID as SYSTEM_ID,
	sys.SYSTEM_CODE,
	sys.SYSTEM_NAME,
	sys.SYSTEM_NAME_E,
	sys.ICON,
	sys.DISPLAY_ORDER
FROM SYS_BRANCH_SYSTEM bs
INNER JOIN SYS_SYSTEM sys ON bs.SYSTEM_ID = sys.ROW_ID
WHERE bs.BRANCH_ID = 1 -- Change this to the desired branch ID
AND bs.IS_ALLOWED = '1'
ORDER BY sys.DISPLAY_ORDER;

-- =====================================================
-- 6. FIND ALL ALLOWED SCREENS FOR A BRANCH
-- Quickly find screens a specific branch can access
-- =====================================================
SELECT DISTINCT
	scr.ROW_ID as SCREEN_ID,
	scr.SCREEN_CODE,
	scr.SCREEN_NAME,
	scr.ROUTE,
	sys.SYSTEM_ID,
	sys.SYSTEM_NAME,
	sys.SYSTEM_CODE,
	scr.DISPLAY_ORDER
FROM SYS_BRANCH_SCREEN bscr
INNER JOIN SYS_SCREEN scr ON bscr.SCREEN_ID = scr.ROW_ID
INNER JOIN SYS_SYSTEM sys ON scr.SYSTEM_ID = sys.ROW_ID
WHERE bscr.BRANCH_ID = 1 -- Change this to the desired branch ID
AND bscr.IS_ALLOWED = '1'
AND scr.IS_ACTIVE = '1'
ORDER BY sys.DISPLAY_ORDER, scr.DISPLAY_ORDER;

-- =====================================================
-- 7. SYSTEM ACCESS SUMMARY
-- Get a summary of which branches have access to each system
-- =====================================================
SELECT 
	sys.ROW_ID as SYSTEM_ID,
	sys.SYSTEM_NAME,
	COUNT(DISTINCT CASE WHEN bs.IS_ALLOWED = '1' THEN bs.BRANCH_ID END) as BRANCHES_WITH_ACCESS,
	COUNT(DISTINCT CASE WHEN bs.IS_ALLOWED = '0' THEN bs.BRANCH_ID END) as BRANCHES_WITHOUT_ACCESS,
	COUNT(DISTINCT br.ROW_ID) - COALESCE(COUNT(DISTINCT bs.BRANCH_ID), 0) as BRANCHES_NEVER_ASSIGNED
FROM SYS_SYSTEM sys
CROSS JOIN SYS_BRANCH br
LEFT JOIN SYS_BRANCH_SYSTEM bs ON sys.ROW_ID = bs.SYSTEM_ID AND br.ROW_ID = bs.BRANCH_ID
WHERE sys.IS_ACTIVE = '1'
GROUP BY sys.ROW_ID, sys.SYSTEM_NAME
ORDER BY BRANCHES_WITH_ACCESS DESC;

-- =====================================================
-- 8. RECENT PERMISSION CHANGES (Last 30 days)
-- Shows all permission grants and revokes in the last month
-- =====================================================
SELECT 
	br.BRANCH_NAME,
	'SYSTEM' as OBJECT_TYPE,
	sys.SYSTEM_NAME as OBJECT_NAME,
	CASE bs.IS_ALLOWED WHEN '1' THEN 'GRANTED' ELSE 'REVOKED' END as ACTION,
	bs.UPDATE_USER as CHANGED_BY,
	bs.UPDATE_DATE as CHANGED_DATE
FROM SYS_BRANCH_SYSTEM bs
INNER JOIN SYS_BRANCH br ON bs.BRANCH_ID = br.ROW_ID
INNER JOIN SYS_SYSTEM sys ON bs.SYSTEM_ID = sys.ROW_ID
WHERE bs.UPDATE_DATE >= SYSDATE - 30
UNION ALL
SELECT 
	br.BRANCH_NAME,
	'SCREEN' as OBJECT_TYPE,
	scr.SCREEN_NAME as OBJECT_NAME,
	CASE bscr.IS_ALLOWED WHEN '1' THEN 'GRANTED' ELSE 'REVOKED' END as ACTION,
	bscr.UPDATE_USER as CHANGED_BY,
	bscr.UPDATE_DATE as CHANGED_DATE
FROM SYS_BRANCH_SCREEN bscr
INNER JOIN SYS_BRANCH br ON bscr.BRANCH_ID = br.ROW_ID
INNER JOIN SYS_SCREEN scr ON bscr.SCREEN_ID = scr.ROW_ID
WHERE bscr.UPDATE_DATE >= SYSDATE - 30
ORDER BY CHANGED_DATE DESC;

-- =====================================================
-- 9. VALIDATE PERMISSION INTEGRITY
-- Check for orphaned records or missing mappings
-- =====================================================
SELECT 
	bs.ROW_ID,
	bs.BRANCH_ID,
	bs.SYSTEM_ID,
	CASE 
		WHEN br.ROW_ID IS NULL THEN 'ORPHANED: Branch does not exist'
		WHEN sys.ROW_ID IS NULL THEN 'ORPHANED: System does not exist'
		ELSE 'OK'
	END as INTEGRITY_STATUS
FROM SYS_BRANCH_SYSTEM bs
LEFT JOIN SYS_BRANCH br ON bs.BRANCH_ID = br.ROW_ID
LEFT JOIN SYS_SYSTEM sys ON bs.SYSTEM_ID = sys.ROW_ID
WHERE br.ROW_ID IS NULL OR sys.ROW_ID IS NULL;

-- =====================================================
-- 10. PERMISSION STATISTICS BY BRANCH
-- Shows permission counts and statistics
-- =====================================================
SELECT 
	br.ROW_ID as BRANCH_ID,
	br.BRANCH_NAME,
	COUNT(DISTINCT CASE WHEN bs.IS_ALLOWED = '1' THEN bs.SYSTEM_ID END) as SYSTEMS_ALLOWED,
	COUNT(DISTINCT CASE WHEN bs.IS_ALLOWED = '0' THEN bs.SYSTEM_ID END) as SYSTEMS_BLOCKED,
	COUNT(DISTINCT CASE WHEN bscr.IS_ALLOWED = '1' THEN bscr.SCREEN_ID END) as SCREENS_ALLOWED,
	COUNT(DISTINCT CASE WHEN bscr.IS_ALLOWED = '0' THEN bscr.SCREEN_ID END) as SCREENS_BLOCKED
FROM SYS_BRANCH br
LEFT JOIN SYS_BRANCH_SYSTEM bs ON br.ROW_ID = bs.BRANCH_ID
LEFT JOIN SYS_BRANCH_SCREEN bscr ON br.ROW_ID = bscr.BRANCH_ID
GROUP BY br.ROW_ID, br.BRANCH_NAME
ORDER BY br.BRANCH_NAME;
