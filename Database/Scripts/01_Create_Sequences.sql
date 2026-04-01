-- =============================================
-- ThinkOnErp API - Oracle Sequences
-- Description: Creates sequences for primary key generation for all 5 core entities
-- Requirements: 27.1, 27.2, 27.3, 27.4, 27.5
-- =============================================

-- Sequence for SYS_ROLE table
-- Generates unique ROW_ID values for role records
CREATE SEQUENCE SEQ_SYS_ROLE
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_CURRENCY table
-- Generates unique ROW_ID values for currency records
CREATE SEQUENCE SEQ_SYS_CURRENCY
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_COMPANY table
-- Generates unique ROW_ID values for company records
CREATE SEQUENCE SEQ_SYS_COMPANY
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_BRANCH table
-- Generates unique ROW_ID values for branch records
CREATE SEQUENCE SEQ_SYS_BRANCH
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_USERS table
-- Generates unique ROW_ID values for user records
CREATE SEQUENCE SEQ_SYS_USERS
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Verification: Check that all sequences were created successfully
SELECT sequence_name, min_value, max_value, increment_by, last_number
FROM user_sequences
WHERE sequence_name IN (
    'SEQ_SYS_ROLE',
    'SEQ_SYS_CURRENCY',
    'SEQ_SYS_COMPANY',
    'SEQ_SYS_BRANCH',
    'SEQ_SYS_USERS'
)
ORDER BY sequence_name;
