# Database Scripts Execution Summary

## Complete Script Execution Order

Execute the scripts in the following order for a fresh database setup:

### Core Setup (Existing Scripts)
1. `01_Create_Sequences.sql` - Creates sequences for all core tables
2. `02_Create_SYS_ROLE_Procedures.sql` - Role table and procedures
3. `03_Create_SYS_CURRENCY_Procedures.sql` - Currency table and procedures
4. `04_Create_SYS_BRANCH_Procedures.sql` - Branch table and procedures
5. `04_Create_SYS_COMPANY_Procedures.sql` - Company table and procedures (original)
6. `05_Create_SYS_USERS_Procedures.sql` - Users table and procedures
7. `06_Insert_Test_Data.sql` - Initial test data for all core tables

### Authentication & Security Extensions
8. `07_Add_RefreshToken_To_Users.sql` - Adds refresh token support
9. `08_Create_Permissions_Tables.sql` - Permission system tables
10. `09_Create_Permissions_Sequences.sql` - Permission sequences
11. `10_Create_Permissions_Procedures.sql` - Permission procedures
12. `11_Insert_Permissions_Seed_Data.sql` - Permission seed data
13. `12_Add_Force_Logout_Column.sql` - Force logout feature

### Audit & Monitoring
14. `13_Extend_SYS_AUDIT_LOG_For_Traceability.sql` - Enhanced audit logging
15. `14_Create_Audit_Archive_Table.sql` - Audit archiving
16. `15_Create_Performance_Metrics_Tables.sql` - Performance monitoring
17. `16_Create_Security_Monitoring_Tables.sql` - Security monitoring
18. `17_Create_Retention_Policy_Table.sql` - Data retention policies

### **NEW: Fiscal Year & Company Extensions**
19. **`18_Create_SYS_FISCAL_YEAR_Table.sql`** - Fiscal year table and procedures
20. **`19_Extend_SYS_COMPANY_Table.sql`** - Add new columns to company table
21. **`20_Update_SYS_COMPANY_Procedures.sql`** - Update company procedures
22. **`21_Insert_Fiscal_Year_Test_Data.sql`** - Fiscal year test data
23. **`22_Update_Company_Test_Data.sql`** - Update company test data with new fields

### **Company & Branch Field Migration**
24. **`23_Create_Company_With_Default_Branch.sql`** - Company with default branch creation
25. **`32_Move_Fields_From_Company_To_Branch.sql`** - Move fields from company to branch level

### **SystemLanguage Removal**
26. **`33_Remove_SystemLanguage_Column.sql`** - Remove SYSTEM_LANGUAGE column from SYS_COMPANY

### **Final Procedure Updates**
27. **`34_Recreate_Company_Procedures_Final.sql`** - Recreate all company procedures after field migration

## Quick Execution Commands

### Execute All New Scripts (Oracle SQL*Plus)
```bash
sqlplus username/password@database <<EOF
@Database/Scripts/18_Create_SYS_FISCAL_YEAR_Table.sql
@Database/Scripts/19_Extend_SYS_COMPANY_Table.sql
@Database/Scripts/20_Update_SYS_COMPANY_Procedures.sql
@Database/Scripts/21_Insert_Fiscal_Year_Test_Data.sql
@Database/Scripts/22_Update_Company_Test_Data.sql
@Database/Scripts/23_Create_Company_With_Default_Branch.sql
@Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql
@Database/Scripts/33_Remove_SystemLanguage_Column.sql
@Database/Scripts/34_Recreate_Company_Procedures_Final.sql
EXIT;
EOF
```

### Execute Individual Scripts
```bash
# Create fiscal year table
sqlplus username/password@database @Database/Scripts/18_Create_SYS_FISCAL_YEAR_Table.sql

# Extend company table
sqlplus username/password@database @Database/Scripts/19_Extend_SYS_COMPANY_Table.sql

# Update company procedures
sqlplus username/password@database @Database/Scripts/20_Update_SYS_COMPANY_Procedures.sql

# Insert fiscal year test data
sqlplus username/password@database @Database/Scripts/21_Insert_Fiscal_Year_Test_Data.sql

# Update company test data
sqlplus username/password@database @Database/Scripts/22_Update_Company_Test_Data.sql

# Create company with branch procedure
sqlplus username/password@database @Database/Scripts/23_Create_Company_With_Default_Branch.sql

# Move fields from company to branch
sqlplus username/password@database @Database/Scripts/32_Move_Fields_From_Company_To_Branch.sql

# Remove SystemLanguage column
sqlplus username/password@database @Database/Scripts/33_Remove_SystemLanguage_Column.sql

# Recreate company procedures (final version)
sqlplus username/password@database @Database/Scripts/34_Recreate_Company_Procedures_Final.sql
```

## Verification Queries

### Check Fiscal Year Table
```sql
SELECT * FROM user_tables WHERE table_name = 'SYS_FISCAL_YEAR';
SELECT * FROM user_sequences WHERE sequence_name = 'SEQ_SYS_FISCAL_YEAR';
```

### Check Company Table Columns
```sql
SELECT column_name, data_type, data_length, nullable, data_default
FROM user_tab_columns
WHERE table_name = 'SYS_COMPANY'
ORDER BY column_id;
```

### Check Fiscal Year Procedures
```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name LIKE 'SP_SYS_FISCAL_YEAR%'
ORDER BY object_name;
```

### Check Updated Company Procedures
```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name LIKE 'SP_SYS_COMPANY%'
ORDER BY object_name;
```

### View Test Data
```sql
-- View fiscal years
SELECT 
    fy.ROW_ID,
    fy.COMPANY_ID,
    c.ROW_DESC_E AS COMPANY_NAME,
    fy.FISCAL_YEAR_CODE,
    fy.ROW_DESC_E,
    fy.START_DATE,
    fy.END_DATE,
    fy.IS_CLOSED,
    fy.IS_ACTIVE
FROM SYS_FISCAL_YEAR fy
JOIN SYS_COMPANY c ON fy.COMPANY_ID = c.ROW_ID
ORDER BY fy.COMPANY_ID, fy.START_DATE;

-- View companies with new fields
SELECT 
    c.ROW_ID,
    c.ROW_DESC_E AS COMPANY_NAME,
    c.LEGAL_NAME_E,
    c.COMPANY_CODE,
    c.TAX_NUMBER,
    c.DEFAULT_LANG,
    c.SYSTEM_LANGUAGE,
    c.ROUNDING_RULES,
    fy.FISCAL_YEAR_CODE,
    curr.ROW_DESC_E AS BASE_CURRENCY,
    c.IS_ACTIVE
FROM SYS_COMPANY c
LEFT JOIN SYS_FISCAL_YEAR fy ON c.FISCAL_YEAR_ID = fy.ROW_ID
LEFT JOIN SYS_CURRENCY curr ON c.BASE_CURRENCY_ID = curr.ROW_ID
WHERE c.IS_ACTIVE = '1'
ORDER BY c.ROW_ID;
```

## Rollback Instructions

If you need to rollback the changes:

```sql
-- Drop new procedures
DROP PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_ALL;
DROP PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_BY_ID;
DROP PROCEDURE SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY;
DROP PROCEDURE SP_SYS_FISCAL_YEAR_INSERT;
DROP PROCEDURE SP_SYS_FISCAL_YEAR_UPDATE;
DROP PROCEDURE SP_SYS_FISCAL_YEAR_DELETE;
DROP PROCEDURE SP_SYS_FISCAL_YEAR_CLOSE;
DROP PROCEDURE SP_SYS_COMPANY_UPDATE_LOGO;
DROP PROCEDURE SP_SYS_COMPANY_GET_LOGO;

-- Remove foreign key constraints from company table
ALTER TABLE SYS_COMPANY DROP CONSTRAINT FK_COMPANY_FISCAL_YEAR;
ALTER TABLE SYS_COMPANY DROP CONSTRAINT FK_COMPANY_BASE_CURRENCY;

-- Drop fiscal year table
DROP TABLE SYS_FISCAL_YEAR;
DROP SEQUENCE SEQ_SYS_FISCAL_YEAR;

-- Remove new columns from company table
ALTER TABLE SYS_COMPANY DROP COLUMN LEGAL_NAME;
ALTER TABLE SYS_COMPANY DROP COLUMN LEGAL_NAME_E;
ALTER TABLE SYS_COMPANY DROP COLUMN COMPANY_CODE;
ALTER TABLE SYS_COMPANY DROP COLUMN DEFAULT_LANG;
ALTER TABLE SYS_COMPANY DROP COLUMN TAX_NUMBER;
ALTER TABLE SYS_COMPANY DROP COLUMN FISCAL_YEAR_ID;
ALTER TABLE SYS_COMPANY DROP COLUMN BASE_CURRENCY_ID;
ALTER TABLE SYS_COMPANY DROP COLUMN SYSTEM_LANGUAGE;
ALTER TABLE SYS_COMPANY DROP COLUMN ROUNDING_RULES;
ALTER TABLE SYS_COMPANY DROP COLUMN COMPANY_LOGO;

-- Restore original company procedures
@Database/Scripts/04_Create_SYS_COMPANY_Procedures.sql
```

## Notes

- All scripts include verification queries at the end
- Scripts use proper error handling with RAISE_APPLICATION_ERROR
- All procedures follow the existing naming conventions
- Foreign key constraints ensure data integrity
- Indexes are created for optimal query performance
- Test data is provided for development and testing purposes

## Support

For detailed information about the fiscal year and company extensions, see:
- `Database/FISCAL_YEAR_AND_COMPANY_EXTENSION.md`

For general database information, see:
- `Database/README.md`
- `Database/TEST_DATA_README.md`
