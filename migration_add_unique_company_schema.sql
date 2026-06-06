-- Migration: Add unique index on COMPANY_SCHEMA in SYS_COMPANY
-- Generated: 2026-06-07

-- First, resolve any existing duplicate NULL COMPANY_SCHEMA values (Oracle allows multiple NULLs in unique indexes)
-- Create the unique index
CREATE UNIQUE INDEX "IX_SYS_COMPANY_SCHEMA" ON "THINKON_ERP"."SYS_COMPANY" ("COMPANY_SCHEMA");

-- Update the EF Migration History
-- Note: If you want to track this in EF migrations, you would also insert into __EFMigrationsHistory.
-- For manual tracking, you can run:
-- INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") 
-- VALUES ('20260607000000_AddUniqueCompanySchema', '8.0.0');

EXIT;
