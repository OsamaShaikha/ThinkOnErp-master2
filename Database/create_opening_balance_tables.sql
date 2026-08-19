-- ============================================================
-- Opening Balance Dedicated Tables — DDL Migration Script
-- ThinkOnErp System
-- Date: 2026-08-17
-- ============================================================
-- Run this script once per tenant schema (per company schema)
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- TABLE 1: GL_OPENING_BALANCE_HEADER
-- One row per branch per fiscal year (unique constraint enforced)
-- STATUS: 1=Draft (editable), 2=Confirmed (locked)
-- OB_VOUCHER_ID: set after Confirm — links to GL_VOUCHER_HEADER
-- ─────────────────────────────────────────────────────────────
CREATE TABLE "GL_OPENING_BALANCE_HEADER" (
    "ID"              NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "BRANCH_ID"       NUMBER NOT NULL,
    "FISCAL_YEAR_ID"  NUMBER NOT NULL,
    "AS_OF_DATE"      DATE NOT NULL,
    "DESCRIPTION"     NVARCHAR2(500),
    "STATUS"          NUMBER(1) DEFAULT 1 NOT NULL,
    "TOTAL_DEBIT"     NUMBER(18,3) DEFAULT 0,
    "TOTAL_CREDIT"    NUMBER(18,3) DEFAULT 0,
    "OB_VOUCHER_ID"   NUMBER,
    "CREATION_USER"   NVARCHAR2(100) NOT NULL,
    "CREATION_DATE"   TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
    "UPDATE_USER"     NVARCHAR2(100),
    "UPDATE_DATE"     TIMESTAMP,
    CONSTRAINT "CK_GL_OB_STATUS" CHECK ("STATUS" IN (1, 2))
);

-- One OB per branch per fiscal year
CREATE UNIQUE INDEX "UX_GL_OB_BRANCH_YEAR"
    ON "GL_OPENING_BALANCE_HEADER" ("BRANCH_ID", "FISCAL_YEAR_ID");

-- ─────────────────────────────────────────────────────────────
-- TABLE 2: GL_OPENING_BALANCE_DETAIL
-- One row per account within the Opening Balance header
-- Only DEBIT or CREDIT per line — never both
-- ─────────────────────────────────────────────────────────────
CREATE TABLE "GL_OPENING_BALANCE_DETAIL" (
    "ID"              NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "HEADER_ID"       NUMBER NOT NULL,
    "LINE_SER"        NUMBER(5) NOT NULL,
    "ACCOUNT_CODE"    NVARCHAR2(50) NOT NULL,
    "DEBIT_AMOUNT"    NUMBER(18,3) DEFAULT 0,
    "CREDIT_AMOUNT"   NUMBER(18,3) DEFAULT 0,
    "LOCAL_DEBIT"     NUMBER(18,3) DEFAULT 0,
    "LOCAL_CREDIT"    NUMBER(18,3) DEFAULT 0,
    "CURRENCY_ID"     NUMBER DEFAULT 1,
    "EXCHANGE_RATE"   NUMBER(14,6) DEFAULT 1,
    "DESCRIPTION"     NVARCHAR2(500),
    CONSTRAINT "FK_GL_OB_DETAIL_HEADER"
        FOREIGN KEY ("HEADER_ID")
        REFERENCES "GL_OPENING_BALANCE_HEADER" ("ID")
        ON DELETE CASCADE,
    CONSTRAINT "CK_GL_OB_DEBIT_CREDIT"
        CHECK (NOT ("DEBIT_AMOUNT" > 0 AND "CREDIT_AMOUNT" > 0))
);

-- Unique line number per header
CREATE UNIQUE INDEX "UX_GL_OB_DETAIL_LINE"
    ON "GL_OPENING_BALANCE_DETAIL" ("HEADER_ID", "LINE_SER");

-- Index for account-based queries (balance reports)
CREATE INDEX "IX_GL_OB_DETAIL_ACCOUNT"
    ON "GL_OPENING_BALANCE_DETAIL" ("ACCOUNT_CODE");

-- ─────────────────────────────────────────────────────────────
-- VERIFY
-- ─────────────────────────────────────────────────────────────
SELECT 'GL_OPENING_BALANCE_HEADER created' AS STATUS FROM DUAL
UNION ALL
SELECT 'GL_OPENING_BALANCE_DETAIL created' AS STATUS FROM DUAL;
