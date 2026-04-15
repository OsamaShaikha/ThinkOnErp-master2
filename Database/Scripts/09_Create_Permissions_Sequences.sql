-- =====================================================
-- Permissions System - Sequences Creation Script
-- Phase 2: Create sequences for all permission tables
-- =====================================================

-- Sequence for SYS_SUPER_ADMIN
CREATE SEQUENCE SEQ_SYS_SUPER_ADMIN
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_SYSTEM
CREATE SEQUENCE SEQ_SYS_SYSTEM
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_SCREEN
CREATE SEQUENCE SEQ_SYS_SCREEN
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_COMPANY_SYSTEM
CREATE SEQUENCE SEQ_SYS_COMPANY_SYSTEM
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_ROLE_SCREEN_PERMISSION
CREATE SEQUENCE SEQ_SYS_ROLE_SCREEN_PERM
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_USER_ROLE
CREATE SEQUENCE SEQ_SYS_USER_ROLE
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_USER_SCREEN_PERMISSION
CREATE SEQUENCE SEQ_SYS_USER_SCREEN_PERM
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Sequence for SYS_AUDIT_LOG
CREATE SEQUENCE SEQ_SYS_AUDIT_LOG
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

COMMIT;

-- =====================================================
-- Script Execution Complete
-- =====================================================
