# Developer Quick Start - Fiscal Year & Company Extensions

## 🚀 Quick Setup

### 1. Database Setup (5 minutes)

```bash
# Execute scripts in order
sqlplus username/password@database <<EOF
@Database/Scripts/18_Create_SYS_FISCAL_YEAR_Table.sql
@Database/Scripts/19_Extend_SYS_COMPANY_Table.sql
@Database/Scripts/20_Update_SYS_COMPANY_Procedures.sql
@Database/Scripts/21_Insert_Fiscal_Year_Test_Data.sql
@Database/Scripts/22_Update_Company_Test_Data.sql
EXIT;
EOF
```

### 2. Verify Build

```bash
dotnet build ThinkOnErp.sln
# Should complete with 0 errors ✅
```

### 3. Run Application

```bash
dotnet run --project src/ThinkOnErp.API
```

## 📦 What's Available Now

### Entities
- `SysFiscalYear` - Complete fiscal year entity
- `SysCompany` - Extended with 10 new properties

### Repositories (Ready to Use)
```csharp
// Inject in your services
public class YourService
{
    private readonly IFiscalYearRepository _fiscalYearRepo;
    private readonly ICompanyRepository _companyRepo;
    
    public YourService(
        IFiscalYearRepository fiscalYearRepo,
        ICompanyRepository companyRepo)
    {
        _fiscalYearRepo = fiscalYearRepo;
        _companyRepo = companyRepo;
    }
}
```

### Available Methods

#### FiscalYear Operations
```csharp
// Get all fiscal years
var fiscalYears = await _fiscalYearRepo.GetAllAsync();

// Get by ID
var fiscalYear = await _fiscalYearRepo.GetByIdAsync(1);

// Get by company
var companyFiscalYears = await _fiscalYearRepo.GetByCompanyIdAsync(1);

// Create
var newId = await _fiscalYearRepo.CreateAsync(new SysFiscalYear
{
    CompanyId = 1,
    FiscalYearCode = "FY2027",
    RowDesc = "السنة المالية 2027",
    RowDescE = "Fiscal Year 2027",
    StartDate = new DateTime(2027, 1, 1),
    EndDate = new DateTime(2027, 12, 31),
    IsClosed = false,
    CreationUser = "admin"
});

// Update
await _fiscalYearRepo.UpdateAsync(fiscalYear);

// Close
await _fiscalYearRepo.CloseAsync(1, "admin");

// Delete (soft)
await _fiscalYearRepo.DeleteAsync(1);
```

#### Company Operations (New Features)
```csharp
// Get all companies (includes new fields)
var companies = await _companyRepo.GetAllAsync();

// Get by ID (includes new fields)
var company = await _companyRepo.GetByIdAsync(1);

// Create with new fields
var newId = await _companyRepo.CreateAsync(new SysCompany
{
    RowDesc = "شركة الأمثلة",
    RowDescE = "Example Company",
    LegalName = "شركة الأمثلة المحدودة",
    LegalNameE = "Example Company Ltd.",
    CompanyCode = "COMP003",
    DefaultLang = "ar",
    TaxNumber = "300123456789003",
    FiscalYearId = 1,
    BaseCurrencyId = 1,
    SystemLanguage = "ar",
    RoundingRules = "HALF_UP",
    CreationUser = "admin"
});

// Update with new fields
await _companyRepo.UpdateAsync(company);

// Upload logo
byte[] logoBytes = File.ReadAllBytes("logo.png");
await _companyRepo.UpdateLogoAsync(1, logoBytes, "admin");

// Download logo
byte[]? logo = await _companyRepo.GetLogoAsync(1);
if (logo != null)
{
    File.WriteAllBytes("downloaded_logo.png", logo);
}
```

## 📋 DTOs Available

### Fiscal Year DTOs
```csharp
// For API responses
FiscalYearDto

// For creating
CreateFiscalYearDto

// For updating
UpdateFiscalYearDto

// For closing
CloseFiscalYearDto
```

### Company DTOs (Updated)
```csharp
// For API responses (includes new fields)
CompanyDto

// For creating (includes new fields)
CreateCompanyDto

// For updating (includes new fields)
UpdateCompanyDto

// For logo operations
CompanyLogoDto
```

## 🔧 Common Tasks

### Task 1: Create a Fiscal Year
```csharp
var createDto = new CreateFiscalYearDto
{
    CompanyId = 1,
    FiscalYearCode = "FY2027",
    FiscalYearNameAr = "السنة المالية 2027",
    FiscalYearNameEn = "Fiscal Year 2027",
    StartDate = new DateTime(2027, 1, 1),
    EndDate = new DateTime(2027, 12, 31),
    IsClosed = false
};

var entity = new SysFiscalYear
{
    CompanyId = createDto.CompanyId,
    FiscalYearCode = createDto.FiscalYearCode,
    RowDesc = createDto.FiscalYearNameAr,
    RowDescE = createDto.FiscalYearNameEn,
    StartDate = createDto.StartDate,
    EndDate = createDto.EndDate,
    IsClosed = createDto.IsClosed,
    CreationUser = "current-user"
};

var newId = await _fiscalYearRepo.CreateAsync(entity);
```

### Task 2: Close a Fiscal Year
```csharp
// Simple one-liner
await _fiscalYearRepo.CloseAsync(fiscalYearId, "current-user");
```

### Task 3: Update Company with New Fields
```csharp
var company = await _companyRepo.GetByIdAsync(1);
if (company != null)
{
    company.CompanyCode = "COMP001";
    company.TaxNumber = "300123456789003";
    company.FiscalYearId = 3;
    company.BaseCurrencyId = 1;
    company.RoundingRules = "HALF_UP";
    company.UpdateUser = "current-user";
    
    await _companyRepo.UpdateAsync(company);
}
```

### Task 4: Upload Company Logo
```csharp
// From file
byte[] logoBytes = await File.ReadAllBytesAsync("path/to/logo.png");
await _companyRepo.UpdateLogoAsync(companyId, logoBytes, "current-user");

// From stream
using var stream = new MemoryStream();
await formFile.CopyToAsync(stream);
byte[] logoBytes = stream.ToArray();
await _companyRepo.UpdateLogoAsync(companyId, logoBytes, "current-user");
```

## 🎯 Next Steps for Full API

To expose these features via REST API, you need to:

1. **Create Command Handlers** (MediatR)
   - CreateFiscalYearCommandHandler
   - UpdateFiscalYearCommandHandler
   - CloseFiscalYearCommandHandler
   - etc.

2. **Create Query Handlers** (MediatR)
   - GetAllFiscalYearsQueryHandler
   - GetFiscalYearByIdQueryHandler
   - GetFiscalYearsByCompanyQueryHandler

3. **Create Validators** (FluentValidation)
   - CreateFiscalYearValidator
   - UpdateFiscalYearValidator
   - etc.

4. **Create Controller**
   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   public class FiscalYearController : ControllerBase
   {
       private readonly IMediator _mediator;
       
       [HttpGet]
       public async Task<ActionResult<List<FiscalYearDto>>> GetAll()
       {
           var query = new GetAllFiscalYearsQuery();
           var result = await _mediator.Send(query);
           return Ok(result);
       }
       
       // ... more endpoints
   }
   ```

## 📚 Documentation

- **Database:** `Database/FISCAL_YEAR_AND_COMPANY_EXTENSION.md`
- **Quick Reference:** `Database/QUICK_REFERENCE_FISCAL_YEAR.md`
- **Implementation:** `IMPLEMENTATION_COMPLETE_SUMMARY.md`
- **Checklist:** `FISCAL_YEAR_APPLICATION_CHECKLIST.md`

## 🐛 Troubleshooting

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

### Database Connection Issues
```bash
# Test connection
sqlplus username/password@database
```

### Missing Stored Procedures
```sql
-- Verify procedures exist
SELECT object_name, status FROM user_objects 
WHERE object_name LIKE 'SP_SYS_FISCAL_YEAR%'
OR object_name LIKE 'SP_SYS_COMPANY%';
```

## ✅ Validation Rules

### Fiscal Year
- FiscalYearCode: Required, max 20 chars
- StartDate: Required, must be before EndDate
- EndDate: Required, must be after StartDate
- CompanyId: Required, must exist

### Company
- CompanyCode: Optional, max 50 chars, unique
- TaxNumber: Optional, max 50 chars
- DefaultLang: Must be 'ar' or 'en'
- SystemLanguage: Must be 'ar' or 'en'
- RoundingRules: Must be valid value (HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR)

## 🔐 Rounding Rules

| Rule | Description | Example (2.5) |
|------|-------------|---------------|
| HALF_UP | Round to nearest, ties away from zero | 3 |
| HALF_DOWN | Round to nearest, ties toward zero | 2 |
| UP | Round away from zero | 3 |
| DOWN | Round toward zero | 2 |
| CEILING | Round toward positive infinity | 3 |
| FLOOR | Round toward negative infinity | 2 |

## 💡 Tips

1. **Always use async/await** - All repository methods are async
2. **Handle nulls** - Use null-conditional operators
3. **Validate before save** - Check business rules
4. **Use transactions** - For multi-step operations
5. **Log operations** - Track fiscal year changes
6. **Test with real data** - Use the test data scripts

## 🚦 Status

- ✅ Entities: Ready
- ✅ Repositories: Ready
- ✅ DTOs: Ready
- ✅ DI: Configured
- ⏳ Commands/Queries: Not yet
- ⏳ Controllers: Not yet
- ⏳ Validators: Not yet

**You can start using repositories directly in your services now!**

---

**Questions?** Check the documentation files or review the implementation summary.
