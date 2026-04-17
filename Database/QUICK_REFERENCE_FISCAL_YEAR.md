# Fiscal Year & Company Extensions - Quick Reference

## New Database Objects

### Tables
- **SYS_FISCAL_YEAR** - Stores fiscal year information for companies

### Sequences
- **SEQ_SYS_FISCAL_YEAR** - Generates fiscal year IDs

### New Company Columns
| Column Name | Type | Description |
|------------|------|-------------|
| LEGAL_NAME | VARCHAR2(300) | Legal name in Arabic |
| LEGAL_NAME_E | VARCHAR2(300) | Legal name in English |
| COMPANY_CODE | VARCHAR2(50) | Unique company code |
| DEFAULT_LANG | VARCHAR2(10) | Default language (ar/en) |
| TAX_NUMBER | VARCHAR2(50) | Tax registration number |
| FISCAL_YEAR_ID | NUMBER | Current fiscal year FK |
| BASE_CURRENCY_ID | NUMBER | Base currency FK |
| SYSTEM_LANGUAGE | VARCHAR2(10) | System language (ar/en) |
| ROUNDING_RULES | VARCHAR2(50) | Rounding method |
| COMPANY_LOGO | BLOB | Company logo image |

## Stored Procedures

### Fiscal Year Procedures
```sql
-- Get all fiscal years
SP_SYS_FISCAL_YEAR_SELECT_ALL(P_RESULT_CURSOR OUT SYS_REFCURSOR)

-- Get fiscal year by ID
SP_SYS_FISCAL_YEAR_SELECT_BY_ID(P_ROW_ID IN NUMBER, P_RESULT_CURSOR OUT SYS_REFCURSOR)

-- Get fiscal years by company
SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY(P_COMPANY_ID IN NUMBER, P_RESULT_CURSOR OUT SYS_REFCURSOR)

-- Create fiscal year
SP_SYS_FISCAL_YEAR_INSERT(
    P_COMPANY_ID IN NUMBER,
    P_FISCAL_YEAR_CODE IN VARCHAR2,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_IS_CLOSED IN CHAR,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)

-- Update fiscal year
SP_SYS_FISCAL_YEAR_UPDATE(
    P_ROW_ID IN NUMBER,
    P_COMPANY_ID IN NUMBER,
    P_FISCAL_YEAR_CODE IN VARCHAR2,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_START_DATE IN DATE,
    P_END_DATE IN DATE,
    P_IS_CLOSED IN CHAR,
    P_UPDATE_USER IN VARCHAR2
)

-- Delete fiscal year (soft delete)
SP_SYS_FISCAL_YEAR_DELETE(P_ROW_ID IN NUMBER)

-- Close fiscal year
SP_SYS_FISCAL_YEAR_CLOSE(P_ROW_ID IN NUMBER, P_UPDATE_USER IN VARCHAR2)
```

### Updated Company Procedures
```sql
-- Get all companies (includes new columns)
SP_SYS_COMPANY_SELECT_ALL(P_RESULT_CURSOR OUT SYS_REFCURSOR)

-- Get company by ID (includes new columns)
SP_SYS_COMPANY_SELECT_BY_ID(P_ROW_ID IN NUMBER, P_RESULT_CURSOR OUT SYS_REFCURSOR)

-- Create company (with new parameters)
SP_SYS_COMPANY_INSERT(
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_DEFAULT_LANG IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2,
    P_FISCAL_YEAR_ID IN NUMBER,
    P_BASE_CURRENCY_ID IN NUMBER,
    P_SYSTEM_LANGUAGE IN VARCHAR2,
    P_ROUNDING_RULES IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_CREATION_USER IN VARCHAR2,
    P_NEW_ID OUT NUMBER
)

-- Update company (with new parameters)
SP_SYS_COMPANY_UPDATE(
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_DEFAULT_LANG IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2,
    P_FISCAL_YEAR_ID IN NUMBER,
    P_BASE_CURRENCY_ID IN NUMBER,
    P_SYSTEM_LANGUAGE IN VARCHAR2,
    P_ROUNDING_RULES IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_UPDATE_USER IN VARCHAR2
)

-- Update company logo
SP_SYS_COMPANY_UPDATE_LOGO(
    P_ROW_ID IN NUMBER,
    P_COMPANY_LOGO IN BLOB,
    P_UPDATE_USER IN VARCHAR2
)

-- Get company logo
SP_SYS_COMPANY_GET_LOGO(P_ROW_ID IN NUMBER, P_RESULT_CURSOR OUT SYS_REFCURSOR)
```

## Common Usage Examples

### Create a New Fiscal Year
```sql
DECLARE
    v_new_id NUMBER;
BEGIN
    SP_SYS_FISCAL_YEAR_INSERT(
        P_COMPANY_ID => 1,
        P_FISCAL_YEAR_CODE => 'FY2027',
        P_ROW_DESC => 'السنة المالية 2027',
        P_ROW_DESC_E => 'Fiscal Year 2027',
        P_START_DATE => TO_DATE('2027-01-01', 'YYYY-MM-DD'),
        P_END_DATE => TO_DATE('2027-12-31', 'YYYY-MM-DD'),
        P_IS_CLOSED => '0',
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
    DBMS_OUTPUT.PUT_LINE('Created Fiscal Year ID: ' || v_new_id);
    COMMIT;
END;
/
```

### Get All Fiscal Years for a Company
```sql
DECLARE
    v_cursor SYS_REFCURSOR;
    v_row_id NUMBER;
    v_code VARCHAR2(20);
    v_desc VARCHAR2(200);
    v_start DATE;
    v_end DATE;
    v_closed CHAR(1);
BEGIN
    SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY(
        P_COMPANY_ID => 1,
        P_RESULT_CURSOR => v_cursor
    );
    
    LOOP
        FETCH v_cursor INTO v_row_id, v_code, v_desc, v_start, v_end, v_closed;
        EXIT WHEN v_cursor%NOTFOUND;
        DBMS_OUTPUT.PUT_LINE('FY: ' || v_code || ' - ' || v_desc);
    END LOOP;
    
    CLOSE v_cursor;
END;
/
```

### Close a Fiscal Year
```sql
BEGIN
    SP_SYS_FISCAL_YEAR_CLOSE(
        P_ROW_ID => 1,
        P_UPDATE_USER => 'admin'
    );
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Fiscal year closed successfully');
END;
/
```

### Create a Company with New Fields
```sql
DECLARE
    v_new_id NUMBER;
BEGIN
    SP_SYS_COMPANY_INSERT(
        P_ROW_DESC => 'شركة الأمثلة التجارية',
        P_ROW_DESC_E => 'Example Trading Company',
        P_LEGAL_NAME => 'شركة الأمثلة التجارية المحدودة',
        P_LEGAL_NAME_E => 'Example Trading Company Ltd.',
        P_COMPANY_CODE => 'COMP003',
        P_DEFAULT_LANG => 'ar',
        P_TAX_NUMBER => '300555666777003',
        P_FISCAL_YEAR_ID => 3,
        P_BASE_CURRENCY_ID => 1,
        P_SYSTEM_LANGUAGE => 'ar',
        P_ROUNDING_RULES => 'HALF_UP',
        P_COUNTRY_ID => 1,
        P_CURR_ID => 1,
        P_CREATION_USER => 'admin',
        P_NEW_ID => v_new_id
    );
    DBMS_OUTPUT.PUT_LINE('Created Company ID: ' || v_new_id);
    COMMIT;
END;
/
```

### Update Company with Fiscal Year
```sql
BEGIN
    SP_SYS_COMPANY_UPDATE(
        P_ROW_ID => 1,
        P_ROW_DESC => 'شركة ثينك أون',
        P_ROW_DESC_E => 'ThinkOn Company',
        P_LEGAL_NAME => 'شركة ثينك أون للبرمجيات المحدودة',
        P_LEGAL_NAME_E => 'ThinkOn Software Solutions Ltd.',
        P_COMPANY_CODE => 'COMP001',
        P_DEFAULT_LANG => 'ar',
        P_TAX_NUMBER => '300123456789003',
        P_FISCAL_YEAR_ID => 3,
        P_BASE_CURRENCY_ID => 1,
        P_SYSTEM_LANGUAGE => 'ar',
        P_ROUNDING_RULES => 'HALF_UP',
        P_COUNTRY_ID => 1,
        P_CURR_ID => 1,
        P_UPDATE_USER => 'admin'
    );
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Company updated successfully');
END;
/
```

## Useful Queries

### View All Fiscal Years with Company Info
```sql
SELECT 
    fy.ROW_ID,
    fy.COMPANY_ID,
    c.ROW_DESC_E AS COMPANY_NAME,
    c.COMPANY_CODE,
    fy.FISCAL_YEAR_CODE,
    fy.ROW_DESC_E AS FISCAL_YEAR_NAME,
    fy.START_DATE,
    fy.END_DATE,
    CASE WHEN fy.IS_CLOSED = '1' THEN 'Closed' ELSE 'Open' END AS STATUS,
    fy.IS_ACTIVE
FROM SYS_FISCAL_YEAR fy
JOIN SYS_COMPANY c ON fy.COMPANY_ID = c.ROW_ID
WHERE fy.IS_ACTIVE = '1'
ORDER BY c.COMPANY_CODE, fy.START_DATE DESC;
```

### View Companies with Extended Information
```sql
SELECT 
    c.ROW_ID,
    c.COMPANY_CODE,
    c.ROW_DESC_E AS COMPANY_NAME,
    c.LEGAL_NAME_E,
    c.TAX_NUMBER,
    c.DEFAULT_LANG,
    c.SYSTEM_LANGUAGE,
    c.ROUNDING_RULES,
    fy.FISCAL_YEAR_CODE AS CURRENT_FISCAL_YEAR,
    curr.ROW_DESC_E AS BASE_CURRENCY,
    c.IS_ACTIVE
FROM SYS_COMPANY c
LEFT JOIN SYS_FISCAL_YEAR fy ON c.FISCAL_YEAR_ID = fy.ROW_ID
LEFT JOIN SYS_CURRENCY curr ON c.BASE_CURRENCY_ID = curr.ROW_ID
WHERE c.IS_ACTIVE = '1'
ORDER BY c.COMPANY_CODE;
```

### Find Open Fiscal Years
```sql
SELECT 
    fy.ROW_ID,
    c.COMPANY_CODE,
    c.ROW_DESC_E AS COMPANY_NAME,
    fy.FISCAL_YEAR_CODE,
    fy.START_DATE,
    fy.END_DATE
FROM SYS_FISCAL_YEAR fy
JOIN SYS_COMPANY c ON fy.COMPANY_ID = c.ROW_ID
WHERE fy.IS_CLOSED = '0'
  AND fy.IS_ACTIVE = '1'
ORDER BY c.COMPANY_CODE, fy.START_DATE;
```

### Find Current Active Fiscal Year for Each Company
```sql
SELECT 
    c.ROW_ID AS COMPANY_ID,
    c.COMPANY_CODE,
    c.ROW_DESC_E AS COMPANY_NAME,
    fy.ROW_ID AS FISCAL_YEAR_ID,
    fy.FISCAL_YEAR_CODE,
    fy.START_DATE,
    fy.END_DATE
FROM SYS_COMPANY c
JOIN SYS_FISCAL_YEAR fy ON c.FISCAL_YEAR_ID = fy.ROW_ID
WHERE c.IS_ACTIVE = '1'
ORDER BY c.COMPANY_CODE;
```

## Rounding Rules Reference

| Rule | Description | Example (2.5) | Example (2.4) |
|------|-------------|---------------|---------------|
| HALF_UP | Round to nearest, ties away from zero | 3 | 2 |
| HALF_DOWN | Round to nearest, ties toward zero | 2 | 2 |
| UP | Round away from zero | 3 | 3 |
| DOWN | Round toward zero | 2 | 2 |
| CEILING | Round toward positive infinity | 3 | 3 |
| FLOOR | Round toward negative infinity | 2 | 2 |

## Validation Rules

1. **Fiscal Year Dates**: END_DATE must be after START_DATE
2. **Company Code**: Must be unique (enforced by UK_COMPANY_CODE constraint)
3. **Fiscal Year Code**: Must be unique per company (enforced by UK_FISCAL_YEAR_CODE constraint)
4. **Language Values**: Must be 'ar' or 'en'
5. **Rounding Rules**: Must be one of: HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR
6. **IS_CLOSED**: Must be '0' (open) or '1' (closed)

## Error Codes

| Error Code | Description |
|-----------|-------------|
| -20301 to -20314 | Fiscal year operation errors |
| -20203 | Company code already exists |
| -20304 | Invalid date range (end date before start date) |
| -20305 | Fiscal year code already exists for company |

## Next Steps for Application Development

1. Create C# entity models for FiscalYear
2. Create DTOs for fiscal year operations
3. Create FiscalYearRepository
4. Update CompanyRepository to handle new fields
5. Create FiscalYearController
6. Update CompanyController
7. Add validation for new fields
8. Update API documentation
9. Create unit tests for new functionality
10. Update integration tests
