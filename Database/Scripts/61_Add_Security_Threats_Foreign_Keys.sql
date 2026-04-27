-- Add foreign key constraints to SYS_SECURITY_THREATS table
-- This completes task 1.10: Create SYS_SECURITY_THREATS table for security monitoring

-- Check if foreign key constraints already exist before adding them
DECLARE
    fk_user_exists NUMBER := 0;
    fk_company_exists NUMBER := 0;
BEGIN
    -- Check if FK_SECURITY_THREAT_USER constraint exists
    SELECT COUNT(*)
    INTO fk_user_exists
    FROM user_constraints
    WHERE constraint_name = 'FK_SECURITY_THREAT_USER'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    -- Check if FK_SECURITY_THREAT_COMPANY constraint exists
    SELECT COUNT(*)
    INTO fk_company_exists
    FROM user_constraints
    WHERE constraint_name = 'FK_SECURITY_THREAT_COMPANY'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    -- Add USER_ID foreign key constraint if it doesn't exist
    IF fk_user_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_SECURITY_THREATS ADD CONSTRAINT FK_SECURITY_THREAT_USER 
                          FOREIGN KEY (USER_ID) REFERENCES SYS_USERS(ROW_ID)';
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_SECURITY_THREAT_USER added successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_SECURITY_THREAT_USER already exists.');
    END IF;
    
    -- Add COMPANY_ID foreign key constraint if it doesn't exist
    IF fk_company_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_SECURITY_THREATS ADD CONSTRAINT FK_SECURITY_THREAT_COMPANY 
                          FOREIGN KEY (COMPANY_ID) REFERENCES SYS_COMPANY(ROW_ID)';
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_SECURITY_THREAT_COMPANY added successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_SECURITY_THREAT_COMPANY already exists.');
    END IF;
    
    -- Add ACKNOWLEDGED_BY foreign key constraint if it doesn't exist
    SELECT COUNT(*)
    INTO fk_user_exists
    FROM user_constraints
    WHERE constraint_name = 'FK_SECURITY_THREAT_ACK_USER'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    IF fk_user_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_SECURITY_THREATS ADD CONSTRAINT FK_SECURITY_THREAT_ACK_USER 
                          FOREIGN KEY (ACKNOWLEDGED_BY) REFERENCES SYS_USERS(ROW_ID)';
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_SECURITY_THREAT_ACK_USER added successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Foreign key constraint FK_SECURITY_THREAT_ACK_USER already exists.');
    END IF;
END;
/

-- Add additional indexes for better query performance
DECLARE
    idx_exists NUMBER := 0;
BEGIN
    -- Check if IDX_THREAT_COMPANY index exists
    SELECT COUNT(*)
    INTO idx_exists
    FROM user_indexes
    WHERE index_name = 'IDX_THREAT_COMPANY'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    IF idx_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_THREAT_COMPANY ON SYS_SECURITY_THREATS(COMPANY_ID)';
        DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_COMPANY created successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_COMPANY already exists.');
    END IF;
    
    -- Check if IDX_THREAT_SEVERITY index exists
    SELECT COUNT(*)
    INTO idx_exists
    FROM user_indexes
    WHERE index_name = 'IDX_THREAT_SEVERITY'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    IF idx_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_THREAT_SEVERITY ON SYS_SECURITY_THREATS(SEVERITY)';
        DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_SEVERITY created successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_SEVERITY already exists.');
    END IF;
    
    -- Check if IDX_THREAT_DETECTION_DATE index exists
    SELECT COUNT(*)
    INTO idx_exists
    FROM user_indexes
    WHERE index_name = 'IDX_THREAT_DETECTION_DATE'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    IF idx_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_THREAT_DETECTION_DATE ON SYS_SECURITY_THREATS(DETECTION_DATE)';
        DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_DETECTION_DATE created successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_DETECTION_DATE already exists.');
    END IF;
END;
/

-- Add check constraints for data integrity
DECLARE
    chk_exists NUMBER := 0;
BEGIN
    -- Check if CHK_THREAT_SEVERITY constraint exists
    SELECT COUNT(*)
    INTO chk_exists
    FROM user_constraints
    WHERE constraint_name = 'CHK_THREAT_SEVERITY'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    IF chk_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_SECURITY_THREATS ADD CONSTRAINT CHK_THREAT_SEVERITY 
                          CHECK (SEVERITY IN (''Critical'', ''High'', ''Medium'', ''Low''))';
        DBMS_OUTPUT.PUT_LINE('Check constraint CHK_THREAT_SEVERITY added successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Check constraint CHK_THREAT_SEVERITY already exists.');
    END IF;
    
    -- Check if CHK_THREAT_STATUS constraint exists
    SELECT COUNT(*)
    INTO chk_exists
    FROM user_constraints
    WHERE constraint_name = 'CHK_THREAT_STATUS'
    AND table_name = 'SYS_SECURITY_THREATS';
    
    IF chk_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_SECURITY_THREATS ADD CONSTRAINT CHK_THREAT_STATUS 
                          CHECK (STATUS IN (''Active'', ''Acknowledged'', ''Resolved'', ''FalsePositive''))';
        DBMS_OUTPUT.PUT_LINE('Check constraint CHK_THREAT_STATUS added successfully.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Check constraint CHK_THREAT_STATUS already exists.');
    END IF;
END;
/

-- Add additional comments for better documentation
COMMENT ON COLUMN SYS_SECURITY_THREATS.USER_ID IS 'Foreign key to SYS_USERS - user associated with the threat (if applicable)';
COMMENT ON COLUMN SYS_SECURITY_THREATS.COMPANY_ID IS 'Foreign key to SYS_COMPANY - company context for the threat';
COMMENT ON COLUMN SYS_SECURITY_THREATS.ACKNOWLEDGED_BY IS 'Foreign key to SYS_USERS - user who acknowledged the threat';
COMMENT ON COLUMN SYS_SECURITY_THREATS.METADATA IS 'Additional threat details in JSON format (request headers, patterns detected, etc.)';

-- Verify the table structure
SELECT 'SYS_SECURITY_THREATS table structure verification:' AS message FROM dual;

SELECT 
    column_name,
    data_type,
    data_length,
    nullable,
    data_default
FROM user_tab_columns 
WHERE table_name = 'SYS_SECURITY_THREATS'
ORDER BY column_id;

-- Verify foreign key constraints
SELECT 'Foreign key constraints on SYS_SECURITY_THREATS:' AS message FROM dual;

SELECT 
    constraint_name,
    constraint_type,
    r_constraint_name,
    status
FROM user_constraints 
WHERE table_name = 'SYS_SECURITY_THREATS'
AND constraint_type = 'R';

-- Verify indexes
SELECT 'Indexes on SYS_SECURITY_THREATS:' AS message FROM dual;

SELECT 
    index_name,
    uniqueness,
    status
FROM user_indexes 
WHERE table_name = 'SYS_SECURITY_THREATS';

COMMIT;

PROMPT 'Task 1.10: SYS_SECURITY_THREATS table foreign key constraints and enhancements completed successfully.';