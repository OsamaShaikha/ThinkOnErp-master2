# ThinkOnErp API - Database Scripts

This directory contains Oracle database scripts for the ThinkOnErp API system.

## Directory Structure

```
Database/
├── Scripts/
│   ├── 01_Create_Sequences.sql           # Creates sequences for primary key generation
│   ├── 02_Create_SYS_ROLE_Procedures.sql # CRUD stored procedures for SYS_ROLE table
│   ├── 03_Create_SYS_CURRENCY_Procedures.sql # CRUD stored procedures for SYS_CURRENCY table
│   ├── 04_Create_SYS_COMPANY_Procedures.sql # CRUD stored procedures for SYS_COMPANY table
│   ├── 04_Create_SYS_BRANCH_Procedures.sql # CRUD stored procedures for SYS_BRANCH table
│   ├── 05_Create_SYS_USERS_Procedures.sql # CRUD stored procedures for SYS_USERS table
│   ├── 06_Insert_Test_Data.sql           # Test data for development
│   ├── 07_Add_RefreshToken_To_Users.sql  # Adds refresh token support
│   ├── 08_Create_Permissions_Tables.sql  # Multi-tenant permissions tables
│   ├── 09_Create_Permissions_Sequences.sql # Sequences for permissions tables
│   ├── 10_Create_Permissions_Procedures.sql # CRUD procedures for permissions
│   ├── 11_Insert_Permissions_Seed_Data.sql # Demo systems and screens
│   ├── 12_Add_Force_Logout_Column.sql    # Force logout feature
│   ├── 13_Extend_SYS_AUDIT_LOG_For_Traceability.sql # Enhanced audit logging
│   ├── 14_Create_Audit_Archive_Table.sql # Audit archiving
│   ├── 15_Create_Performance_Metrics_Tables.sql # Performance monitoring
│   ├── 16_Create_Security_Monitoring_Tables.sql # Security monitoring
│   ├── 17_Create_Retention_Policy_Table.sql # Data retention policies
│   ├── 18_Create_SYS_FISCAL_YEAR_Table.sql # Fiscal year table and procedures
│   ├── 19_Extend_SYS_COMPANY_Table.sql   # Extends company table with new columns
│   ├── 20_Update_SYS_COMPANY_Procedures.sql # Updates company procedures
│   ├── 21_Insert_Fiscal_Year_Test_Data.sql # Fiscal year test data
│   └── 22_Update_Company_Test_Data.sql   # Updates company test data
├── README.md
├── TEST_DATA_README.md
├── FISCAL_YEAR_AND_COMPANY_EXTENSION.md  # Detailed documentation for new features
└── SCRIPT_EXECUTION_SUMMARY.md           # Complete execution order guide
```

## Execution Order

Execute the scripts in numerical order:

### Core System Setup (Required)

1. **01_Create_Sequences.sql** - Creates sequences for all 5 core entities
   - SEQ_SYS_ROLE
   - SEQ_SYS_CURRENCY
   - SEQ_SYS_COMPANY
   - SEQ_SYS_BRANCH
   - SEQ_SYS_USERS

2. **02_Create_SYS_ROLE_Procedures.sql** - Creates CRUD stored procedures for SYS_ROLE table
3. **03_Create_SYS_CURRENCY_Procedures.sql** - Creates CRUD stored procedures for SYS_CURRENCY table
4. **04_Create_SYS_COMPANY_Procedures.sql** - Creates CRUD stored procedures for SYS_COMPANY table
5. **04_Create_SYS_BRANCH_Procedures.sql** - Creates CRUD stored procedures for SYS_BRANCH table
6. **05_Create_SYS_USERS_Procedures.sql** - Creates CRUD stored procedures for SYS_USERS table
7. **06_Insert_Test_Data.sql** - Inserts test data for development (optional but recommended)
8. **07_Add_RefreshToken_To_Users.sql** - Adds refresh token columns to SYS_USERS table

### Permissions System Setup (Optional - for multi-tenant permissions)

9. **08_Create_Permissions_Tables.sql** - Creates permission tables
   - SYS_SUPER_ADMIN
   - SYS_SYSTEM
   - SYS_SCREEN
   - SYS_COMPANY_SYSTEM
   - SYS_ROLE_SCREEN_PERMISSION
   - SYS_USER_ROLE
   - SYS_USER_SCREEN_PERMISSION
   - SYS_AUDIT_LOG

10. **09_Create_Permissions_Sequences.sql** - Creates sequences for permission tables
    - SEQ_SYS_SUPER_ADMIN
    - SEQ_SYS_SYSTEM
    - SEQ_SYS_SCREEN
    - SEQ_SYS_COMPANY_SYSTEM
    - SEQ_SYS_ROLE_SCREEN_PERM
    - SEQ_SYS_USER_ROLE
    - SEQ_SYS_USER_SCREEN_PERM
    - SEQ_SYS_AUDIT_LOG

11. **10_Create_Permissions_Procedures.sql** - Creates CRUD procedures for permissions
    - System management procedures
    - Screen management procedures
    - Company system assignment procedures
    - Role screen permission procedures
    - User role assignment procedures
    - User screen permission override procedures
    - FN_CHECK_USER_PERMISSION function

12. **11_Insert_Permissions_Seed_Data.sql** - Inserts demo data
    - 5 Systems (Accounting, Inventory, HR, CRM, POS)
    - 24 Screens across all systems
    - Demo company system assignments
    - Demo role screen permissions

### Security & Monitoring Extensions

13. **12_Add_Force_Logout_Column.sql** - Adds force logout feature to users table
14. **13_Extend_SYS_AUDIT_LOG_For_Traceability.sql** - Enhanced audit logging with traceability
15. **14_Create_Audit_Archive_Table.sql** - Audit log archiving functionality
16. **15_Create_Performance_Metrics_Tables.sql** - Performance monitoring tables
17. **16_Create_Security_Monitoring_Tables.sql** - Security event monitoring
18. **17_Create_Retention_Policy_Table.sql** - Data retention policy management

### Fiscal Year & Company Extensions (NEW)

19. **18_Create_SYS_FISCAL_YEAR_Table.sql** - Creates fiscal year table and procedures
    - SYS_FISCAL_YEAR table
    - SEQ_SYS_FISCAL_YEAR sequence
    - CRUD procedures for fiscal year management
    - Fiscal year closing functionality

20. **19_Extend_SYS_COMPANY_Table.sql** - Adds new columns to company table
    - LEGAL_NAME, LEGAL_NAME_E (Legal names)
    - COMPANY_CODE (Unique company identifier)
    - DEFAULT_LANG, SYSTEM_LANGUAGE (Language preferences)
    - TAX_NUMBER (Tax registration)
    - FISCAL_YEAR_ID (Current fiscal year)
    - BASE_CURRENCY_ID (Base currency)
    - ROUNDING_RULES (Calculation rounding)
    - COMPANY_LOGO (Logo image as BLOB)

21. **20_Update_SYS_COMPANY_Procedures.sql** - Updates company procedures
    - Updated SELECT, INSERT, UPDATE procedures
    - New SP_SYS_COMPANY_UPDATE_LOGO procedure
    - New SP_SYS_COMPANY_GET_LOGO procedure

22. **21_Insert_Fiscal_Year_Test_Data.sql** - Inserts fiscal year test data
    - Multiple fiscal years for test companies
    - Open and closed fiscal year examples

23. **22_Update_Company_Test_Data.sql** - Updates existing company test data
    - Populates new company fields with sample data
    - Links companies to fiscal years and currencies

## How to Execute

### Using SQL*Plus

```bash
sqlplus username/password@database
@Database/Scripts/01_Create_Sequences.sql
@Database/Scripts/02_Create_SYS_ROLE_Procedures.sql
@Database/Scripts/03_Create_SYS_CURRENCY_Procedures.sql
@Database/Scripts/04_Create_SYS_BRANCH_Procedures.sql
@Database/Scripts/05_Create_SYS_USERS_Procedures.sql
```

### Using Oracle SQL Developer

1. Open Oracle SQL Developer
2. Connect to your database
3. Open and execute each script file in order:
   - `Database/Scripts/01_Create_Sequences.sql`
   - `Database/Scripts/02_Create_SYS_ROLE_Procedures.sql`
   - `Database/Scripts/03_Create_SYS_CURRENCY_Procedures.sql`
   - `Database/Scripts/04_Create_SYS_BRANCH_Procedures.sql`
   - `Database/Scripts/05_Create_SYS_USERS_Procedures.sql`
4. Execute each script (F5 or Run Script button)

### Using Command Line

```bash
sqlplus username/password@database @Database/Scripts/01_Create_Sequences.sql
sqlplus username/password@database @Database/Scripts/02_Create_SYS_ROLE_Procedures.sql
sqlplus username/password@database @Database/Scripts/03_Create_SYS_CURRENCY_Procedures.sql
sqlplus username/password@database @Database/Scripts/04_Create_SYS_BRANCH_Procedures.sql
sqlplus username/password@database @Database/Scripts/05_Create_SYS_USERS_Procedures.sql
```

## Verification

After executing the scripts, verify that all objects were created successfully.

### Verify Sequences

```sql
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
```

Expected output: 5 sequences listed with START WITH 1, INCREMENT BY 1

### Verify Stored Procedures

```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_type = 'PROCEDURE'
  AND object_name LIKE 'SP_SYS_%'
ORDER BY object_name;
```

Expected output: All procedures with STATUS = 'VALID'

## Sequence Usage

These sequences are used by stored procedures during INSERT operations to generate unique primary key values:

- **SEQ_SYS_ROLE**: Used by `SP_SYS_ROLE_INSERT`
- **SEQ_SYS_CURRENCY**: Used by `SP_SYS_CURRENCY_INSERT`
- **SEQ_SYS_COMPANY**: Used by `SP_SYS_COMPANY_INSERT`
- **SEQ_SYS_BRANCH**: Used by `SP_SYS_BRANCH_INSERT`
- **SEQ_SYS_USERS**: Used by `SP_SYS_USERS_INSERT`

Example usage in stored procedure:
```sql
SELECT SEQ_SYS_ROLE.NEXTVAL INTO P_NEW_ID FROM DUAL;
```

## Additional Documentation

- **FISCAL_YEAR_AND_COMPANY_EXTENSION.md** - Detailed documentation for fiscal year and company extensions
- **SCRIPT_EXECUTION_SUMMARY.md** - Complete script execution order and verification queries
- **TEST_DATA_README.md** - Information about test data and credentials

## Notes

- All sequences start at 1 and increment by 1
- NOCACHE is used to prevent gaps in sequence values
- NOCYCLE ensures sequences don't wrap around after reaching maximum value
- These sequences must be created before creating the stored procedures that use them
- Scripts 18-22 add fiscal year management and extend company functionality
- Execute scripts in numerical order to ensure proper dependencies
