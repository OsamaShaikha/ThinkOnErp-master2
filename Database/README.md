# ThinkOnErp API - Database Scripts

This directory contains Oracle database scripts for the ThinkOnErp API system.

## Directory Structure

```
Database/
├── Scripts/
│   ├── 01_Create_Sequences.sql           # Creates sequences for primary key generation
│   ├── 02_Create_SYS_ROLE_Procedures.sql # CRUD stored procedures for SYS_ROLE table
│   ├── 03_Create_SYS_CURRENCY_Procedures.sql # CRUD stored procedures for SYS_CURRENCY table
│   ├── 04_Create_SYS_BRANCH_Procedures.sql # CRUD stored procedures for SYS_BRANCH table
│   ├── 05_Create_SYS_USERS_Procedures.sql # CRUD stored procedures for SYS_USERS table
│   └── (future scripts)
└── README.md
```

## Execution Order

Execute the scripts in numerical order:

1. **01_Create_Sequences.sql** - Creates sequences for all 5 core entities
   - SEQ_SYS_ROLE
   - SEQ_SYS_CURRENCY
   - SEQ_SYS_COMPANY
   - SEQ_SYS_BRANCH
   - SEQ_SYS_USERS

2. **02_Create_SYS_ROLE_Procedures.sql** - Creates CRUD stored procedures for SYS_ROLE table
   - SP_SYS_ROLE_SELECT_ALL
   - SP_SYS_ROLE_SELECT_BY_ID
   - SP_SYS_ROLE_INSERT
   - SP_SYS_ROLE_UPDATE
   - SP_SYS_ROLE_DELETE

3. **03_Create_SYS_CURRENCY_Procedures.sql** - Creates CRUD stored procedures for SYS_CURRENCY table
   - SP_SYS_CURRENCY_SELECT_ALL
   - SP_SYS_CURRENCY_SELECT_BY_ID
   - SP_SYS_CURRENCY_INSERT
   - SP_SYS_CURRENCY_UPDATE
   - SP_SYS_CURRENCY_DELETE

4. **04_Create_SYS_BRANCH_Procedures.sql** - Creates CRUD stored procedures for SYS_BRANCH table
   - SP_SYS_BRANCH_SELECT_ALL
   - SP_SYS_BRANCH_SELECT_BY_ID
   - SP_SYS_BRANCH_INSERT
   - SP_SYS_BRANCH_UPDATE
   - SP_SYS_BRANCH_DELETE

5. **05_Create_SYS_USERS_Procedures.sql** - Creates CRUD stored procedures for SYS_USERS table
   - SP_SYS_USERS_SELECT_ALL
   - SP_SYS_USERS_SELECT_BY_ID
   - SP_SYS_USERS_INSERT
   - SP_SYS_USERS_UPDATE
   - SP_SYS_USERS_DELETE
   - SP_SYS_USERS_LOGIN (special authentication procedure)

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

## Notes

- All sequences start at 1 and increment by 1
- NOCACHE is used to prevent gaps in sequence values
- NOCYCLE ensures sequences don't wrap around after reaching maximum value
- These sequences must be created before creating the stored procedures that use them
