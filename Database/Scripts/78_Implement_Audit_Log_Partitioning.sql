-- =====================================================
-- Script: 78_Implement_Audit_Log_Partitioning.sql
-- Purpose: Implement table partitioning strategy for SYS_AUDIT_LOG
-- Task: 11.4 from Full Traceability System spec
-- =====================================================
-- This script converts SYS_AUDIT_LOG to a range-partitioned table by CREATION_DATE
-- Partitioning improves query performance for date-range queries and simplifies archival
-- =====================================================

-- Step 1: Create a new partitioned table with the same structure
-- =====================================================
-- Note: Oracle does not support direct conversion of non-partitioned to partitioned tables
-- We must create a new partitioned table and migrate data

CREATE TABLE SYS_AUDIT_LOG_PARTITIONED (
    ROW_ID             NUMBER(19),
    ACTOR_TYPE         NVARCHAR2(50)              NOT NULL,
    ACTOR_ID           NUMBER(19)                 NOT NULL,
    COMPANY_ID         NUMBER(19),
    ACTION             NVARCHAR2(100)             NOT NULL,
    ENTITY_TYPE        NVARCHAR2(100)             NOT NULL,
    ENTITY_ID          NUMBER(19),
    OLD_VALUE          CLOB,
    NEW_VALUE          CLOB,
    IP_ADDRESS         NVARCHAR2(50),
    USER_AGENT         NVARCHAR2(500),
    CREATION_DATE      DATE                       DEFAULT SYSDATE,
    CORRELATION_ID     VARCHAR2(100 BYTE),
    BRANCH_ID          NUMBER(19),
    HTTP_METHOD        VARCHAR2(10 BYTE),
    ENDPOINT_PATH      VARCHAR2(500 BYTE),
    REQUEST_PAYLOAD    CLOB,
    RESPONSE_PAYLOAD   CLOB,
    EXECUTION_TIME_MS  NUMBER(19),
    STATUS_CODE        NUMBER(5),
    EXCEPTION_TYPE     VARCHAR2(200 BYTE),
    EXCEPTION_MESSAGE  VARCHAR2(2000 BYTE),
    STACK_TRACE        CLOB,
    SEVERITY           VARCHAR2(20 BYTE)          DEFAULT 'Info',
    EVENT_CATEGORY     VARCHAR2(50 BYTE)          DEFAULT 'DataChange',
    METADATA           CLOB
)
LOB (METADATA) STORE AS SECUREFILE (
    TABLESPACE  USERS
    ENABLE      STORAGE IN ROW
    CHUNK       8192
    NOCACHE
    LOGGING
)
LOB (NEW_VALUE) STORE AS SECUREFILE (
    TABLESPACE  USERS
    ENABLE      STORAGE IN ROW
    CHUNK       8192
    NOCACHE
    LOGGING
)
LOB (OLD_VALUE) STORE AS SECUREFILE (
    TABLESPACE  USERS
    ENABLE      STORAGE IN ROW
    CHUNK       8192
    NOCACHE
    LOGGING
)
LOB (REQUEST_PAYLOAD) STORE AS SECUREFILE (
    TABLESPACE  USERS
    ENABLE      STORAGE IN ROW
    CHUNK       8192
    NOCACHE
    LOGGING
)
LOB (RESPONSE_PAYLOAD) STORE AS SECUREFILE (
    TABLESPACE  USERS
    ENABLE      STORAGE IN ROW
    CHUNK       8192
    NOCACHE
    LOGGING
)
LOB (STACK_TRACE) STORE AS SECUREFILE (
    TABLESPACE  USERS
    ENABLE      STORAGE IN ROW
    CHUNK       8192
    NOCACHE
    LOGGING
)
TABLESPACE USERS
PARTITION BY RANGE (CREATION_DATE)
INTERVAL (NUMTOYMINTERVAL(1, 'MONTH'))
(
    -- Initial partition for data before 2024-01-01
    PARTITION P_AUDIT_BEFORE_2024 VALUES LESS THAN (TO_DATE('2024-01-01', 'YYYY-MM-DD'))
)
ENABLE ROW MOVEMENT;

-- Add table and column comments
COMMENT ON TABLE SYS_AUDIT_LOG_PARTITIONED IS 'Comprehensive audit trail for all system changes (partitioned by month for performance)';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.ACTOR_TYPE IS 'Type of user performing action';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.ACTION IS 'Action performed (CREATE, UPDATE, DELETE, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.OLD_VALUE IS 'JSON of old values';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.NEW_VALUE IS 'JSON of new values';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.CORRELATION_ID IS 'Unique identifier tracking request through system';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.BRANCH_ID IS 'Foreign key to SYS_BRANCH table for multi-tenant operations';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.HTTP_METHOD IS 'HTTP method of the API request (GET, POST, PUT, DELETE)';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.ENDPOINT_PATH IS 'API endpoint path that was called';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.REQUEST_PAYLOAD IS 'JSON request body (sensitive data masked)';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.RESPONSE_PAYLOAD IS 'JSON response body (sensitive data masked)';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.EXECUTION_TIME_MS IS 'Total execution time in milliseconds';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.STATUS_CODE IS 'HTTP status code of the response';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.EXCEPTION_TYPE IS 'Type of exception if error occurred';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.EXCEPTION_MESSAGE IS 'Exception message if error occurred';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.STACK_TRACE IS 'Full stack trace if exception occurred';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.SEVERITY IS 'Severity level: Critical, Error, Warning, Info';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.EVENT_CATEGORY IS 'Category: DataChange, Authentication, Permission, Exception, Configuration, Request';
COMMENT ON COLUMN SYS_AUDIT_LOG_PARTITIONED.METADATA IS 'Additional JSON metadata for extensibility';

-- Step 2: Create local partitioned indexes for optimal query performance
-- =====================================================
-- Local indexes are partitioned along with the table, improving query performance

-- Primary key as local partitioned index
CREATE UNIQUE INDEX PK_AUDIT_LOG_PART ON SYS_AUDIT_LOG_PARTITIONED(ROW_ID, CREATION_DATE)
LOCAL
TABLESPACE USERS;

-- Correlation ID index (local partitioned)
CREATE INDEX IDX_AUDIT_LOG_PART_CORR ON SYS_AUDIT_LOG_PARTITIONED(CORRELATION_ID, CREATION_DATE)
LOCAL
TABLESPACE USERS;

-- Branch ID index (local partitioned)
CREATE INDEX IDX_AUDIT_LOG_PART_BRANCH ON SYS_AUDIT_LOG_PARTITIONED(BRANCH_ID, CREATION_DATE)
LOCAL
TABLESPACE USERS;

-- Endpoint path index (local partitioned)
CREATE INDEX IDX_AUDIT_LOG_PART_ENDPOINT ON SYS_AUDIT_LOG_PARTITIONED(ENDPOINT_PATH, CREATION_DATE)
LOCAL
TABLESPACE USERS;

-- Event category index (local partitioned)
CREATE INDEX IDX_AUDIT_LOG_PART_CATEGORY ON SYS_AUDIT_LOG_PARTITIONED(EVENT_CATEGORY, CREATION_DATE)
LOCAL
TABLESPACE USERS;

-- Severity index (local partitioned)
CREATE INDEX IDX_AUDIT_LOG_PART_SEVERITY ON SYS_AUDIT_LOG_PARTITIONED(SEVERITY, CREATION_DATE)
LOCAL
TABLESPACE USERS;

-- Composite indexes for common query patterns (local partitioned)
CREATE INDEX IDX_AUDIT_LOG_PART_COMPANY_DATE ON SYS_AUDIT_LOG_PARTITIONED(COMPANY_ID, CREATION_DATE)
LOCAL
TABLESPACE USERS;

CREATE INDEX IDX_AUDIT_LOG_PART_ACTOR_DATE ON SYS_AUDIT_LOG_PARTITIONED(ACTOR_ID, CREATION_DATE)
LOCAL
TABLESPACE USERS;

CREATE INDEX IDX_AUDIT_LOG_PART_ENTITY_DATE ON SYS_AUDIT_LOG_PARTITIONED(ENTITY_TYPE, ENTITY_ID, CREATION_DATE)
LOCAL
TABLESPACE USERS;

-- Step 3: Migrate data from old table to new partitioned table
-- =====================================================
-- This can be done in batches to avoid long-running transactions
-- For production, consider using Oracle Data Pump or parallel insert

-- Option 1: Direct insert (for smaller datasets)
-- INSERT /*+ APPEND PARALLEL(4) */ INTO SYS_AUDIT_LOG_PARTITIONED
-- SELECT * FROM SYS_AUDIT_LOG;

-- Option 2: Batch insert (for larger datasets - recommended for production)
-- This should be done in a maintenance window
-- See the partition maintenance procedures below for batch migration logic

-- Step 4: Rename tables to swap old and new
-- =====================================================
-- This should be done during a maintenance window after data migration is complete

-- RENAME SYS_AUDIT_LOG TO SYS_AUDIT_LOG_OLD;
-- RENAME SYS_AUDIT_LOG_PARTITIONED TO SYS_AUDIT_LOG;

-- Step 5: Recreate foreign key constraints
-- =====================================================
-- After renaming, recreate any foreign key constraints that reference SYS_AUDIT_LOG

-- ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT FK_AUDIT_LOG_BRANCH 
--     FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID);

-- Step 6: Update dependent objects (views, procedures, etc.)
-- =====================================================
-- Verify and update any database objects that reference SYS_AUDIT_LOG

COMMIT;

-- =====================================================
-- Partition Maintenance Procedures
-- =====================================================

-- Procedure 1: Add new partition manually (if needed)
-- =====================================================
-- Note: With INTERVAL partitioning, Oracle automatically creates new partitions
-- This procedure is only needed if you want to pre-create partitions

CREATE OR REPLACE PROCEDURE SP_ADD_AUDIT_LOG_PARTITION(
    p_partition_name IN VARCHAR2,
    p_partition_date IN DATE
)
AS
    v_sql VARCHAR2(4000);
BEGIN
    -- Construct ALTER TABLE statement to add partition
    v_sql := 'ALTER TABLE SYS_AUDIT_LOG ADD PARTITION ' || p_partition_name ||
             ' VALUES LESS THAN (TO_DATE(''' || TO_CHAR(p_partition_date, 'YYYY-MM-DD') || ''', ''YYYY-MM-DD''))';
    
    -- Execute the statement
    EXECUTE IMMEDIATE v_sql;
    
    DBMS_OUTPUT.PUT_LINE('Partition ' || p_partition_name || ' added successfully');
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error adding partition: ' || SQLERRM);
        ROLLBACK;
        RAISE;
END SP_ADD_AUDIT_LOG_PARTITION;
/

-- Procedure 2: Drop old partition (for archival)
-- =====================================================
-- This procedure drops a partition after data has been archived
-- WARNING: This permanently deletes data. Ensure data is archived first!

CREATE OR REPLACE PROCEDURE SP_DROP_AUDIT_LOG_PARTITION(
    p_partition_name IN VARCHAR2
)
AS
    v_sql VARCHAR2(4000);
    v_row_count NUMBER;
BEGIN
    -- Check if partition exists and get row count
    SELECT COUNT(*)
    INTO v_row_count
    FROM USER_TAB_PARTITIONS
    WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
    AND PARTITION_NAME = p_partition_name;
    
    IF v_row_count = 0 THEN
        DBMS_OUTPUT.PUT_LINE('Partition ' || p_partition_name || ' does not exist');
        RETURN;
    END IF;
    
    -- Drop the partition
    v_sql := 'ALTER TABLE SYS_AUDIT_LOG DROP PARTITION ' || p_partition_name;
    EXECUTE IMMEDIATE v_sql;
    
    DBMS_OUTPUT.PUT_LINE('Partition ' || p_partition_name || ' dropped successfully');
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error dropping partition: ' || SQLERRM);
        ROLLBACK;
        RAISE;
END SP_DROP_AUDIT_LOG_PARTITION;
/

-- Procedure 3: Truncate old partition (faster than delete)
-- =====================================================
-- This procedure truncates a partition, removing all data quickly
-- Use this before dropping a partition or for testing

CREATE OR REPLACE PROCEDURE SP_TRUNCATE_AUDIT_LOG_PARTITION(
    p_partition_name IN VARCHAR2
)
AS
    v_sql VARCHAR2(4000);
BEGIN
    -- Truncate the partition
    v_sql := 'ALTER TABLE SYS_AUDIT_LOG TRUNCATE PARTITION ' || p_partition_name;
    EXECUTE IMMEDIATE v_sql;
    
    DBMS_OUTPUT.PUT_LINE('Partition ' || p_partition_name || ' truncated successfully');
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error truncating partition: ' || SQLERRM);
        ROLLBACK;
        RAISE;
END SP_TRUNCATE_AUDIT_LOG_PARTITION;
/

-- Procedure 4: Archive partition data to archive table
-- =====================================================
-- This procedure moves data from a partition to the archive table before dropping

CREATE OR REPLACE PROCEDURE SP_ARCHIVE_AUDIT_LOG_PARTITION(
    p_partition_name IN VARCHAR2,
    p_archive_batch_id IN NUMBER DEFAULT NULL
)
AS
    v_sql VARCHAR2(4000);
    v_rows_archived NUMBER := 0;
    v_batch_id NUMBER;
    v_partition_exists NUMBER;
BEGIN
    -- Check if partition exists
    SELECT COUNT(*)
    INTO v_partition_exists
    FROM USER_TAB_PARTITIONS
    WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
    AND PARTITION_NAME = p_partition_name;
    
    IF v_partition_exists = 0 THEN
        DBMS_OUTPUT.PUT_LINE('Partition ' || p_partition_name || ' does not exist');
        RETURN;
    END IF;
    
    -- Generate batch ID if not provided
    IF p_archive_batch_id IS NULL THEN
        SELECT NVL(MAX(ARCHIVE_BATCH_ID), 0) + 1
        INTO v_batch_id
        FROM SYS_AUDIT_LOG_ARCHIVE;
    ELSE
        v_batch_id := p_archive_batch_id;
    END IF;
    
    -- Insert data from partition into archive table
    v_sql := 'INSERT /*+ APPEND */ INTO SYS_AUDIT_LOG_ARCHIVE ' ||
             'SELECT ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID, ACTION, ' ||
             'ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT, ' ||
             'CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH, REQUEST_PAYLOAD, RESPONSE_PAYLOAD, ' ||
             'EXECUTION_TIME_MS, STATUS_CODE, EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, ' ||
             'SEVERITY, EVENT_CATEGORY, METADATA, CREATION_DATE, ' ||
             'SYSDATE AS ARCHIVED_DATE, ' || v_batch_id || ' AS ARCHIVE_BATCH_ID, ' ||
             'NULL AS CHECKSUM ' ||
             'FROM SYS_AUDIT_LOG PARTITION (' || p_partition_name || ')';
    
    EXECUTE IMMEDIATE v_sql;
    v_rows_archived := SQL%ROWCOUNT;
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('Archived ' || v_rows_archived || ' rows from partition ' || 
                        p_partition_name || ' to archive table (batch ID: ' || v_batch_id || ')');
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error archiving partition: ' || SQLERRM);
        ROLLBACK;
        RAISE;
END SP_ARCHIVE_AUDIT_LOG_PARTITION;
/

-- Procedure 5: Get partition information
-- =====================================================
-- This procedure displays information about all partitions

CREATE OR REPLACE PROCEDURE SP_GET_AUDIT_LOG_PARTITION_INFO
AS
    CURSOR c_partitions IS
        SELECT 
            PARTITION_NAME,
            HIGH_VALUE,
            NUM_ROWS,
            BLOCKS,
            COMPRESSION,
            LAST_ANALYZED
        FROM USER_TAB_PARTITIONS
        WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
        ORDER BY PARTITION_POSITION;
BEGIN
    DBMS_OUTPUT.PUT_LINE('=== SYS_AUDIT_LOG Partition Information ===');
    DBMS_OUTPUT.PUT_LINE('');
    
    FOR rec IN c_partitions LOOP
        DBMS_OUTPUT.PUT_LINE('Partition: ' || rec.PARTITION_NAME);
        DBMS_OUTPUT.PUT_LINE('  High Value: ' || rec.HIGH_VALUE);
        DBMS_OUTPUT.PUT_LINE('  Rows: ' || NVL(TO_CHAR(rec.NUM_ROWS), 'Not analyzed'));
        DBMS_OUTPUT.PUT_LINE('  Blocks: ' || NVL(TO_CHAR(rec.BLOCKS), 'Not analyzed'));
        DBMS_OUTPUT.PUT_LINE('  Compression: ' || NVL(rec.COMPRESSION, 'NONE'));
        DBMS_OUTPUT.PUT_LINE('  Last Analyzed: ' || NVL(TO_CHAR(rec.LAST_ANALYZED, 'YYYY-MM-DD HH24:MI:SS'), 'Never'));
        DBMS_OUTPUT.PUT_LINE('');
    END LOOP;
    
    DBMS_OUTPUT.PUT_LINE('=== End of Partition Information ===');
END SP_GET_AUDIT_LOG_PARTITION_INFO;
/

-- Procedure 6: Migrate data in batches (for production use)
-- =====================================================
-- This procedure migrates data from old table to partitioned table in batches

CREATE OR REPLACE PROCEDURE SP_MIGRATE_AUDIT_LOG_TO_PARTITIONED(
    p_batch_size IN NUMBER DEFAULT 10000,
    p_commit_interval IN NUMBER DEFAULT 5
)
AS
    v_total_rows NUMBER := 0;
    v_migrated_rows NUMBER := 0;
    v_batch_count NUMBER := 0;
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_min_row_id NUMBER;
    v_max_row_id NUMBER;
    v_current_row_id NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    
    -- Get total row count
    SELECT COUNT(*), MIN(ROW_ID), MAX(ROW_ID)
    INTO v_total_rows, v_min_row_id, v_max_row_id
    FROM SYS_AUDIT_LOG;
    
    DBMS_OUTPUT.PUT_LINE('Starting migration of ' || v_total_rows || ' rows');
    DBMS_OUTPUT.PUT_LINE('Batch size: ' || p_batch_size);
    DBMS_OUTPUT.PUT_LINE('Commit interval: ' || p_commit_interval || ' batches');
    DBMS_OUTPUT.PUT_LINE('');
    
    v_current_row_id := v_min_row_id;
    
    -- Migrate in batches
    WHILE v_current_row_id <= v_max_row_id LOOP
        -- Insert batch
        INSERT /*+ APPEND */ INTO SYS_AUDIT_LOG_PARTITIONED
        SELECT *
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID >= v_current_row_id
        AND ROW_ID < v_current_row_id + p_batch_size;
        
        v_migrated_rows := v_migrated_rows + SQL%ROWCOUNT;
        v_batch_count := v_batch_count + 1;
        
        -- Commit at intervals
        IF MOD(v_batch_count, p_commit_interval) = 0 THEN
            COMMIT;
            DBMS_OUTPUT.PUT_LINE('Migrated ' || v_migrated_rows || ' of ' || v_total_rows || 
                               ' rows (' || ROUND((v_migrated_rows / v_total_rows) * 100, 2) || '%)');
        END IF;
        
        v_current_row_id := v_current_row_id + p_batch_size;
    END LOOP;
    
    COMMIT;
    
    v_end_time := SYSTIMESTAMP;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Migration completed successfully');
    DBMS_OUTPUT.PUT_LINE('Total rows migrated: ' || v_migrated_rows);
    DBMS_OUTPUT.PUT_LINE('Duration: ' || TO_CHAR(EXTRACT(SECOND FROM (v_end_time - v_start_time))) || ' seconds');
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error during migration: ' || SQLERRM);
        ROLLBACK;
        RAISE;
END SP_MIGRATE_AUDIT_LOG_TO_PARTITIONED;
/

-- Grant execute permissions on procedures
GRANT EXECUTE ON SP_ADD_AUDIT_LOG_PARTITION TO PUBLIC;
GRANT EXECUTE ON SP_DROP_AUDIT_LOG_PARTITION TO PUBLIC;
GRANT EXECUTE ON SP_TRUNCATE_AUDIT_LOG_PARTITION TO PUBLIC;
GRANT EXECUTE ON SP_ARCHIVE_AUDIT_LOG_PARTITION TO PUBLIC;
GRANT EXECUTE ON SP_GET_AUDIT_LOG_PARTITION_INFO TO PUBLIC;
GRANT EXECUTE ON SP_MIGRATE_AUDIT_LOG_TO_PARTITIONED TO PUBLIC;

COMMIT;

-- =====================================================
-- End of Script
-- =====================================================
