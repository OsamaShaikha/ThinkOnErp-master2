CREATE TABLE SYS_COMPANY
(
  ROW_ID             NUMBER                     NOT NULL,
  ROW_DESC           VARCHAR2(4000 BYTE),
  ROW_DESC_E         VARCHAR2(4000 BYTE),
  COUNTRY_ID         NUMBER,
  CURR_ID            NUMBER,
  IS_ACTIVE          CHAR(1 BYTE),
  CREATION_USER      VARCHAR2(4000 BYTE),
  CREATION_DATE      DATE,
  UPDATE_USER        VARCHAR2(4000 BYTE),
  UPDATE_DATE        DATE,
  LEGAL_NAME         VARCHAR2(300 BYTE),
  LEGAL_NAME_E       VARCHAR2(300 BYTE),
  COMPANY_CODE       VARCHAR2(50 BYTE),
  TAX_NUMBER         VARCHAR2(50 BYTE),
  COMPANY_LOGO       BLOB,
  DEFAULT_BRANCH_ID  NUMBER(19)
)
LOB (COMPANY_LOGO) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING
      STORAGE    (
                  INITIAL          256K
                  NEXT             1M
                  MINEXTENTS       1
                  MAXEXTENTS       UNLIMITED
                  PCTINCREASE      0
                  BUFFER_POOL      DEFAULT
                 ))
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON COLUMN SYS_COMPANY.LEGAL_NAME IS 'Legal name of the company in Arabic';

COMMENT ON COLUMN SYS_COMPANY.LEGAL_NAME_E IS 'Legal name of the company in English';

COMMENT ON COLUMN SYS_COMPANY.COMPANY_CODE IS 'Unique company code for identification';

COMMENT ON COLUMN SYS_COMPANY.TAX_NUMBER IS 'Tax registration number';

COMMENT ON COLUMN SYS_COMPANY.COMPANY_LOGO IS 'Company logo image stored as BLOB';

COMMENT ON COLUMN SYS_COMPANY.DEFAULT_BRANCH_ID IS 'Foreign key to SYS_BRANCH table - references the default/head branch for this company';


CREATE TABLE SYS_CURRENCY
(
  ROW_ID           NUMBER                       NOT NULL,
  ROW_DESC         VARCHAR2(2000 BYTE),
  ROW_DESC_E       VARCHAR2(2000 BYTE),
  SHORT_DESC       VARCHAR2(2000 BYTE),
  SHORT_DESC_E     VARCHAR2(2000 BYTE),
  SINGULER_DESC    VARCHAR2(2000 BYTE),
  SINGULER_DESC_E  VARCHAR2(2000 BYTE),
  DUAL_DESC        VARCHAR2(2000 BYTE),
  DUAL_DESC_E      VARCHAR2(2000 BYTE),
  SUM_DESC         VARCHAR2(2000 BYTE),
  SUM_DESC_E       VARCHAR2(2000 BYTE),
  FRAC_DESC        VARCHAR2(2000 BYTE),
  FRAC_DESC_E      VARCHAR2(2000 BYTE),
  CURR_RATE        NUMBER,
  CURR_RATE_DATE   DATE,
  CREATION_USER    VARCHAR2(4000 BYTE),
  CREATION_DATE    DATE,
  UPDATE_USER      VARCHAR2(4000 BYTE),
  UPDATE_DATE      DATE,
  IS_ACTIVE        CHAR(1 BYTE)
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;


CREATE TABLE SYS_FISCAL_YEAR
(
  ROW_ID            NUMBER,
  COMPANY_ID        NUMBER                      NOT NULL,
  FISCAL_YEAR_CODE  VARCHAR2(20 BYTE)           NOT NULL,
  ROW_DESC          VARCHAR2(200 BYTE),
  ROW_DESC_E        VARCHAR2(200 BYTE),
  START_DATE        DATE                        NOT NULL,
  END_DATE          DATE                        NOT NULL,
  IS_CLOSED         CHAR(1 BYTE)                DEFAULT '0',
  IS_ACTIVE         CHAR(1 BYTE)                DEFAULT '1',
  CREATION_USER     VARCHAR2(100 BYTE),
  CREATION_DATE     DATE                        DEFAULT SYSDATE,
  UPDATE_USER       VARCHAR2(100 BYTE),
  UPDATE_DATE       DATE,
  BRANCH_ID         NUMBER(19)                  NOT NULL
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;


CREATE TABLE SYS_ROLE
(
  ROW_ID         NUMBER,
  ROW_DESC       VARCHAR2(4000 BYTE),
  ROW_DESC_E     VARCHAR2(4000 BYTE),
  NOTE           VARCHAR2(4000 BYTE),
  IS_ACTIVE      CHAR(1 BYTE),
  CREATION_USER  VARCHAR2(4000 BYTE),
  CREATION_DATE  DATE,
  UPDATE_USER    VARCHAR2(4000 BYTE),
  UPDATE_DATE    DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;


CREATE TABLE SYS_SAVED_SEARCH
(
  ROW_ID              NUMBER(19),
  USER_ID             NUMBER(19)                NOT NULL,
  SEARCH_NAME         NVARCHAR2(100)            NOT NULL,
  SEARCH_DESCRIPTION  NVARCHAR2(500),
  SEARCH_CRITERIA     NCLOB                     NOT NULL,
  IS_PUBLIC           CHAR(1 BYTE)              DEFAULT 'N'                   NOT NULL,
  IS_DEFAULT          CHAR(1 BYTE)              DEFAULT 'N'                   NOT NULL,
  USAGE_COUNT         NUMBER(10)                DEFAULT 0                     NOT NULL,
  LAST_USED_DATE      DATE,
  IS_ACTIVE           CHAR(1 BYTE)              DEFAULT 'Y'                   NOT NULL,
  CREATION_USER       NVARCHAR2(100)            NOT NULL,
  CREATION_DATE       DATE                      DEFAULT SYSDATE               NOT NULL,
  UPDATE_USER         NVARCHAR2(100),
  UPDATE_DATE         DATE
)
LOB (SEARCH_CRITERIA) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;


CREATE TABLE SYS_SCREEN
(
  ROW_ID            NUMBER(19),
  SYSTEM_ID         NUMBER(19)                  NOT NULL,
  PARENT_SCREEN_ID  NUMBER(19),
  SCREEN_CODE       NVARCHAR2(100)              NOT NULL,
  SCREEN_NAME       NVARCHAR2(200)              NOT NULL,
  SCREEN_NAME_E     NVARCHAR2(200)              NOT NULL,
  ROUTE             NVARCHAR2(500),
  DESCRIPTION       NVARCHAR2(500),
  DESCRIPTION_E     NVARCHAR2(500),
  ICON              NVARCHAR2(100),
  DISPLAY_ORDER     NUMBER(10)                  DEFAULT 0,
  IS_ACTIVE         CHAR(1 BYTE)                DEFAULT '1',
  CREATION_USER     NVARCHAR2(100)              NOT NULL,
  CREATION_DATE     DATE                        DEFAULT SYSDATE,
  UPDATE_USER       NVARCHAR2(100),
  UPDATE_DATE       DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_SCREEN IS 'Screens/pages within each system';

COMMENT ON COLUMN SYS_SCREEN.PARENT_SCREEN_ID IS 'For nested/hierarchical screens';

COMMENT ON COLUMN SYS_SCREEN.SCREEN_CODE IS 'Unique code identifier (e.g., invoices_list)';

COMMENT ON COLUMN SYS_SCREEN.ROUTE IS 'Frontend route path';


CREATE TABLE SYS_SEARCH_ANALYTICS
(
  ROW_ID             NUMBER(19),
  USER_ID            NUMBER(19)                 NOT NULL,
  SEARCH_TERM        NVARCHAR2(500),
  SEARCH_CRITERIA    NCLOB,
  FILTER_LOGIC       NVARCHAR2(10),
  RESULT_COUNT       NUMBER(10),
  EXECUTION_TIME_MS  NUMBER(10),
  SEARCH_DATE        DATE                       DEFAULT SYSDATE               NOT NULL,
  COMPANY_ID         NUMBER(19),
  BRANCH_ID          NUMBER(19)
)
LOB (SEARCH_CRITERIA) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;


CREATE TABLE SYS_SUPER_ADMIN
(
  ROW_ID                NUMBER(19),
  ROW_DESC              NVARCHAR2(200)          NOT NULL,
  ROW_DESC_E            NVARCHAR2(200)          NOT NULL,
  USER_NAME             NVARCHAR2(100)          NOT NULL,
  PASSWORD              NVARCHAR2(500)          NOT NULL,
  EMAIL                 NVARCHAR2(200),
  PHONE                 NVARCHAR2(50),
  TWO_FA_SECRET         NVARCHAR2(100),
  TWO_FA_ENABLED        CHAR(1 BYTE)            DEFAULT '0',
  IS_ACTIVE             CHAR(1 BYTE)            DEFAULT '1',
  LAST_LOGIN_DATE       DATE,
  CREATION_USER         NVARCHAR2(100)          NOT NULL,
  CREATION_DATE         DATE                    DEFAULT SYSDATE,
  UPDATE_USER           NVARCHAR2(100),
  UPDATE_DATE           DATE,
  REFRESH_TOKEN         NVARCHAR2(500),
  REFRESH_TOKEN_EXPIRY  DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_SUPER_ADMIN IS 'Super Admin accounts with full platform control';

COMMENT ON COLUMN SYS_SUPER_ADMIN.TWO_FA_SECRET IS 'TOTP secret for 2FA authentication';

COMMENT ON COLUMN SYS_SUPER_ADMIN.TWO_FA_ENABLED IS '1=Enabled, 0=Disabled';


CREATE TABLE SYS_SYSTEM
(
  ROW_ID         NUMBER(19),
  SYSTEM_CODE    NVARCHAR2(50)                  NOT NULL,
  SYSTEM_NAME    NVARCHAR2(200)                 NOT NULL,
  SYSTEM_NAME_E  NVARCHAR2(200)                 NOT NULL,
  DESCRIPTION    NVARCHAR2(500),
  DESCRIPTION_E  NVARCHAR2(500),
  ICON           NVARCHAR2(100),
  DISPLAY_ORDER  NUMBER(10)                     DEFAULT 0,
  IS_ACTIVE      CHAR(1 BYTE)                   DEFAULT '1',
  CREATION_USER  NVARCHAR2(100)                 NOT NULL,
  CREATION_DATE  DATE                           DEFAULT SYSDATE,
  UPDATE_USER    NVARCHAR2(100),
  UPDATE_DATE    DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_SYSTEM IS 'Available systems/modules (Accounting, Inventory, HR, etc.)';

COMMENT ON COLUMN SYS_SYSTEM.SYSTEM_CODE IS 'Unique code identifier (e.g., accounting, inventory)';

COMMENT ON COLUMN SYS_SYSTEM.DISPLAY_ORDER IS 'Order for displaying in UI';


CREATE TABLE SYS_THINKON_CLIENTS
(
  ROW_ID           NUMBER,
  COMPANY_NAME     VARCHAR2(2000 BYTE),
  COMPANY_NAME_E   VARCHAR2(2000 BYTE),
  SCHEMA_NAME      VARCHAR2(4000 BYTE),
  SCHEMA_PASSWORD  VARCHAR2(4000 BYTE),
  IS_ACTIVE        CHAR(1 BYTE)                 DEFAULT 'Y'
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;


CREATE TABLE SYS_TICKET_CATEGORY
(
  ROW_ID              NUMBER(19),
  CATEGORY_NAME_AR    NVARCHAR2(100)            NOT NULL,
  CATEGORY_NAME_EN    NVARCHAR2(100)            NOT NULL,
  DESCRIPTION_AR      NVARCHAR2(500),
  DESCRIPTION_EN      NVARCHAR2(500),
  PARENT_CATEGORY_ID  NUMBER(19),
  IS_ACTIVE           CHAR(1 BYTE)              DEFAULT 'Y'                   NOT NULL,
  CREATION_USER       NVARCHAR2(100)            NOT NULL,
  CREATION_DATE       DATE                      DEFAULT SYSDATE               NOT NULL,
  UPDATE_USER         NVARCHAR2(100),
  UPDATE_DATE         DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_TICKET_CATEGORY IS 'Optional ticket categorization with hierarchical support';

COMMENT ON COLUMN SYS_TICKET_CATEGORY.PARENT_CATEGORY_ID IS 'Parent category for hierarchical organization';


CREATE TABLE SYS_TICKET_COMMENT
(
  ROW_ID         NUMBER(19),
  TICKET_ID      NUMBER(19)                     NOT NULL,
  COMMENT_TEXT   NCLOB                          NOT NULL,
  IS_INTERNAL    CHAR(1 BYTE)                   DEFAULT 'N'                   NOT NULL,
  CREATION_USER  NVARCHAR2(100)                 NOT NULL,
  CREATION_DATE  DATE                           DEFAULT SYSDATE               NOT NULL
)
LOB (COMMENT_TEXT) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_TICKET_COMMENT IS 'Ticket comments and communication history';

COMMENT ON COLUMN SYS_TICKET_COMMENT.COMMENT_TEXT IS 'Comment text supporting rich text formatting up to 2000 characters';

COMMENT ON COLUMN SYS_TICKET_COMMENT.IS_INTERNAL IS 'Y for admin-only comments, N for public comments';


CREATE TABLE SYS_TICKET_CONFIG
(
  ROW_ID          NUMBER(19),
  CONFIG_KEY      NVARCHAR2(100)                NOT NULL,
  CONFIG_VALUE    NCLOB                         NOT NULL,
  CONFIG_TYPE     NVARCHAR2(50)                 NOT NULL,
  DESCRIPTION_AR  NVARCHAR2(500),
  DESCRIPTION_EN  NVARCHAR2(500),
  IS_ACTIVE       CHAR(1 BYTE)                  DEFAULT 'Y'                   NOT NULL,
  CREATION_USER   NVARCHAR2(100)                NOT NULL,
  CREATION_DATE   DATE                          DEFAULT SYSDATE               NOT NULL,
  UPDATE_USER     NVARCHAR2(100),
  UPDATE_DATE     DATE
)
LOB (CONFIG_VALUE) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING
      STORAGE    (
                  INITIAL          256K
                  NEXT             1M
                  MINEXTENTS       1
                  MAXEXTENTS       UNLIMITED
                  PCTINCREASE      0
                  BUFFER_POOL      DEFAULT
                 ))
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;


CREATE TABLE SYS_TICKET_PRIORITY
(
  ROW_ID                      NUMBER(19),
  PRIORITY_NAME_AR            NVARCHAR2(50)     NOT NULL,
  PRIORITY_NAME_EN            NVARCHAR2(50)     NOT NULL,
  PRIORITY_LEVEL              NUMBER(1)         NOT NULL,
  SLA_TARGET_HOURS            NUMBER(10,2)      NOT NULL,
  ESCALATION_THRESHOLD_HOURS  NUMBER(10,2)      NOT NULL,
  IS_ACTIVE                   CHAR(1 BYTE)      DEFAULT 'Y'                   NOT NULL,
  CREATION_USER               NVARCHAR2(100)    NOT NULL,
  CREATION_DATE               DATE              DEFAULT SYSDATE               NOT NULL,
  UPDATE_USER                 NVARCHAR2(100),
  UPDATE_DATE                 DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_TICKET_PRIORITY IS 'Ticket priority levels with SLA targets and escalation thresholds';

COMMENT ON COLUMN SYS_TICKET_PRIORITY.PRIORITY_LEVEL IS 'Numeric priority level (1=Critical, 2=High, 3=Medium, 4=Low)';

COMMENT ON COLUMN SYS_TICKET_PRIORITY.SLA_TARGET_HOURS IS 'Target resolution time in hours';

COMMENT ON COLUMN SYS_TICKET_PRIORITY.ESCALATION_THRESHOLD_HOURS IS 'Hours before escalation alert';


CREATE TABLE SYS_TICKET_STATUS
(
  ROW_ID           NUMBER(19),
  STATUS_NAME_AR   NVARCHAR2(50)                NOT NULL,
  STATUS_NAME_EN   NVARCHAR2(50)                NOT NULL,
  STATUS_CODE      NVARCHAR2(20)                NOT NULL,
  DISPLAY_ORDER    NUMBER(3)                    NOT NULL,
  IS_FINAL_STATUS  CHAR(1 BYTE)                 DEFAULT 'N'                   NOT NULL,
  IS_ACTIVE        CHAR(1 BYTE)                 DEFAULT 'Y'                   NOT NULL,
  CREATION_USER    NVARCHAR2(100)               NOT NULL,
  CREATION_DATE    DATE                         DEFAULT SYSDATE               NOT NULL,
  UPDATE_USER      NVARCHAR2(100),
  UPDATE_DATE      DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_TICKET_STATUS IS 'Ticket status definitions for workflow management';

COMMENT ON COLUMN SYS_TICKET_STATUS.STATUS_CODE IS 'Unique code for programmatic status identification';

COMMENT ON COLUMN SYS_TICKET_STATUS.DISPLAY_ORDER IS 'Order for status display in UI';

COMMENT ON COLUMN SYS_TICKET_STATUS.IS_FINAL_STATUS IS 'Y if status represents ticket completion (Closed/Cancelled)';


CREATE TABLE SYS_TICKET_TYPE
(
  ROW_ID               NUMBER(19),
  TYPE_NAME_AR         NVARCHAR2(100)           NOT NULL,
  TYPE_NAME_EN         NVARCHAR2(100)           NOT NULL,
  DESCRIPTION_AR       NVARCHAR2(500),
  DESCRIPTION_EN       NVARCHAR2(500),
  DEFAULT_PRIORITY_ID  NUMBER(19)               NOT NULL,
  SLA_TARGET_HOURS     NUMBER(10,2)             NOT NULL,
  IS_ACTIVE            CHAR(1 BYTE)             DEFAULT 'Y'                   NOT NULL,
  CREATION_USER        NVARCHAR2(100)           NOT NULL,
  CREATION_DATE        DATE                     DEFAULT SYSDATE               NOT NULL,
  UPDATE_USER          NVARCHAR2(100),
  UPDATE_DATE          DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_TICKET_TYPE IS 'Ticket type definitions with default priority and SLA settings';

COMMENT ON COLUMN SYS_TICKET_TYPE.DEFAULT_PRIORITY_ID IS 'Default priority assigned to new tickets of this type';

COMMENT ON COLUMN SYS_TICKET_TYPE.SLA_TARGET_HOURS IS 'Type-specific SLA target (overrides priority default if specified)';


CREATE TABLE SYS_USERS
(
  ROW_ID                NUMBER                  NOT NULL,
  ROW_DESC              VARCHAR2(4000 BYTE),
  ROW_DESC_E            VARCHAR2(4000 BYTE),
  USER_NAME             VARCHAR2(4000 BYTE),
  PASSWORD              VARCHAR2(4000 BYTE),
  PHONE                 VARCHAR2(4000 BYTE),
  PHONE2                VARCHAR2(4000 BYTE),
  ROLE                  NUMBER,
  BRANCH_ID             NUMBER,
  EMAIL                 VARCHAR2(4000 BYTE),
  LAST_LOGIN_DATE       DATE,
  IS_ACTIVE             CHAR(1 BYTE),
  IS_ADMIN              CHAR(1 BYTE),
  CREATION_USER         VARCHAR2(4000 BYTE),
  CREATION_DATE         DATE,
  UPDATE_USER           VARCHAR2(4000 BYTE),
  UPDATE_DATE           DATE,
  REFRESH_TOKEN         VARCHAR2(500 BYTE),
  REFRESH_TOKEN_EXPIRY  DATE,
  IS_SUPER_ADMIN        CHAR(1 BYTE)            DEFAULT '0',
  FORCE_LOGOUT_DATE     DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON COLUMN SYS_USERS.REFRESH_TOKEN IS 'Refresh token for JWT authentication';

COMMENT ON COLUMN SYS_USERS.REFRESH_TOKEN_EXPIRY IS 'Expiration date for the refresh token';

COMMENT ON COLUMN SYS_USERS.IS_SUPER_ADMIN IS '1=Super Admin (full platform access), 0=Regular user';

COMMENT ON COLUMN SYS_USERS.FORCE_LOGOUT_DATE IS 'Date when user was force logged out. Tokens issued before this date are invalid.';


CREATE TABLE SYS_USER_ROLE
(
  ROW_ID         NUMBER(19),
  USER_ID        NUMBER(19)                     NOT NULL,
  ROLE_ID        NUMBER(19)                     NOT NULL,
  ASSIGNED_BY    NUMBER(19),
  ASSIGNED_DATE  DATE                           DEFAULT SYSDATE,
  CREATION_USER  NVARCHAR2(100)                 NOT NULL,
  CREATION_DATE  DATE                           DEFAULT SYSDATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_USER_ROLE IS 'User to role assignments';

COMMENT ON COLUMN SYS_USER_ROLE.ASSIGNED_BY IS 'User ID who assigned this role';


CREATE TABLE SYS_USER_SCREEN_PERMISSION
(
  ROW_ID         NUMBER(19),
  USER_ID        NUMBER(19)                     NOT NULL,
  SCREEN_ID      NUMBER(19)                     NOT NULL,
  CAN_VIEW       CHAR(1 BYTE)                   DEFAULT '0',
  CAN_INSERT     CHAR(1 BYTE)                   DEFAULT '0',
  CAN_UPDATE     CHAR(1 BYTE)                   DEFAULT '0',
  CAN_DELETE     CHAR(1 BYTE)                   DEFAULT '0',
  ASSIGNED_BY    NUMBER(19),
  ASSIGNED_DATE  DATE                           DEFAULT SYSDATE,
  NOTES          NVARCHAR2(1000),
  CREATION_USER  NVARCHAR2(100)                 NOT NULL,
  CREATION_DATE  DATE                           DEFAULT SYSDATE,
  UPDATE_USER    NVARCHAR2(100),
  UPDATE_DATE    DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_USER_SCREEN_PERMISSION IS 'Direct user-level permission overrides (takes precedence over role permissions)';

COMMENT ON COLUMN SYS_USER_SCREEN_PERMISSION.ASSIGNED_BY IS 'Super Admin or Company Admin who set this override';


CREATE INDEX IDX_COMMENT_DATE ON SYS_TICKET_COMMENT
(CREATION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_COMMENT_TICKET ON SYS_TICKET_COMMENT
(TICKET_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_COMMENT_USER ON SYS_TICKET_COMMENT
(CREATION_USER)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_FISCAL_YEAR_BRANCH ON SYS_FISCAL_YEAR
(BRANCH_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_FISCAL_YEAR_COMPANY ON SYS_FISCAL_YEAR
(COMPANY_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_FISCAL_YEAR_DATES ON SYS_FISCAL_YEAR
(START_DATE, END_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SAVED_SEARCH_NAME ON SYS_SAVED_SEARCH
(SEARCH_NAME)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SAVED_SEARCH_PUBLIC ON SYS_SAVED_SEARCH
(IS_PUBLIC, IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SAVED_SEARCH_USER ON SYS_SAVED_SEARCH
(USER_ID, IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SCREEN_PARENT ON SYS_SCREEN
(PARENT_SCREEN_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SCREEN_SYSTEM ON SYS_SCREEN
(SYSTEM_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SEARCH_ANALYTICS_COMPANY ON SYS_SEARCH_ANALYTICS
(COMPANY_ID, SEARCH_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SEARCH_ANALYTICS_DATE ON SYS_SEARCH_ANALYTICS
(SEARCH_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SEARCH_ANALYTICS_TERM ON SYS_SEARCH_ANALYTICS
(SEARCH_TERM)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_SEARCH_ANALYTICS_USER ON SYS_SEARCH_ANALYTICS
(USER_ID, SEARCH_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_CATEGORY_ACTIVE ON SYS_TICKET_CATEGORY
(IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_CONFIG_ACTIVE ON SYS_TICKET_CONFIG
(IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_CONFIG_TYPE ON SYS_TICKET_CONFIG
(CONFIG_TYPE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_PRIORITY_ACTIVE ON SYS_TICKET_PRIORITY
(IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_STATUS_ACTIVE ON SYS_TICKET_STATUS
(IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_TYPE_ACTIVE ON SYS_TICKET_TYPE
(IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_USER_ROLE_ROLE ON SYS_USER_ROLE
(ROLE_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_USER_ROLE_USER ON SYS_USER_ROLE
(USER_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_USER_SCREEN_PERM_SCREEN ON SYS_USER_SCREEN_PERMISSION
(SCREEN_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_USER_SCREEN_PERM_USER ON SYS_USER_SCREEN_PERMISSION
(USER_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX PK_SYS_COMPANY ON SYS_COMPANY
(ROW_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX PK_SYS_USERS ON SYS_USERS
(ROW_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

--  There is no statement for index THINKON_ERP.SYS_C008311.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008343.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008344.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008345.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008351.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008352.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008359.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008360.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008386.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008397.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008415.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008437.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008449.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008450.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008459.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008467.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008499.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008525.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008535.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008536.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008539.
--  The object is created when the parent object is created.

CREATE UNIQUE INDEX SYS_CURRENCY_PK ON SYS_CURRENCY
(ROW_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX SYS_ROLE_PK ON SYS_ROLE
(ROW_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX SYS_THINKON_CLIENTS_PK ON SYS_THINKON_CLIENTS
(ROW_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX UK_COMPANY_CODE ON SYS_COMPANY
(COMPANY_CODE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX UK_FISCAL_YEAR_CODE ON SYS_FISCAL_YEAR
(COMPANY_ID, FISCAL_YEAR_CODE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX UK_TICKET_PRIORITY_LEVEL ON SYS_TICKET_PRIORITY
(PRIORITY_LEVEL)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX UK_USER_ROLE ON SYS_USER_ROLE
(USER_ID, ROLE_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX UK_USER_SCREEN ON SYS_USER_SCREEN_PERMISSION
(USER_ID, SCREEN_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE TABLE SYS_BRANCH
(
  ROW_ID            NUMBER                      NOT NULL,
  PAR_ROW_ID        NUMBER,
  ROW_DESC          VARCHAR2(4000 BYTE),
  ROW_DESC_E        VARCHAR2(4000 BYTE),
  PHONE             VARCHAR2(4000 BYTE),
  MOBILE            VARCHAR2(4000 BYTE),
  FAX               VARCHAR2(4000 BYTE),
  EMAIL             VARCHAR2(4000 BYTE),
  IS_HEAD_BRANCH    CHAR(1 BYTE),
  IS_ACTIVE         CHAR(1 BYTE),
  CREATION_USER     VARCHAR2(4000 BYTE),
  CREATION_DATE     DATE,
  UPDATE_USER       VARCHAR2(4000 BYTE),
  UPDATE_DATE       DATE,
  BRANCH_LOGO       BLOB,
  DEFAULT_LANG      VARCHAR2(10 BYTE)           DEFAULT 'ar',
  BASE_CURRENCY_ID  NUMBER,
  ROUNDING_RULES    NUMBER                      DEFAULT NULL
)
LOB (BRANCH_LOGO) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING
      STORAGE    (
                  INITIAL          256K
                  NEXT             1M
                  MINEXTENTS       1
                  MAXEXTENTS       UNLIMITED
                  PCTINCREASE      0
                  BUFFER_POOL      DEFAULT
                 ))
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON COLUMN SYS_BRANCH.BRANCH_LOGO IS 'Branch logo image stored as BLOB (max 5MB)';

COMMENT ON COLUMN SYS_BRANCH.DEFAULT_LANG IS 'Default language for the branch (ar/en)';

COMMENT ON COLUMN SYS_BRANCH.BASE_CURRENCY_ID IS 'Base currency for the branch operations';

COMMENT ON COLUMN SYS_BRANCH.ROUNDING_RULES IS 'Rounding rules for calculations (1=HALF_UP, 2=HALF_DOWN, 3=UP, 4=DOWN, 5=CEILING, 6=FLOOR)';


CREATE TABLE SYS_COMPANY_SYSTEM
(
  ROW_ID         NUMBER(19),
  COMPANY_ID     NUMBER(19)                     NOT NULL,
  SYSTEM_ID      NUMBER(19)                     NOT NULL,
  IS_ALLOWED     CHAR(1 BYTE)                   DEFAULT '1',
  GRANTED_BY     NUMBER(19),
  GRANTED_DATE   DATE                           DEFAULT SYSDATE,
  REVOKED_DATE   DATE,
  NOTES          NVARCHAR2(1000),
  CREATION_USER  NVARCHAR2(100)                 NOT NULL,
  CREATION_DATE  DATE                           DEFAULT SYSDATE,
  UPDATE_USER    NVARCHAR2(100),
  UPDATE_DATE    DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_COMPANY_SYSTEM IS 'System access control per company (allow/block)';

COMMENT ON COLUMN SYS_COMPANY_SYSTEM.IS_ALLOWED IS '1=Allowed, 0=Blocked';

COMMENT ON COLUMN SYS_COMPANY_SYSTEM.GRANTED_BY IS 'Super Admin who granted/revoked access';


CREATE TABLE SYS_REQUEST_TICKET
(
  ROW_ID                    NUMBER(19),
  TITLE_AR                  NVARCHAR2(200)      NOT NULL,
  TITLE_EN                  NVARCHAR2(200)      NOT NULL,
  DESCRIPTION               NCLOB               NOT NULL,
  COMPANY_ID                NUMBER(19)          NOT NULL,
  BRANCH_ID                 NUMBER(19)          NOT NULL,
  REQUESTER_ID              NUMBER(19)          NOT NULL,
  ASSIGNEE_ID               NUMBER(19),
  TICKET_TYPE_ID            NUMBER(19)          NOT NULL,
  TICKET_STATUS_ID          NUMBER(19)          NOT NULL,
  TICKET_PRIORITY_ID        NUMBER(19)          NOT NULL,
  TICKET_CATEGORY_ID        NUMBER(19),
  EXPECTED_RESOLUTION_DATE  DATE,
  ACTUAL_RESOLUTION_DATE    DATE,
  IS_ACTIVE                 CHAR(1 BYTE)        DEFAULT 'Y'                   NOT NULL,
  CREATION_USER             NVARCHAR2(100)      NOT NULL,
  CREATION_DATE             DATE                DEFAULT SYSDATE               NOT NULL,
  UPDATE_USER               NVARCHAR2(100),
  UPDATE_DATE               DATE
)
LOB (DESCRIPTION) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING
      STORAGE    (
                  INITIAL          256K
                  NEXT             1M
                  MINEXTENTS       1
                  MAXEXTENTS       UNLIMITED
                  PCTINCREASE      0
                  BUFFER_POOL      DEFAULT
                 ))
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_REQUEST_TICKET IS 'Main ticket entity with multilingual support and comprehensive tracking';

COMMENT ON COLUMN SYS_REQUEST_TICKET.TITLE_AR IS 'Ticket title in Arabic';

COMMENT ON COLUMN SYS_REQUEST_TICKET.TITLE_EN IS 'Ticket title in English';

COMMENT ON COLUMN SYS_REQUEST_TICKET.DESCRIPTION IS 'Detailed ticket description supporting rich text up to 5000 characters';

COMMENT ON COLUMN SYS_REQUEST_TICKET.EXPECTED_RESOLUTION_DATE IS 'Calculated based on SLA targets and priority';

COMMENT ON COLUMN SYS_REQUEST_TICKET.ACTUAL_RESOLUTION_DATE IS 'Set when ticket status changes to Resolved';


CREATE TABLE SYS_ROLE_SCREEN_PERMISSION
(
  ROW_ID         NUMBER(19),
  ROLE_ID        NUMBER(19)                     NOT NULL,
  SCREEN_ID      NUMBER(19)                     NOT NULL,
  CAN_VIEW       CHAR(1 BYTE)                   DEFAULT '0',
  CAN_INSERT     CHAR(1 BYTE)                   DEFAULT '0',
  CAN_UPDATE     CHAR(1 BYTE)                   DEFAULT '0',
  CAN_DELETE     CHAR(1 BYTE)                   DEFAULT '0',
  CREATION_USER  NVARCHAR2(100)                 NOT NULL,
  CREATION_DATE  DATE                           DEFAULT SYSDATE,
  UPDATE_USER    NVARCHAR2(100),
  UPDATE_DATE    DATE
)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_ROLE_SCREEN_PERMISSION IS 'Granular screen permissions per role';

COMMENT ON COLUMN SYS_ROLE_SCREEN_PERMISSION.CAN_VIEW IS 'Can view/read the screen';

COMMENT ON COLUMN SYS_ROLE_SCREEN_PERMISSION.CAN_INSERT IS 'Can create new records';

COMMENT ON COLUMN SYS_ROLE_SCREEN_PERMISSION.CAN_UPDATE IS 'Can edit existing records';

COMMENT ON COLUMN SYS_ROLE_SCREEN_PERMISSION.CAN_DELETE IS 'Can delete records';


CREATE TABLE SYS_TICKET_ATTACHMENT
(
  ROW_ID         NUMBER(19),
  TICKET_ID      NUMBER(19)                     NOT NULL,
  FILE_NAME      NVARCHAR2(255)                 NOT NULL,
  FILE_SIZE      NUMBER(19)                     NOT NULL,
  MIME_TYPE      NVARCHAR2(100)                 NOT NULL,
  FILE_CONTENT   BLOB                           NOT NULL,
  CREATION_USER  NVARCHAR2(100)                 NOT NULL,
  CREATION_DATE  DATE                           DEFAULT SYSDATE               NOT NULL
)
LOB (FILE_CONTENT) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_TICKET_ATTACHMENT IS 'File attachments stored as BLOB with metadata';

COMMENT ON COLUMN SYS_TICKET_ATTACHMENT.FILE_SIZE IS 'File size in bytes (max 10MB)';

COMMENT ON COLUMN SYS_TICKET_ATTACHMENT.MIME_TYPE IS 'File MIME type for validation and download';

COMMENT ON COLUMN SYS_TICKET_ATTACHMENT.FILE_CONTENT IS 'Binary file content stored as BLOB';


CREATE INDEX IDX_ATTACHMENT_DATE ON SYS_TICKET_ATTACHMENT
(CREATION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_ATTACHMENT_TICKET ON SYS_TICKET_ATTACHMENT
(TICKET_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_BRANCH_BASE_CURRENCY ON SYS_BRANCH
(BASE_CURRENCY_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_COMPANY_SYSTEM_COMPANY ON SYS_COMPANY_SYSTEM
(COMPANY_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_COMPANY_SYSTEM_SYSTEM ON SYS_COMPANY_SYSTEM
(SYSTEM_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_ROLE_SCREEN_PERM_ROLE ON SYS_ROLE_SCREEN_PERMISSION
(ROLE_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_ROLE_SCREEN_PERM_SCREEN ON SYS_ROLE_SCREEN_PERMISSION
(SCREEN_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_ACTIVE ON SYS_REQUEST_TICKET
(IS_ACTIVE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_ASSIGNEE ON SYS_REQUEST_TICKET
(ASSIGNEE_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_COMPANY_BRANCH ON SYS_REQUEST_TICKET
(COMPANY_ID, BRANCH_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_CREATION_DATE ON SYS_REQUEST_TICKET
(CREATION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_EXPECTED_DATE ON SYS_REQUEST_TICKET
(EXPECTED_RESOLUTION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_REQUESTER ON SYS_REQUEST_TICKET
(REQUESTER_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_RESOLUTION_DATE ON SYS_REQUEST_TICKET
(ACTUAL_RESOLUTION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_STATUS_PRIORITY ON SYS_REQUEST_TICKET
(TICKET_STATUS_ID, TICKET_PRIORITY_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_TITLE_AR ON SYS_REQUEST_TICKET
(TITLE_AR)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_TITLE_EN ON SYS_REQUEST_TICKET
(TITLE_EN)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_TICKET_TYPE ON SYS_REQUEST_TICKET
(TICKET_TYPE_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX PK_SYS_BRANCH ON SYS_BRANCH
(ROW_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

--  There is no statement for index THINKON_ERP.SYS_C008367.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008379.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008484.
--  The object is created when the parent object is created.

--  There is no statement for index THINKON_ERP.SYS_C008509.
--  The object is created when the parent object is created.

CREATE UNIQUE INDEX UK_COMPANY_SYSTEM ON SYS_COMPANY_SYSTEM
(COMPANY_ID, SYSTEM_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE UNIQUE INDEX UK_ROLE_SCREEN ON SYS_ROLE_SCREEN_PERMISSION
(ROLE_ID, SCREEN_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            INITIAL          64K
            NEXT             1M
            MINEXTENTS       1
            MAXEXTENTS       UNLIMITED
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE TABLE SYS_AUDIT_LOG
(
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
  LOGGING)
LOB (NEW_VALUE) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
LOB (OLD_VALUE) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
LOB (REQUEST_PAYLOAD) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
LOB (RESPONSE_PAYLOAD) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
LOB (STACK_TRACE) STORE AS SECUREFILE (
  TABLESPACE  USERS
  ENABLE      STORAGE IN ROW
  CHUNK       8192
  NOCACHE
  LOGGING)
TABLESPACE USERS
PCTUSED    0
PCTFREE    10
INITRANS   1
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           )
LOGGING 
NOCOMPRESS 
NOCACHE
MONITORING;

COMMENT ON TABLE SYS_AUDIT_LOG IS 'Comprehensive audit trail for all system changes';

COMMENT ON COLUMN SYS_AUDIT_LOG.ACTOR_TYPE IS 'Type of user performing action';

COMMENT ON COLUMN SYS_AUDIT_LOG.ACTION IS 'Action performed (CREATE, UPDATE, DELETE, etc.)';

COMMENT ON COLUMN SYS_AUDIT_LOG.OLD_VALUE IS 'JSON of old values';

COMMENT ON COLUMN SYS_AUDIT_LOG.NEW_VALUE IS 'JSON of new values';

COMMENT ON COLUMN SYS_AUDIT_LOG.CORRELATION_ID IS 'Unique identifier tracking request through system';

COMMENT ON COLUMN SYS_AUDIT_LOG.BRANCH_ID IS 'Foreign key to SYS_BRANCH table for multi-tenant operations';

COMMENT ON COLUMN SYS_AUDIT_LOG.HTTP_METHOD IS 'HTTP method of the API request (GET, POST, PUT, DELETE)';

COMMENT ON COLUMN SYS_AUDIT_LOG.ENDPOINT_PATH IS 'API endpoint path that was called';

COMMENT ON COLUMN SYS_AUDIT_LOG.REQUEST_PAYLOAD IS 'JSON request body (sensitive data masked)';

COMMENT ON COLUMN SYS_AUDIT_LOG.RESPONSE_PAYLOAD IS 'JSON response body (sensitive data masked)';

COMMENT ON COLUMN SYS_AUDIT_LOG.EXECUTION_TIME_MS IS 'Total execution time in milliseconds';

COMMENT ON COLUMN SYS_AUDIT_LOG.STATUS_CODE IS 'HTTP status code of the response';

COMMENT ON COLUMN SYS_AUDIT_LOG.EXCEPTION_TYPE IS 'Type of exception if error occurred';

COMMENT ON COLUMN SYS_AUDIT_LOG.EXCEPTION_MESSAGE IS 'Exception message if error occurred';

COMMENT ON COLUMN SYS_AUDIT_LOG.STACK_TRACE IS 'Full stack trace if exception occurred';

COMMENT ON COLUMN SYS_AUDIT_LOG.SEVERITY IS 'Severity level: Critical, Error, Warning, Info';

COMMENT ON COLUMN SYS_AUDIT_LOG.EVENT_CATEGORY IS 'Category: DataChange, Authentication, Permission, Exception, Configuration, Request';

COMMENT ON COLUMN SYS_AUDIT_LOG.METADATA IS 'Additional JSON metadata for extensibility';


CREATE INDEX IDX_AUDIT_LOG_ACTOR ON SYS_AUDIT_LOG
(ACTOR_ID, ACTOR_TYPE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_ACTOR_DATE ON SYS_AUDIT_LOG
(ACTOR_ID, CREATION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_BRANCH ON SYS_AUDIT_LOG
(BRANCH_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_CATEGORY ON SYS_AUDIT_LOG
(EVENT_CATEGORY)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_COMPANY ON SYS_AUDIT_LOG
(COMPANY_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_COMPANY_DATE ON SYS_AUDIT_LOG
(COMPANY_ID, CREATION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_CORRELATION ON SYS_AUDIT_LOG
(CORRELATION_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_DATE ON SYS_AUDIT_LOG
(CREATION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_ENDPOINT ON SYS_AUDIT_LOG
(ENDPOINT_PATH)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_ENTITY ON SYS_AUDIT_LOG
(ENTITY_TYPE, ENTITY_ID)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_ENTITY_DATE ON SYS_AUDIT_LOG
(ENTITY_TYPE, ENTITY_ID, CREATION_DATE)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

CREATE INDEX IDX_AUDIT_LOG_SEVERITY ON SYS_AUDIT_LOG
(SEVERITY)
LOGGING
TABLESPACE USERS
PCTFREE    10
INITRANS   2
MAXTRANS   255
STORAGE    (
            PCTINCREASE      0
            BUFFER_POOL      DEFAULT
           );

--  There is no statement for index THINKON_ERP.SYS_C008406.
--  The object is created when the parent object is created.

ALTER TABLE SYS_COMPANY ADD (
  CONSTRAINT PK_SYS_COMPANY
  PRIMARY KEY
  (ROW_ID)
  USING INDEX PK_SYS_COMPANY
  ENABLE VALIDATE
,  CONSTRAINT UK_COMPANY_CODE
  UNIQUE (COMPANY_CODE)
  USING INDEX UK_COMPANY_CODE
  ENABLE VALIDATE);

ALTER TABLE SYS_CURRENCY ADD (
  CONSTRAINT SYS_CURRENCY_PK
  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_FISCAL_YEAR ADD (
  CHECK (IS_CLOSED IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (IS_ACTIVE IN ('0', '1'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  CONSTRAINT UK_FISCAL_YEAR_CODE
  UNIQUE (COMPANY_ID, FISCAL_YEAR_CODE)
  USING INDEX UK_FISCAL_YEAR_CODE
  ENABLE VALIDATE);

ALTER TABLE SYS_ROLE ADD (
  CONSTRAINT SYS_ROLE_PK
  PRIMARY KEY
  (ROW_ID)
  USING INDEX SYS_ROLE_PK
  ENABLE VALIDATE);

ALTER TABLE SYS_SAVED_SEARCH ADD (
  CONSTRAINT CHK_SAVED_SEARCH_ACTIVE
  CHECK (IS_ACTIVE IN ('Y', 'N'))
  ENABLE VALIDATE
,  CONSTRAINT CHK_SAVED_SEARCH_DEFAULT
  CHECK (IS_DEFAULT IN ('Y', 'N'))
  ENABLE VALIDATE
,  CONSTRAINT CHK_SAVED_SEARCH_PUBLIC
  CHECK (IS_PUBLIC IN ('Y', 'N'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_SCREEN ADD (
  CHECK (IS_ACTIVE IN ('0', '1'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  UNIQUE (SCREEN_CODE)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_SEARCH_ANALYTICS ADD (
  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_SUPER_ADMIN ADD (
  CHECK (TWO_FA_ENABLED IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (IS_ACTIVE IN ('0', '1'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  UNIQUE (USER_NAME)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  UNIQUE (EMAIL)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_SYSTEM ADD (
  CHECK (IS_ACTIVE IN ('0', '1'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  UNIQUE (SYSTEM_CODE)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_THINKON_CLIENTS ADD (
  CONSTRAINT SYS_THINKON_CLIENTS_PK
  PRIMARY KEY
  (ROW_ID)
  USING INDEX SYS_THINKON_CLIENTS_PK
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_CATEGORY ADD (
  CHECK (IS_ACTIVE IN ('Y', 'N'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_COMMENT ADD (
  CHECK (IS_INTERNAL IN ('Y', 'N'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_CONFIG ADD (
  CONSTRAINT CHK_TICKET_CONFIG_ACTIVE
  CHECK (IS_ACTIVE IN ('Y', 'N'))
  ENABLE VALIDATE
,  CONSTRAINT CHK_TICKET_CONFIG_TYPE
  CHECK (CONFIG_TYPE IN ('SLA', 'FileAttachment', 'Notification', 'Workflow', 'General'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  UNIQUE (CONFIG_KEY)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_PRIORITY ADD (
  CHECK (IS_ACTIVE IN ('Y', 'N'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  CONSTRAINT UK_TICKET_PRIORITY_LEVEL
  UNIQUE (PRIORITY_LEVEL)
  USING INDEX UK_TICKET_PRIORITY_LEVEL
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_STATUS ADD (
  CHECK (IS_FINAL_STATUS IN ('Y', 'N'))
  ENABLE VALIDATE
,  CHECK (IS_ACTIVE IN ('Y', 'N'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  UNIQUE (STATUS_CODE)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_TYPE ADD (
  CHECK (IS_ACTIVE IN ('Y', 'N'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_USERS ADD (
  CHECK (IS_SUPER_ADMIN IN ('0', '1'))
  ENABLE VALIDATE
,  CONSTRAINT PK_SYS_USERS
  PRIMARY KEY
  (ROW_ID)
  USING INDEX PK_SYS_USERS
  ENABLE VALIDATE
,  UNIQUE (USER_NAME)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   167
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_USER_ROLE ADD (
  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  CONSTRAINT UK_USER_ROLE
  UNIQUE (USER_ID, ROLE_ID)
  USING INDEX UK_USER_ROLE
  ENABLE VALIDATE);

ALTER TABLE SYS_USER_SCREEN_PERMISSION ADD (
  CHECK (CAN_VIEW IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (CAN_INSERT IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (CAN_UPDATE IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (CAN_DELETE IN ('0', '1'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  CONSTRAINT UK_USER_SCREEN
  UNIQUE (USER_ID, SCREEN_ID)
  USING INDEX UK_USER_SCREEN
  ENABLE VALIDATE);

ALTER TABLE SYS_BRANCH ADD (
  CONSTRAINT CHK_BRANCH_DEFAULT_LANG
  CHECK (DEFAULT_LANG IN ('ar', 'en'))
  ENABLE VALIDATE
,  CONSTRAINT PK_SYS_BRANCH
  PRIMARY KEY
  (ROW_ID)
  USING INDEX PK_SYS_BRANCH
  ENABLE VALIDATE);

ALTER TABLE SYS_COMPANY_SYSTEM ADD (
  CHECK (IS_ALLOWED IN ('0', '1'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  CONSTRAINT UK_COMPANY_SYSTEM
  UNIQUE (COMPANY_ID, SYSTEM_ID)
  USING INDEX UK_COMPANY_SYSTEM
  ENABLE VALIDATE);

ALTER TABLE SYS_REQUEST_TICKET ADD (
  CONSTRAINT CHK_EXPECTED_DATE
  CHECK (EXPECTED_RESOLUTION_DATE IS NULL OR EXPECTED_RESOLUTION_DATE >= CREATION_DATE)
  ENABLE VALIDATE
,  CONSTRAINT CHK_RESOLUTION_DATE
  CHECK (ACTUAL_RESOLUTION_DATE IS NULL OR ACTUAL_RESOLUTION_DATE >= CREATION_DATE)
  ENABLE VALIDATE
,  CHECK (IS_ACTIVE IN ('Y', 'N'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_ROLE_SCREEN_PERMISSION ADD (
  CHECK (CAN_VIEW IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (CAN_INSERT IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (CAN_UPDATE IN ('0', '1'))
  ENABLE VALIDATE
,  CHECK (CAN_DELETE IN ('0', '1'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                INITIAL          64K
                NEXT             1M
                MINEXTENTS       1
                MAXEXTENTS       UNLIMITED
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE
,  CONSTRAINT UK_ROLE_SCREEN
  UNIQUE (ROLE_ID, SCREEN_ID)
  USING INDEX UK_ROLE_SCREEN
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_ATTACHMENT ADD (
  CONSTRAINT CHK_FILE_SIZE
  CHECK (FILE_SIZE > 0 AND FILE_SIZE <= 10485760)
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_AUDIT_LOG ADD (
  CHECK (ACTOR_TYPE IN ('SUPER_ADMIN', 'COMPANY_ADMIN', 'USER'))
  ENABLE VALIDATE
,  PRIMARY KEY
  (ROW_ID)
  USING INDEX
    TABLESPACE USERS
    PCTFREE    10
    INITRANS   2
    MAXTRANS   255
    STORAGE    (
                PCTINCREASE      0
                BUFFER_POOL      DEFAULT
               )
  ENABLE VALIDATE);

ALTER TABLE SYS_COMPANY ADD (
  CONSTRAINT FK_COMPANY_DEFAULT_BRANCH 
  FOREIGN KEY (DEFAULT_BRANCH_ID) 
  REFERENCES SYS_BRANCH (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT SYS_COMPANY_CURRENCY_FK 
  FOREIGN KEY (CURR_ID) 
  REFERENCES SYS_CURRENCY (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_FISCAL_YEAR ADD (
  CONSTRAINT FK_FISCAL_YEAR_BRANCH 
  FOREIGN KEY (BRANCH_ID) 
  REFERENCES SYS_BRANCH (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_FISCAL_YEAR_COMPANY 
  FOREIGN KEY (COMPANY_ID) 
  REFERENCES SYS_COMPANY (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_SAVED_SEARCH ADD (
  CONSTRAINT FK_SAVED_SEARCH_USER 
  FOREIGN KEY (USER_ID) 
  REFERENCES SYS_USERS (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_SCREEN ADD (
  CONSTRAINT FK_SCREEN_PARENT 
  FOREIGN KEY (PARENT_SCREEN_ID) 
  REFERENCES SYS_SCREEN (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_SCREEN_SYSTEM 
  FOREIGN KEY (SYSTEM_ID) 
  REFERENCES SYS_SYSTEM (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_SEARCH_ANALYTICS ADD (
  CONSTRAINT FK_SEARCH_ANALYTICS_BRANCH 
  FOREIGN KEY (BRANCH_ID) 
  REFERENCES SYS_BRANCH (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_SEARCH_ANALYTICS_COMPANY 
  FOREIGN KEY (COMPANY_ID) 
  REFERENCES SYS_COMPANY (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_SEARCH_ANALYTICS_USER 
  FOREIGN KEY (USER_ID) 
  REFERENCES SYS_USERS (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_CATEGORY ADD (
  CONSTRAINT FK_CATEGORY_PARENT 
  FOREIGN KEY (PARENT_CATEGORY_ID) 
  REFERENCES SYS_TICKET_CATEGORY (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_COMMENT ADD (
  CONSTRAINT FK_COMMENT_TICKET 
  FOREIGN KEY (TICKET_ID) 
  REFERENCES SYS_REQUEST_TICKET (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_TYPE ADD (
  CONSTRAINT FK_TYPE_DEFAULT_PRIORITY 
  FOREIGN KEY (DEFAULT_PRIORITY_ID) 
  REFERENCES SYS_TICKET_PRIORITY (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_USERS ADD (
  CONSTRAINT SYS_USERS_BRANCH_FK 
  FOREIGN KEY (BRANCH_ID) 
  REFERENCES SYS_BRANCH (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT SYS_USERS_ROLE_FK 
  FOREIGN KEY (ROLE) 
  REFERENCES SYS_ROLE (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_USER_ROLE ADD (
  CONSTRAINT FK_USER_ROLE_ROLE 
  FOREIGN KEY (ROLE_ID) 
  REFERENCES SYS_ROLE (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_USER_ROLE_USER 
  FOREIGN KEY (USER_ID) 
  REFERENCES SYS_USERS (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_USER_SCREEN_PERMISSION ADD (
  CONSTRAINT FK_USER_SCREEN_PERM_SCREEN 
  FOREIGN KEY (SCREEN_ID) 
  REFERENCES SYS_SCREEN (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_USER_SCREEN_PERM_USER 
  FOREIGN KEY (USER_ID) 
  REFERENCES SYS_USERS (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_BRANCH ADD (
  CONSTRAINT FK_BRANCH_BASE_CURRENCY 
  FOREIGN KEY (BASE_CURRENCY_ID) 
  REFERENCES SYS_CURRENCY (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT SYS_BRANCH_COMPANY_FK 
  FOREIGN KEY (PAR_ROW_ID) 
  REFERENCES SYS_COMPANY (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_COMPANY_SYSTEM ADD (
  CONSTRAINT FK_COMPANY_SYSTEM_COMPANY 
  FOREIGN KEY (COMPANY_ID) 
  REFERENCES SYS_COMPANY (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_COMPANY_SYSTEM_GRANTED_BY 
  FOREIGN KEY (GRANTED_BY) 
  REFERENCES SYS_SUPER_ADMIN (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_COMPANY_SYSTEM_SYSTEM 
  FOREIGN KEY (SYSTEM_ID) 
  REFERENCES SYS_SYSTEM (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_REQUEST_TICKET ADD (
  CONSTRAINT FK_TICKET_ASSIGNEE 
  FOREIGN KEY (ASSIGNEE_ID) 
  REFERENCES SYS_USERS (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_TICKET_BRANCH 
  FOREIGN KEY (BRANCH_ID) 
  REFERENCES SYS_BRANCH (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_TICKET_CATEGORY 
  FOREIGN KEY (TICKET_CATEGORY_ID) 
  REFERENCES SYS_TICKET_CATEGORY (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_TICKET_COMPANY 
  FOREIGN KEY (COMPANY_ID) 
  REFERENCES SYS_COMPANY (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_TICKET_PRIORITY 
  FOREIGN KEY (TICKET_PRIORITY_ID) 
  REFERENCES SYS_TICKET_PRIORITY (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_TICKET_REQUESTER 
  FOREIGN KEY (REQUESTER_ID) 
  REFERENCES SYS_USERS (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_TICKET_STATUS 
  FOREIGN KEY (TICKET_STATUS_ID) 
  REFERENCES SYS_TICKET_STATUS (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_TICKET_TYPE 
  FOREIGN KEY (TICKET_TYPE_ID) 
  REFERENCES SYS_TICKET_TYPE (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_ROLE_SCREEN_PERMISSION ADD (
  CONSTRAINT FK_ROLE_SCREEN_PERM_ROLE 
  FOREIGN KEY (ROLE_ID) 
  REFERENCES SYS_ROLE (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_ROLE_SCREEN_PERM_SCREEN 
  FOREIGN KEY (SCREEN_ID) 
  REFERENCES SYS_SCREEN (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_TICKET_ATTACHMENT ADD (
  CONSTRAINT FK_ATTACHMENT_TICKET 
  FOREIGN KEY (TICKET_ID) 
  REFERENCES SYS_REQUEST_TICKET (ROW_ID)
  ENABLE VALIDATE);

ALTER TABLE SYS_AUDIT_LOG ADD (
  CONSTRAINT FK_AUDIT_LOG_BRANCH 
  FOREIGN KEY (BRANCH_ID) 
  REFERENCES SYS_BRANCH (ROW_ID)
  ENABLE VALIDATE
,  CONSTRAINT FK_AUDIT_LOG_COMPANY 
  FOREIGN KEY (COMPANY_ID) 
  REFERENCES SYS_COMPANY (ROW_ID)
  ENABLE VALIDATE);
