-- =============================================
-- Script: 76_Create_Report_Schedule_Table.sql
-- Description: Creates table for storing scheduled report configurations
-- Author: System
-- Date: 2024
-- =============================================

-- Create sequence for report schedule IDs
CREATE SEQUENCE SEQ_SYS_REPORT_SCHEDULE
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Create report schedule table
CREATE TABLE SYS_REPORT_SCHEDULE (
    ROW_ID NUMBER(19) PRIMARY KEY,
    REPORT_TYPE NVARCHAR2(100) NOT NULL,
    FREQUENCY NVARCHAR2(20) NOT NULL, -- Daily, Weekly, Monthly
    DAY_OF_WEEK NUMBER(1), -- 1=Monday, 7=Sunday (for weekly reports)
    DAY_OF_MONTH NUMBER(2), -- 1-31 (for monthly reports)
    TIME_OF_DAY NVARCHAR2(5) DEFAULT '02:00', -- HH:mm format
    RECIPIENTS NVARCHAR2(1000) NOT NULL, -- Comma-separated email addresses
    EXPORT_FORMAT NVARCHAR2(20) NOT NULL, -- PDF, CSV, JSON
    PARAMETERS CLOB, -- JSON format for report-specific parameters
    IS_ACTIVE NUMBER(1) DEFAULT 1,
    CREATED_BY_USER_ID NUMBER(19) NOT NULL,
    CREATED_AT DATE DEFAULT SYSDATE,
    LAST_GENERATED_AT DATE,
    LAST_GENERATION_STATUS NVARCHAR2(50), -- Success, Failed, InProgress
    LAST_ERROR_MESSAGE NVARCHAR2(4000),
    CONSTRAINT FK_REPORT_SCHEDULE_USER FOREIGN KEY (CREATED_BY_USER_ID) REFERENCES SYS_USERS(ROW_ID),
    CONSTRAINT CHK_FREQUENCY CHECK (FREQUENCY IN ('Daily', 'Weekly', 'Monthly')),
    CONSTRAINT CHK_DAY_OF_WEEK CHECK (DAY_OF_WEEK IS NULL OR (DAY_OF_WEEK >= 1 AND DAY_OF_WEEK <= 7)),
    CONSTRAINT CHK_DAY_OF_MONTH CHECK (DAY_OF_MONTH IS NULL OR (DAY_OF_MONTH >= 1 AND DAY_OF_MONTH <= 31)),
    CONSTRAINT CHK_EXPORT_FORMAT CHECK (EXPORT_FORMAT IN ('PDF', 'CSV', 'JSON')),
    CONSTRAINT CHK_IS_ACTIVE CHECK (IS_ACTIVE IN (0, 1))
);

-- Create indexes for efficient querying
CREATE INDEX IDX_REPORT_SCHEDULE_ACTIVE ON SYS_REPORT_SCHEDULE(IS_ACTIVE, FREQUENCY);
CREATE INDEX IDX_REPORT_SCHEDULE_NEXT_RUN ON SYS_REPORT_SCHEDULE(LAST_GENERATED_AT, IS_ACTIVE);
CREATE INDEX IDX_REPORT_SCHEDULE_CREATED_BY ON SYS_REPORT_SCHEDULE(CREATED_BY_USER_ID);

-- Add comments
COMMENT ON TABLE SYS_REPORT_SCHEDULE IS 'Stores scheduled report generation configurations for compliance reports';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.REPORT_TYPE IS 'Type of report: GDPR_Access, GDPR_Export, SOX_Financial, SOX_Segregation, ISO27001_Security, UserActivity, DataModification';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.FREQUENCY IS 'Report generation frequency: Daily, Weekly, Monthly';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.DAY_OF_WEEK IS 'Day of week for weekly reports (1=Monday, 7=Sunday)';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.DAY_OF_MONTH IS 'Day of month for monthly reports (1-31)';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.TIME_OF_DAY IS 'Time of day to generate report in HH:mm format (24-hour)';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.RECIPIENTS IS 'Comma-separated list of email addresses to receive the report';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.EXPORT_FORMAT IS 'Export format: PDF, CSV, JSON';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.PARAMETERS IS 'JSON-formatted report-specific parameters (e.g., date ranges, filters)';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.IS_ACTIVE IS 'Whether the schedule is active (1) or disabled (0)';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.LAST_GENERATED_AT IS 'Timestamp of last successful report generation';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.LAST_GENERATION_STATUS IS 'Status of last generation attempt: Success, Failed, InProgress';
COMMENT ON COLUMN SYS_REPORT_SCHEDULE.LAST_ERROR_MESSAGE IS 'Error message if last generation failed';

-- Insert sample scheduled reports for testing
INSERT INTO SYS_REPORT_SCHEDULE (
    ROW_ID,
    REPORT_TYPE,
    FREQUENCY,
    DAY_OF_WEEK,
    DAY_OF_MONTH,
    TIME_OF_DAY,
    RECIPIENTS,
    EXPORT_FORMAT,
    PARAMETERS,
    IS_ACTIVE,
    CREATED_BY_USER_ID
) VALUES (
    SEQ_SYS_REPORT_SCHEDULE.NEXTVAL,
    'GDPR_Access',
    'Weekly',
    1, -- Monday
    NULL,
    '02:00',
    'compliance@example.com',
    'PDF',
    '{"startDateOffset": -7, "endDateOffset": 0}',
    1,
    1 -- Super Admin
);

INSERT INTO SYS_REPORT_SCHEDULE (
    ROW_ID,
    REPORT_TYPE,
    FREQUENCY,
    DAY_OF_WEEK,
    DAY_OF_MONTH,
    TIME_OF_DAY,
    RECIPIENTS,
    EXPORT_FORMAT,
    PARAMETERS,
    IS_ACTIVE,
    CREATED_BY_USER_ID
) VALUES (
    SEQ_SYS_REPORT_SCHEDULE.NEXTVAL,
    'SOX_Financial',
    'Monthly',
    NULL,
    1, -- First day of month
    '03:00',
    'finance@example.com,audit@example.com',
    'CSV',
    '{"startDateOffset": -30, "endDateOffset": 0}',
    1,
    1 -- Super Admin
);

INSERT INTO SYS_REPORT_SCHEDULE (
    ROW_ID,
    REPORT_TYPE,
    FREQUENCY,
    DAY_OF_WEEK,
    DAY_OF_MONTH,
    TIME_OF_DAY,
    RECIPIENTS,
    EXPORT_FORMAT,
    PARAMETERS,
    IS_ACTIVE,
    CREATED_BY_USER_ID
) VALUES (
    SEQ_SYS_REPORT_SCHEDULE.NEXTVAL,
    'ISO27001_Security',
    'Daily',
    NULL,
    NULL,
    '01:00',
    'security@example.com',
    'JSON',
    '{"startDateOffset": -1, "endDateOffset": 0}',
    1,
    1 -- Super Admin
);

COMMIT;

