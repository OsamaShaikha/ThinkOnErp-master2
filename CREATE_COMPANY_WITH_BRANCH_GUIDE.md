# Create Company with Default Branch - Implementation Guide

## Overview

This feature allows you to create a new company and automatically generate a default head office branch in a single atomic transaction. This ensures that every company has at least one branch from the moment it's created.

## Database Implementation

### New Stored Procedures

#### 1. `SP_SYS_COMPANY_INSERT_WITH_BRANCH`
**Full-featured procedure with all company and branch parameters**

```sql
SP_SYS_COMPANY_INSERT_WITH_BRANCH (
    -- Company Parameters
    P_ROW_DESC IN VARCHAR2,           -- Arabic company name
    P_ROW_DESC_E IN VARCHAR2,         -- English company name (required)
    P_LEGAL_NAME IN VARCHAR2,         -- Arabic legal name
    P_LEGAL_NAME_E IN VARCHAR2,       -- English legal name (required)
    P_COMPANY_CODE IN VARCHAR2,       -- Unique company code (required)
    P_DEFAULT_LANG IN VARCHAR2,       -- Default language (ar/en)
    P_TAX_NUMBER IN VARCHAR2,         -- Tax registration number
    P_FISCAL_YEAR_ID IN NUMBER,       -- Current fiscal year ID
    P_BASE_CURRENCY_ID IN NUMBER,     -- Base currency ID
    P_SYSTEM_LANGUAGE IN VARCHAR2,    -- System language (ar/en)
    P_ROUNDING_RULES IN VARCHAR2,     -- Rounding rules
    P_COUNTRY_ID IN NUMBER,           -- Country ID
    P_CURR_ID IN NUMBER,              -- Currency ID (legacy)
    
    -- Branch Parameters
    P_BRANCH_DESC IN VARCHAR2,        -- Arabic branch name (optional)
    P_BRANCH_DESC_E IN VARCHAR2,      -- English branch name (optional)
    P_BRANCH_PHONE IN VARCHAR2,       -- Branch phone
    P_BRANCH_MOBILE IN VARCHAR2,      -- Branch mobile
    P_BRANCH_FAX IN VARCHAR2,         -- Branch fax
    P_BRANCH_EMAIL IN VARCHAR2,       -- Branch email
    
    -- Common Parameters
    P_CREATION_USER IN VARCHAR2,      -- User creating the records
    
    -- Output Parameters
    P_NEW_COMPANY_ID OUT NUMBER,      -- Returns new company ID
    P_NEW_BRANCH_ID OUT NUMBER        -- Returns new branch ID
)
```

#### 2. `SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH`
**Simplified procedure with minimal parameters**

```sql
SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH (
    P_ROW_DESC IN VARCHAR2,           -- Arabic company name
    P_ROW_DESC_E IN VARCHAR2,         -- English company name (required)
    P_LEGAL_NAME_E IN VARCHAR2,       -- English legal name (required)
    P_COMPANY_CODE IN VARCHAR2,       -- Unique company code (required)
    P_CREATION_USER IN VARCHAR2,      -- User creating the records
    P_NEW_COMPANY_ID OUT NUMBER,      -- Returns new company ID
    P_NEW_BRANCH_ID OUT NUMBER        -- Returns new branch ID
)
```

### Features

- **Atomic Transaction**: Both company and branch are created in a single transaction
- **Auto-Generated Branch Names**: If branch names aren't provided, they're auto-generated:
  - Arabic: `{CompanyName} - الفرع الرئيسي`
  - English: `{CompanyName} - Head Office`
- **Head Branch Flag**: The created branch is automatically marked as the head branch (`IS_HEAD_BRANCH = '1'`)
- **Validation**: Comprehensive validation for all parameters
- **Error Handling**: Proper rollback on any failure

## Application Layer Implementation

### Command Structure

```csharp
public class CreateCompanyWithBranchCommand : IRequest<CreateCompanyWithBranchResult>
{
    // Company Information (Required)
    public string CompanyNameEn { get; set; }      // Required
    public string LegalNameEn { get; set; }        // Required
    public string CompanyCode { get; set; }        // Required
    
    // Company Information (Optional)
    public string? CompanyNameAr { get; set; }
    public string? LegalNameAr { get; set; }
    public string DefaultLang { get; set; } = "ar";
    public string? TaxNumber { get; set; }
    public Int64? FiscalYearId { get; set; }
    public Int64? BaseCurrencyId { get; set; }
    public string SystemLanguage { get; set; } = "ar";
    public string RoundingRules { get; set; } = "HALF_UP";
    public Int64? CountryId { get; set; }
    public Int64? CurrId { get; set; }
    
    // Branch Information (All Optional)
    public string? BranchNameAr { get; set; }
    public string? BranchNameEn { get; set; }
    public string? BranchPhone { get; set; }
    public string? BranchMobile { get; set; }
    public string? BranchFax { get; set; }
    public string? BranchEmail { get; set; }
    
    // Audit Information
    public string CreationUser { get; set; }
}
```

### Result Structure

```csharp
public class CreateCompanyWithBranchResult
{
    public Int64 CompanyId { get; set; }
    public Int64 BranchId { get; set; }
    public string CompanyCode { get; set; }
    public string CompanyName { get; set; }
    public string BranchName { get; set; }
}
```

## API Endpoint

### Endpoint Details
- **URL**: `POST /api/companies/with-branch`
- **Authorization**: AdminOnly
- **Content-Type**: `application/json`

### Request Example

```json
{
  "companyNameEn": "Example Corporation",
  "companyNameAr": "شركة المثال",
  "legalNameEn": "Example Corporation LLC",
  "legalNameAr": "شركة المثال ذ.م.م",
  "companyCode": "EXC001",
  "defaultLang": "en",
  "systemLanguage": "en",
  "taxNumber": "123456789",
  "fiscalYearId": 1,
  "baseCurrencyId": 1,
  "roundingRules": "HALF_UP",
  "countryId": 1,
  "currId": 1,
  "branchNameEn": "Example Corp - Headquarters",
  "branchNameAr": "شركة المثال - المقر الرئيسي",
  "branchPhone": "+1-555-0123",
  "branchMobile": "+1-555-0124",
  "branchEmail": "info@example.com"
}
```

### Minimal Request Example

```json
{
  "companyNameEn": "Simple Company",
  "legalNameEn": "Simple Company LLC",
  "companyCode": "SIM001"
}
```

### Response Example

```json
{
  "success": true,
  "message": "Company and default branch created successfully",
  "data": {
    "companyId": 123,
    "branchId": 456,
    "companyCode": "EXC001",
    "companyName": "Example Corporation",
    "branchName": "Example Corp - Headquarters"
  },
  "statusCode": 201
}
```

## Usage Scenarios

### 1. Full Company Setup
Use when you have complete company and branch information:

```bash
curl -X POST /api/companies/with-branch \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "companyNameEn": "Tech Solutions Inc",
    "legalNameEn": "Tech Solutions Incorporated",
    "companyCode": "TECH001",
    "defaultLang": "en",
    "taxNumber": "987654321",
    "branchNameEn": "Tech Solutions - Main Office",
    "branchPhone": "+1-555-TECH",
    "branchEmail": "contact@techsolutions.com"
  }'
```

### 2. Quick Company Creation
Use when you only have basic company information:

```bash
curl -X POST /api/companies/with-branch \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "companyNameEn": "Quick Start LLC",
    "legalNameEn": "Quick Start Limited Liability Company",
    "companyCode": "QS001"
  }'
```

### 3. Multi-Language Company
Use for companies operating in Arabic and English:

```bash
curl -X POST /api/companies/with-branch \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "companyNameEn": "Gulf Trading Company",
    "companyNameAr": "شركة الخليج للتجارة",
    "legalNameEn": "Gulf Trading Company LLC",
    "legalNameAr": "شركة الخليج للتجارة ذ.م.م",
    "companyCode": "GTC001",
    "defaultLang": "ar",
    "systemLanguage": "ar"
  }'
```

## Validation Rules

### Required Fields
- `companyNameEn`: English company name
- `legalNameEn`: English legal name
- `companyCode`: Unique company code (uppercase letters, numbers, underscores, hyphens only)

### Optional Fields with Validation
- `defaultLang`: Must be "ar" or "en"
- `systemLanguage`: Must be "ar" or "en"
- `roundingRules`: Must be one of: HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR
- `branchEmail`: Must be valid email format if provided
- All ID fields: Must be positive numbers if provided

### Length Limits
- Company names: 200 characters
- Legal names: 200 characters
- Company code: 50 characters
- Tax number: 50 characters
- Branch contact info: 20-100 characters depending on field

## Error Handling

### Common Error Responses

#### 1. Company Code Already Exists
```json
{
  "success": false,
  "message": "Company code 'EXC001' already exists.",
  "statusCode": 400
}
```

#### 2. Validation Error
```json
{
  "success": false,
  "message": "Company English name is required",
  "statusCode": 400
}
```

#### 3. Invalid Language
```json
{
  "success": false,
  "message": "Default language must be 'ar' or 'en'",
  "statusCode": 400
}
```

## Database Setup

### 1. Execute the Script
```sql
@Database/Scripts/23_Create_Company_With_Default_Branch.sql
```

### 2. Verify Installation
```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_INSERT_WITH_BRANCH',
    'SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH'
)
ORDER BY object_name;
```

### 3. Test the Procedures (Optional)
```sql
DECLARE
    V_COMPANY_ID NUMBER;
    V_BRANCH_ID NUMBER;
BEGIN
    SP_SYS_COMPANY_INSERT_WITH_SIMPLE_BRANCH(
        P_ROW_DESC => 'شركة الاختبار',
        P_ROW_DESC_E => 'Test Company',
        P_LEGAL_NAME_E => 'Test Company LLC',
        P_COMPANY_CODE => 'TEST001',
        P_CREATION_USER => 'system',
        P_NEW_COMPANY_ID => V_COMPANY_ID,
        P_NEW_BRANCH_ID => V_BRANCH_ID
    );
    
    DBMS_OUTPUT.PUT_LINE('Company ID: ' || V_COMPANY_ID);
    DBMS_OUTPUT.PUT_LINE('Branch ID: ' || V_BRANCH_ID);
    
    -- Clean up test data
    DELETE FROM SYS_BRANCH WHERE ROW_ID = V_BRANCH_ID;
    DELETE FROM SYS_COMPANY WHERE ROW_ID = V_COMPANY_ID;
    COMMIT;
END;
/
```

## Benefits

### 1. Data Consistency
- Ensures every company has at least one branch
- Atomic transaction prevents partial creation
- Automatic head branch designation

### 2. Simplified Workflow
- Single API call creates both entities
- Reduces client-side complexity
- Automatic branch name generation

### 3. Business Logic Enforcement
- Validates company codes uniqueness
- Enforces required fields
- Maintains referential integrity

### 4. Flexibility
- Full control over all fields when needed
- Simple creation with minimal data
- Optional branch customization

## Integration with Existing System

### Backward Compatibility
- Original `POST /api/companies` endpoint remains unchanged
- Existing company creation workflows continue to work
- New endpoint is additive, not replacing

### Branch Management
- Created branches can be managed via existing branch endpoints
- Additional branches can be added later
- Head branch can be changed if needed

### Fiscal Year Integration
- Companies can be created with or without fiscal year assignment
- Fiscal year can be assigned later via company update
- Supports the fiscal year management system

## Best Practices

### 1. Use Appropriate Endpoint
- Use `/api/companies/with-branch` for new company setups
- Use `/api/companies` only when you need to create a company without a branch initially

### 2. Provide Meaningful Names
- Use descriptive company codes (e.g., "ACME001" not "C001")
- Provide both Arabic and English names for multi-language support
- Use clear branch names that identify the location or purpose

### 3. Set Proper Languages
- Set `defaultLang` based on primary business language
- Set `systemLanguage` based on user interface preference
- Consider regional requirements for language settings

### 4. Handle Errors Gracefully
- Check for company code uniqueness before submission
- Validate all required fields client-side
- Provide clear error messages to users

## Troubleshooting

### Common Issues

#### 1. Company Code Conflicts
**Problem**: Company code already exists
**Solution**: Use a different, unique company code

#### 2. Missing Required Fields
**Problem**: Validation errors on submission
**Solution**: Ensure all required fields are provided and valid

#### 3. Database Connection Issues
**Problem**: Stored procedure not found
**Solution**: Verify database script execution and permissions

#### 4. Authorization Errors
**Problem**: 403 Forbidden response
**Solution**: Ensure user has AdminOnly role/permissions

### Debugging Steps

1. **Check Database Objects**
   ```sql
   SELECT * FROM user_objects WHERE object_name LIKE '%COMPANY%BRANCH%';
   ```

2. **Verify Permissions**
   ```sql
   SELECT * FROM user_tab_privs WHERE table_name LIKE '%COMPANY%';
   ```

3. **Test Stored Procedure Directly**
   ```sql
   -- Use the test script provided in the database script
   ```

4. **Check Application Logs**
   - Look for Oracle exceptions
   - Check parameter mapping issues
   - Verify connection string

## Future Enhancements

### Potential Improvements
- **Bulk Company Creation**: Create multiple companies with branches in one operation
- **Template Support**: Predefined company/branch templates
- **Workflow Integration**: Integration with approval workflows
- **Audit Trail**: Enhanced audit logging for company creation
- **Notification System**: Email notifications on company creation

### API Versioning
- Current implementation is v1
- Future versions may add additional fields
- Backward compatibility will be maintained

---

**Last Updated**: April 17, 2026  
**Version**: 1.0  
**Status**: Production Ready