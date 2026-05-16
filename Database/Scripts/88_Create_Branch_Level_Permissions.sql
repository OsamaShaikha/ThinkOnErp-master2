-- =====================================================
-- Branch-Level Permissions System
-- Allows Super Admin to control module/screen access per branch
-- =====================================================

-- =====================================================
-- 1. SYS_BRANCH_SYSTEM Table
-- Controls which systems/modules are accessible per branch
-- =====================================================
CREATE TABLE SYS_BRANCH_SYSTEM (
    ROW_ID NUMBER(19) PRIMARY KEY,
    BRANCH_ID NUMBER(19) NOT NULL,
    SYSTEM_ID NUMBER(19) NOT NULL,
    IS_ALLOWED CHAR(1) DEFAULT '1' CHECK (IS_ALLOWED IN ('0', '1')),
    GRANTED_BY NUMBER(19),
    GRANTED_DATE DATE DEFAULT SYSDATE,
    REVOKED_DATE DATE,
    NOTES NVARCHAR2(1000),
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE,
    UPDATE_USER NVARCHAR2(100),
    UPDATE_DATE DATE,
    CONSTRAINT FK_BRANCH_SYSTEM_BRANCH FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID),
    CONSTRAINT FK_BRANCH_SYSTEM_SYSTEM FOREIGN KEY (SYSTEM_ID) REFERENCES SYS_SYSTEM(ROW_ID),
    CONSTRAINT FK_BRANCH_SYSTEM_GRANTED_BY FOREIGN KEY (GRANTED_BY) REFERENCES SYS_SUPER_ADMIN(ROW_ID),
    CONSTRAINT UK_BRANCH_SYSTEM UNIQUE (BRANCH_ID, SYSTEM_ID)
);

COMMENT ON TABLE SYS_BRANCH_SYSTEM IS 'System/module access control per branch (allow/block)';
COMMENT ON COLUMN SYS_BRANCH_SYSTEM.BRANCH_ID IS 'Branch ID this permission applies to';
COMMENT ON COLUMN SYS_BRANCH_SYSTEM.SYSTEM_ID IS 'System/module ID (e.g., Accounting, Inventory)';
COMMENT ON COLUMN SYS_BRANCH_SYSTEM.IS_ALLOWED IS '1=Allowed, 0=Blocked';
COMMENT ON COLUMN SYS_BRANCH_SYSTEM.GRANTED_BY IS 'Super Admin who granted/revoked access';
COMMENT ON COLUMN SYS_BRANCH_SYSTEM.GRANTED_DATE IS 'Date when access was granted';
COMMENT ON COLUMN SYS_BRANCH_SYSTEM.REVOKED_DATE IS 'Date when access was revoked (if applicable)';

-- =====================================================
-- 2. SYS_BRANCH_SCREEN Table
-- Controls which screens are accessible per branch
-- =====================================================
CREATE TABLE SYS_BRANCH_SCREEN (
    ROW_ID NUMBER(19) PRIMARY KEY,
    BRANCH_ID NUMBER(19) NOT NULL,
    SCREEN_ID NUMBER(19) NOT NULL,
    IS_ALLOWED CHAR(1) DEFAULT '1' CHECK (IS_ALLOWED IN ('0', '1')),
    GRANTED_BY NUMBER(19),
    GRANTED_DATE DATE DEFAULT SYSDATE,
    REVOKED_DATE DATE,
    NOTES NVARCHAR2(1000),
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE,
    UPDATE_USER NVARCHAR2(100),
    UPDATE_DATE DATE,
    CONSTRAINT FK_BRANCH_SCREEN_BRANCH FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID),
    CONSTRAINT FK_BRANCH_SCREEN_SCREEN FOREIGN KEY (SCREEN_ID) REFERENCES SYS_SCREEN(ROW_ID),
    CONSTRAINT FK_BRANCH_SCREEN_GRANTED_BY FOREIGN KEY (GRANTED_BY) REFERENCES SYS_SUPER_ADMIN(ROW_ID),
    CONSTRAINT UK_BRANCH_SCREEN UNIQUE (BRANCH_ID, SCREEN_ID)
);

COMMENT ON TABLE SYS_BRANCH_SCREEN IS 'Screen access control per branch (allow/block)';
COMMENT ON COLUMN SYS_BRANCH_SCREEN.BRANCH_ID IS 'Branch ID this permission applies to';
COMMENT ON COLUMN SYS_BRANCH_SCREEN.SCREEN_ID IS 'Screen ID (e.g., invoices_list, customers_create)';
COMMENT ON COLUMN SYS_BRANCH_SCREEN.IS_ALLOWED IS '1=Allowed, 0=Blocked';
COMMENT ON COLUMN SYS_BRANCH_SCREEN.GRANTED_BY IS 'Super Admin who granted/revoked access';
COMMENT ON COLUMN SYS_BRANCH_SCREEN.GRANTED_DATE IS 'Date when access was granted';
COMMENT ON COLUMN SYS_BRANCH_SCREEN.REVOKED_DATE IS 'Date when access was revoked (if applicable)';

-- =====================================================
-- Create Indexes for Performance
-- =====================================================
CREATE INDEX IDX_BRANCH_SYSTEM_BRANCH ON SYS_BRANCH_SYSTEM(BRANCH_ID);
CREATE INDEX IDX_BRANCH_SYSTEM_SYSTEM ON SYS_BRANCH_SYSTEM(SYSTEM_ID);
CREATE INDEX IDX_BRANCH_SYSTEM_GRANTED_BY ON SYS_BRANCH_SYSTEM(GRANTED_BY);

CREATE INDEX IDX_BRANCH_SCREEN_BRANCH ON SYS_BRANCH_SCREEN(BRANCH_ID);
CREATE INDEX IDX_BRANCH_SCREEN_SCREEN ON SYS_BRANCH_SCREEN(SCREEN_ID);
CREATE INDEX IDX_BRANCH_SCREEN_GRANTED_BY ON SYS_BRANCH_SCREEN(GRANTED_BY);

-- =====================================================
-- Create Sequences for Primary Keys
-- =====================================================
CREATE SEQUENCE SEQ_SYS_BRANCH_SYSTEM
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

CREATE SEQUENCE SEQ_SYS_BRANCH_SCREEN
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

COMMIT;

-- =====================================================
-- Script Execution Complete
-- =====================================================
-- Usage Notes:
-- 1. Super Admin can grant/revoke system access per branch
-- 2. Super Admin can grant/revoke screen access per branch
-- 3. Permission hierarchy: Company > Branch > Role > User
-- 4. If a system is blocked at branch level, all its screens are inaccessible
-- 5. Screen-level permissions provide finer control within allowed systems
-- =====================================================
