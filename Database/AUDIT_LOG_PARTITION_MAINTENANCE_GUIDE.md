# SYS_AUDIT_LOG Partition Maintenance Quick Reference

## Quick Command Reference

### View Partition Information

```sql
-- Get detailed partition information
EXEC SP_GET_AUDIT_LOG_PARTITION_INFO;

-- Query partition metadata
SELECT 
    PARTITION_NAME,
    HIGH_VALUE,
    NUM_ROWS,
    ROUND(BYTES / 1024 / 1024, 2) AS SIZE_MB,
    LAST_ANALYZED
FROM USER_TAB_PARTITIONS p
LEFT JOIN USER_SEGMENTS s ON s.PARTITION_NAME = p.PARTITION_NAME
WHERE p.TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY PARTITION_POSITION DESC;
```

### Archive Old Partition

```sql
-- Archive a specific partition to archive table
EXEC SP_ARCHIVE_AUDIT_LOG_PARTITION('P_AUDIT_2023_01');

-- Archive with custom batch ID
EXEC SP_ARCHIVE_AUDIT_LOG_PARTITION('P_AUDIT_2023_01', 12345);
```

### Drop Old Partition

```sql
-- Drop a partition (WARNING: Irreversible - archive first!)
EXEC SP_DROP_AUDIT_LOG_PARTITION('P_AUDIT_2023_01');
```

### Truncate Partition

```sql
-- Quickly remove all data from a partition
EXEC SP_TRUNCATE_AUDIT_LOG_PARTITION('P_AUDIT_2023_01');
```

### Add Partition Manually

```sql
-- Pre-create a partition (rarely needed with INTERVAL partitioning)
EXEC SP_ADD_AUDIT_LOG_PARTITION(
    'P_AUDIT_2025_02',
    TO_DATE('2025-03-01', 'YYYY-MM-DD')
);
```

### Migrate Data to Partitioned Table

```sql
-- Migrate in batches (batch_size=10000, commit_interval=5)
EXEC SP_MIGRATE_AUDIT_LOG_TO_PARTITIONED(10000, 5);
```

## Monthly Maintenance Checklist

### Week 1: Review and Plan

- [ ] Review partition sizes and growth trends
- [ ] Identify partitions eligible for archival (based on retention policy)
- [ ] Verify archive table has sufficient space
- [ ] Schedule maintenance window if needed

```sql
-- Check partitions older than retention period
SELECT 
    PARTITION_NAME,
    HIGH_VALUE,
    NUM_ROWS,
    ROUND(BYTES / 1024 / 1024, 2) AS SIZE_MB
FROM USER_TAB_PARTITIONS p
LEFT JOIN USER_SEGMENTS s ON s.PARTITION_NAME = p.PARTITION_NAME
WHERE p.TABLE_NAME = 'SYS_AUDIT_LOG'
AND PARTITION_NAME LIKE 'P_AUDIT_%'
AND PARTITION_POSITION < (
    SELECT MAX(PARTITION_POSITION) - 36  -- 3 years
    FROM USER_TAB_PARTITIONS
    WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
)
ORDER BY PARTITION_POSITION;
```

### Week 2: Archive Old Partitions

- [ ] Backup database before archival
- [ ] Archive partitions older than retention period
- [ ] Verify archived data in archive table
- [ ] Document archived partitions

```sql
-- Archive partitions older than 3 years
DECLARE
    v_cutoff_date DATE := ADD_MONTHS(SYSDATE, -36);
BEGIN
    FOR rec IN (
        SELECT PARTITION_NAME
        FROM USER_TAB_PARTITIONS
        WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
        AND PARTITION_NAME LIKE 'P_AUDIT_2021%'  -- Adjust year as needed
        ORDER BY PARTITION_POSITION
    ) LOOP
        DBMS_OUTPUT.PUT_LINE('Archiving: ' || rec.PARTITION_NAME);
        SP_ARCHIVE_AUDIT_LOG_PARTITION(rec.PARTITION_NAME);
    END LOOP;
END;
/
```

### Week 3: Drop Archived Partitions

- [ ] Verify archived data is accessible
- [ ] Verify backup is complete
- [ ] Drop archived partitions
- [ ] Monitor space reclamation

```sql
-- Drop partitions that have been archived
DECLARE
    v_archived_batch_id NUMBER;
BEGIN
    -- Get latest archive batch ID
    SELECT MAX(ARCHIVE_BATCH_ID) INTO v_archived_batch_id
    FROM SYS_AUDIT_LOG_ARCHIVE;
    
    -- Drop partitions from that batch
    FOR rec IN (
        SELECT DISTINCT PARTITION_NAME
        FROM USER_TAB_PARTITIONS
        WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
        AND PARTITION_NAME LIKE 'P_AUDIT_2021%'  -- Adjust as needed
    ) LOOP
        DBMS_OUTPUT.PUT_LINE('Dropping: ' || rec.PARTITION_NAME);
        SP_DROP_AUDIT_LOG_PARTITION(rec.PARTITION_NAME);
    END LOOP;
END;
/
```

### Week 4: Maintenance and Optimization

- [ ] Gather statistics on active partitions
- [ ] Rebuild indexes if needed
- [ ] Compress old partitions
- [ ] Update documentation

```sql
-- Gather statistics on all partitions
EXEC DBMS_STATS.GATHER_TABLE_STATS(
    ownname => USER,
    tabname => 'SYS_AUDIT_LOG',
    granularity => 'ALL',
    degree => 4
);

-- Compress old partitions (older than 1 year)
DECLARE
BEGIN
    FOR rec IN (
        SELECT PARTITION_NAME
        FROM USER_TAB_PARTITIONS
        WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
        AND PARTITION_NAME LIKE 'P_AUDIT_2023%'  -- Adjust year
        ORDER BY PARTITION_POSITION
    ) LOOP
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_AUDIT_LOG MODIFY PARTITION ' || 
                         rec.PARTITION_NAME || ' COMPRESS';
        DBMS_OUTPUT.PUT_LINE('Compressed: ' || rec.PARTITION_NAME);
    END LOOP;
END;
/
```

## Retention Policy Reference

| Event Category | Retention Period | Archive After | Drop After |
|---------------|------------------|---------------|------------|
| Authentication | 1 year | 13 months | 14 months |
| DataChange | 3 years | 37 months | 38 months |
| Financial | 7 years | 85 months | 86 months |
| PersonalData (GDPR) | 3 years | 37 months | 38 months |
| Security | 2 years | 25 months | 26 months |
| Configuration | 5 years | 61 months | 62 months |

**Note**: The most restrictive retention policy (7 years for Financial) should be applied to all partitions unless event-specific archival is implemented.

## Automated Archival Setup

### Create Scheduled Job

```sql
-- Create procedure for automated archival
CREATE OR REPLACE PROCEDURE SP_ARCHIVE_OLD_AUDIT_PARTITIONS
AS
    v_cutoff_date DATE := ADD_MONTHS(SYSDATE, -36);  -- 3 years
    v_archived_count NUMBER := 0;
    v_dropped_count NUMBER := 0;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting automated archival process');
    DBMS_OUTPUT.PUT_LINE('Cutoff date: ' || TO_CHAR(v_cutoff_date, 'YYYY-MM-DD'));
    
    -- Archive and drop old partitions
    FOR rec IN (
        SELECT PARTITION_NAME
        FROM USER_TAB_PARTITIONS
        WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
        AND PARTITION_NAME LIKE 'P_AUDIT_%'
        AND PARTITION_POSITION < (
            SELECT MAX(PARTITION_POSITION) - 36
            FROM USER_TAB_PARTITIONS
            WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
        )
        ORDER BY PARTITION_POSITION
    ) LOOP
        BEGIN
            -- Archive partition
            SP_ARCHIVE_AUDIT_LOG_PARTITION(rec.PARTITION_NAME);
            v_archived_count := v_archived_count + 1;
            DBMS_OUTPUT.PUT_LINE('Archived: ' || rec.PARTITION_NAME);
            
            -- Drop partition
            SP_DROP_AUDIT_LOG_PARTITION(rec.PARTITION_NAME);
            v_dropped_count := v_dropped_count + 1;
            DBMS_OUTPUT.PUT_LINE('Dropped: ' || rec.PARTITION_NAME);
            
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('Error processing ' || rec.PARTITION_NAME || ': ' || SQLERRM);
        END;
    END LOOP;
    
    DBMS_OUTPUT.PUT_LINE('Archival complete. Archived: ' || v_archived_count || ', Dropped: ' || v_dropped_count);
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error in automated archival: ' || SQLERRM);
        RAISE;
END SP_ARCHIVE_OLD_AUDIT_PARTITIONS;
/

-- Create scheduled job to run monthly
BEGIN
    DBMS_SCHEDULER.CREATE_JOB(
        job_name        => 'AUDIT_LOG_MONTHLY_ARCHIVAL',
        job_type        => 'STORED_PROCEDURE',
        job_action      => 'SP_ARCHIVE_OLD_AUDIT_PARTITIONS',
        start_date      => SYSTIMESTAMP,
        repeat_interval => 'FREQ=MONTHLY; BYMONTHDAY=1; BYHOUR=2; BYMINUTE=0',
        enabled         => TRUE,
        comments        => 'Monthly archival of old audit log partitions'
    );
    
    DBMS_OUTPUT.PUT_LINE('Scheduled job created successfully');
END;
/

-- View scheduled job status
SELECT 
    JOB_NAME,
    STATE,
    ENABLED,
    NEXT_RUN_DATE,
    LAST_START_DATE,
    LAST_RUN_DURATION
FROM USER_SCHEDULER_JOBS
WHERE JOB_NAME = 'AUDIT_LOG_MONTHLY_ARCHIVAL';
```

### Disable/Enable Scheduled Job

```sql
-- Disable job
EXEC DBMS_SCHEDULER.DISABLE('AUDIT_LOG_MONTHLY_ARCHIVAL');

-- Enable job
EXEC DBMS_SCHEDULER.ENABLE('AUDIT_LOG_MONTHLY_ARCHIVAL');

-- Drop job
EXEC DBMS_SCHEDULER.DROP_JOB('AUDIT_LOG_MONTHLY_ARCHIVAL');
```

## Monitoring Queries

### Partition Growth Trend

```sql
-- View partition growth over time
SELECT 
    PARTITION_NAME,
    NUM_ROWS,
    ROUND(BYTES / 1024 / 1024, 2) AS SIZE_MB,
    ROUND(BYTES / NULLIF(NUM_ROWS, 0), 2) AS AVG_ROW_SIZE_BYTES
FROM USER_TAB_PARTITIONS p
LEFT JOIN USER_SEGMENTS s ON s.PARTITION_NAME = p.PARTITION_NAME
WHERE p.TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY PARTITION_POSITION DESC
FETCH FIRST 12 ROWS ONLY;  -- Last 12 months
```

### Index Health Check

```sql
-- Check index status on partitions
SELECT 
    i.INDEX_NAME,
    ip.PARTITION_NAME,
    ip.STATUS,
    ip.NUM_ROWS,
    ip.LAST_ANALYZED
FROM USER_IND_PARTITIONS ip
JOIN USER_INDEXES i ON i.INDEX_NAME = ip.INDEX_NAME
WHERE i.TABLE_NAME = 'SYS_AUDIT_LOG'
AND ip.STATUS != 'USABLE'
ORDER BY i.INDEX_NAME, ip.PARTITION_POSITION DESC;
```

### Archive Table Status

```sql
-- Check archive table size and row count
SELECT 
    COUNT(*) AS TOTAL_ROWS,
    COUNT(DISTINCT ARCHIVE_BATCH_ID) AS BATCH_COUNT,
    MIN(CREATION_DATE) AS OLDEST_RECORD,
    MAX(CREATION_DATE) AS NEWEST_RECORD,
    MIN(ARCHIVED_DATE) AS FIRST_ARCHIVED,
    MAX(ARCHIVED_DATE) AS LAST_ARCHIVED
FROM SYS_AUDIT_LOG_ARCHIVE;

-- Check archive batches
SELECT 
    ARCHIVE_BATCH_ID,
    COUNT(*) AS ROW_COUNT,
    MIN(CREATION_DATE) AS OLDEST_RECORD,
    MAX(CREATION_DATE) AS NEWEST_RECORD,
    MIN(ARCHIVED_DATE) AS ARCHIVED_DATE
FROM SYS_AUDIT_LOG_ARCHIVE
GROUP BY ARCHIVE_BATCH_ID
ORDER BY ARCHIVE_BATCH_ID DESC;
```

### Space Usage

```sql
-- Total space used by SYS_AUDIT_LOG
SELECT 
    SEGMENT_NAME,
    PARTITION_NAME,
    ROUND(BYTES / 1024 / 1024, 2) AS SIZE_MB,
    ROUND(BYTES / 1024 / 1024 / 1024, 2) AS SIZE_GB
FROM USER_SEGMENTS
WHERE SEGMENT_NAME = 'SYS_AUDIT_LOG'
ORDER BY PARTITION_NAME DESC;

-- Total space by segment type
SELECT 
    SEGMENT_TYPE,
    COUNT(*) AS SEGMENT_COUNT,
    ROUND(SUM(BYTES) / 1024 / 1024 / 1024, 2) AS TOTAL_SIZE_GB
FROM USER_SEGMENTS
WHERE SEGMENT_NAME LIKE 'SYS_AUDIT_LOG%'
OR SEGMENT_NAME LIKE 'IDX_AUDIT_LOG%'
GROUP BY SEGMENT_TYPE
ORDER BY TOTAL_SIZE_GB DESC;
```

## Troubleshooting

### Partition Not Created Automatically

```sql
-- Check if INTERVAL partitioning is enabled
SELECT INTERVAL FROM USER_PART_TABLES WHERE TABLE_NAME = 'SYS_AUDIT_LOG';

-- Enable INTERVAL partitioning if NULL
ALTER TABLE SYS_AUDIT_LOG SET INTERVAL (NUMTOYMINTERVAL(1, 'MONTH'));
```

### Query Not Using Partition Pruning

```sql
-- Check execution plan
EXPLAIN PLAN FOR
SELECT * FROM SYS_AUDIT_LOG
WHERE CREATION_DATE >= TO_DATE('2024-01-01', 'YYYY-MM-DD');

SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY);

-- Look for "PARTITION RANGE ITERATOR" with specific partition numbers
```

### Cannot Drop Partition

```sql
-- Check for foreign key constraints
SELECT 
    CONSTRAINT_NAME,
    TABLE_NAME,
    R_CONSTRAINT_NAME
FROM USER_CONSTRAINTS
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
AND CONSTRAINT_TYPE = 'R';

-- Check for active sessions
SELECT 
    s.SID,
    s.SERIAL#,
    s.USERNAME,
    s.PROGRAM,
    s.SQL_ID
FROM V$SESSION s
WHERE s.SQL_TEXT LIKE '%SYS_AUDIT_LOG%'
OR s.MODULE LIKE '%AUDIT%';
```

### Partition Archive Failed

```sql
-- Check archive table space
SELECT 
    TABLESPACE_NAME,
    ROUND(BYTES / 1024 / 1024 / 1024, 2) AS SIZE_GB,
    ROUND(MAXBYTES / 1024 / 1024 / 1024, 2) AS MAX_SIZE_GB
FROM USER_SEGMENTS
WHERE SEGMENT_NAME = 'SYS_AUDIT_LOG_ARCHIVE';

-- Check for errors in alert log
-- Review Oracle alert log for ORA- errors

-- Retry archive with smaller batch
-- (Modify procedure to use batch processing if needed)
```

## Emergency Procedures

### Rapid Space Reclamation

If disk space is critically low:

```sql
-- 1. Identify largest partitions
SELECT 
    PARTITION_NAME,
    ROUND(BYTES / 1024 / 1024 / 1024, 2) AS SIZE_GB
FROM USER_SEGMENTS
WHERE SEGMENT_NAME = 'SYS_AUDIT_LOG'
ORDER BY BYTES DESC
FETCH FIRST 5 ROWS ONLY;

-- 2. Truncate oldest partition (WARNING: Data loss!)
EXEC SP_TRUNCATE_AUDIT_LOG_PARTITION('P_AUDIT_BEFORE_2024');

-- 3. Drop truncated partition
EXEC SP_DROP_AUDIT_LOG_PARTITION('P_AUDIT_BEFORE_2024');

-- 4. Compress remaining old partitions
ALTER TABLE SYS_AUDIT_LOG MODIFY PARTITION P_AUDIT_2023_01 COMPRESS;
```

### Rollback Partitioning

If partitioning causes issues:

```sql
-- 1. Stop application
-- 2. Rename tables
RENAME SYS_AUDIT_LOG TO SYS_AUDIT_LOG_PARTITIONED_BACKUP;
RENAME SYS_AUDIT_LOG_OLD TO SYS_AUDIT_LOG;

-- 3. Recreate constraints
ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT FK_AUDIT_LOG_BRANCH 
    FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID);

-- 4. Restart application and verify
-- 5. Drop backup after verification
-- DROP TABLE SYS_AUDIT_LOG_PARTITIONED_BACKUP;
```

## Contact and Support

For assistance with partition maintenance:
- Database Administrator: [Contact Info]
- Development Team: [Contact Info]
- Documentation: `Database/AUDIT_LOG_PARTITIONING_STRATEGY.md`

---

**Last Updated**: 2024  
**Version**: 1.0  
**Task Reference**: Full Traceability System - Task 11.4
