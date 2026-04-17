# Fiscal Year & Company Extension - Application Development Checklist

## Database Layer ✅ COMPLETE

- [x] Create SYS_FISCAL_YEAR table
- [x] Create SEQ_SYS_FISCAL_YEAR sequence
- [x] Create fiscal year stored procedures (7 procedures)
- [x] Extend SYS_COMPANY table with new columns
- [x] Update company stored procedures
- [x] Create logo management procedures
- [x] Add foreign key constraints
- [x] Add unique constraints
- [x] Add check constraints
- [x] Create indexes
- [x] Insert test data
- [x] Create documentation

## Domain Layer (Entity Models) ⏳ TODO

### FiscalYear Entity
- [ ] Create `src/ThinkOnErp.Domain/Entities/FiscalYear.cs`
  - [ ] ROW_ID (int)
  - [ ] COMPANY_ID (int)
  - [ ] FISCAL_YEAR_CODE (string)
  - [ ] ROW_DESC (string)
  - [ ] ROW_DESC_E (string)
  - [ ] START_DATE (DateTime)
  - [ ] END_DATE (DateTime)
  - [ ] IS_CLOSED (bool)
  - [ ] IS_ACTIVE (bool)
  - [ ] Audit fields
  - [ ] Navigation property to Company

### Update Company Entity
- [ ] Update `src/ThinkOnErp.Domain/Entities/Company.cs`
  - [ ] Add LEGAL_NAME (string)
  - [ ] Add LEGAL_NAME_E (string)
  - [ ] Add COMPANY_CODE (string)
  - [ ] Add DEFAULT_LANG (string)
  - [ ] Add TAX_NUMBER (string)
  - [ ] Add FISCAL_YEAR_ID (int?)
  - [ ] Add BASE_CURRENCY_ID (int?)
  - [ ] Add SYSTEM_LANGUAGE (string)
  - [ ] Add ROUNDING_RULES (string)
  - [ ] Add COMPANY_LOGO (byte[])
  - [ ] Add navigation property to FiscalYear
  - [ ] Add navigation property to BaseCurrency

## Application Layer (DTOs) ⏳ TODO

### Fiscal Year DTOs
- [ ] Create `src/ThinkOnErp.Application/DTOs/FiscalYear/FiscalYearDto.cs`
- [ ] Create `src/ThinkOnErp.Application/DTOs/FiscalYear/CreateFiscalYearDto.cs`
- [ ] Create `src/ThinkOnErp.Application/DTOs/FiscalYear/UpdateFiscalYearDto.cs`
- [ ] Create `src/ThinkOnErp.Application/DTOs/FiscalYear/CloseFiscalYearDto.cs`

### Update Company DTOs
- [ ] Update `src/ThinkOnErp.Application/DTOs/Company/CompanyDto.cs`
- [ ] Update `src/ThinkOnErp.Application/DTOs/Company/CreateCompanyDto.cs`
- [ ] Update `src/ThinkOnErp.Application/DTOs/Company/UpdateCompanyDto.cs`
- [ ] Create `src/ThinkOnErp.Application/DTOs/Company/CompanyLogoDto.cs`

## Application Layer (Commands & Queries) ⏳ TODO

### Fiscal Year Commands
- [ ] Create `src/ThinkOnErp.Application/Features/FiscalYears/Commands/CreateFiscalYear/`
  - [ ] CreateFiscalYearCommand.cs
  - [ ] CreateFiscalYearCommandHandler.cs
  - [ ] CreateFiscalYearCommandValidator.cs
- [ ] Create `src/ThinkOnErp.Application/Features/FiscalYears/Commands/UpdateFiscalYear/`
  - [ ] UpdateFiscalYearCommand.cs
  - [ ] UpdateFiscalYearCommandHandler.cs
  - [ ] UpdateFiscalYearCommandValidator.cs
- [ ] Create `src/ThinkOnErp.Application/Features/FiscalYears/Commands/DeleteFiscalYear/`
  - [ ] DeleteFiscalYearCommand.cs
  - [ ] DeleteFiscalYearCommandHandler.cs
- [ ] Create `src/ThinkOnErp.Application/Features/FiscalYears/Commands/CloseFiscalYear/`
  - [ ] CloseFiscalYearCommand.cs
  - [ ] CloseFiscalYearCommandHandler.cs

### Fiscal Year Queries
- [ ] Create `src/ThinkOnErp.Application/Features/FiscalYears/Queries/GetAllFiscalYears/`
  - [ ] GetAllFiscalYearsQuery.cs
  - [ ] GetAllFiscalYearsQueryHandler.cs
- [ ] Create `src/ThinkOnErp.Application/Features/FiscalYears/Queries/GetFiscalYearById/`
  - [ ] GetFiscalYearByIdQuery.cs
  - [ ] GetFiscalYearByIdQueryHandler.cs
- [ ] Create `src/ThinkOnErp.Application/Features/FiscalYears/Queries/GetFiscalYearsByCompany/`
  - [ ] GetFiscalYearsByCompanyQuery.cs
  - [ ] GetFiscalYearsByCompanyQueryHandler.cs

### Update Company Commands
- [ ] Update `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompany/`
  - [ ] Update CreateCompanyCommand.cs
  - [ ] Update CreateCompanyCommandHandler.cs
  - [ ] Update CreateCompanyCommandValidator.cs
- [ ] Update `src/ThinkOnErp.Application/Features/Companies/Commands/UpdateCompany/`
  - [ ] Update UpdateCompanyCommand.cs
  - [ ] Update UpdateCompanyCommandHandler.cs
  - [ ] Update UpdateCompanyCommandValidator.cs
- [ ] Create `src/ThinkOnErp.Application/Features/Companies/Commands/UpdateCompanyLogo/`
  - [ ] UpdateCompanyLogoCommand.cs
  - [ ] UpdateCompanyLogoCommandHandler.cs
  - [ ] UpdateCompanyLogoCommandValidator.cs

## Infrastructure Layer (Repositories) ⏳ TODO

### Fiscal Year Repository
- [ ] Create `src/ThinkOnErp.Infrastructure/Repositories/FiscalYearRepository.cs`
  - [ ] GetAllAsync()
  - [ ] GetByIdAsync(int id)
  - [ ] GetByCompanyIdAsync(int companyId)
  - [ ] CreateAsync(FiscalYear fiscalYear)
  - [ ] UpdateAsync(FiscalYear fiscalYear)
  - [ ] DeleteAsync(int id)
  - [ ] CloseAsync(int id, string userName)

### Update Company Repository
- [ ] Update `src/ThinkOnErp.Infrastructure/Repositories/CompanyRepository.cs`
  - [ ] Update GetAllAsync() - include new columns
  - [ ] Update GetByIdAsync() - include new columns
  - [ ] Update CreateAsync() - handle new columns
  - [ ] Update UpdateAsync() - handle new columns
  - [ ] Add UpdateLogoAsync(int id, byte[] logo, string userName)
  - [ ] Add GetLogoAsync(int id)

### Repository Interfaces
- [ ] Create `src/ThinkOnErp.Application/Interfaces/IFiscalYearRepository.cs`
- [ ] Update `src/ThinkOnErp.Application/Interfaces/ICompanyRepository.cs`

## API Layer (Controllers) ⏳ TODO

### Fiscal Year Controller
- [ ] Create `src/ThinkOnErp.API/Controllers/FiscalYearController.cs`
  - [ ] GET /api/fiscalyears - GetAll
  - [ ] GET /api/fiscalyears/{id} - GetById
  - [ ] GET /api/fiscalyears/company/{companyId} - GetByCompany
  - [ ] POST /api/fiscalyears - Create
  - [ ] PUT /api/fiscalyears/{id} - Update
  - [ ] DELETE /api/fiscalyears/{id} - Delete
  - [ ] POST /api/fiscalyears/{id}/close - Close
  - [ ] Add [Authorize] attributes
  - [ ] Add XML documentation comments
  - [ ] Add proper HTTP status codes

### Update Company Controller
- [ ] Update `src/ThinkOnErp.API/Controllers/CompanyController.cs`
  - [ ] Update Create endpoint - handle new fields
  - [ ] Update Update endpoint - handle new fields
  - [ ] Add GET /api/companies/{id}/logo - GetLogo
  - [ ] Add PUT /api/companies/{id}/logo - UpdateLogo
  - [ ] Add DELETE /api/companies/{id}/logo - DeleteLogo

## Validation ⏳ TODO

### Fiscal Year Validators
- [ ] Create fiscal year validators using FluentValidation
  - [ ] Validate FISCAL_YEAR_CODE format
  - [ ] Validate START_DATE < END_DATE
  - [ ] Validate COMPANY_ID exists
  - [ ] Validate unique fiscal year code per company

### Company Validators
- [ ] Update company validators
  - [ ] Validate COMPANY_CODE uniqueness
  - [ ] Validate COMPANY_CODE format
  - [ ] Validate TAX_NUMBER format
  - [ ] Validate DEFAULT_LANG (ar/en)
  - [ ] Validate SYSTEM_LANGUAGE (ar/en)
  - [ ] Validate ROUNDING_RULES (valid values)
  - [ ] Validate FISCAL_YEAR_ID exists
  - [ ] Validate BASE_CURRENCY_ID exists
  - [ ] Validate LEGAL_NAME required
  - [ ] Validate logo file size and type

## Database Context ⏳ TODO

- [ ] Update `src/ThinkOnErp.Infrastructure/Data/OracleDbContext.cs`
  - [ ] Add DbSet<FiscalYear> FiscalYears
  - [ ] Update Company entity configuration
  - [ ] Configure FiscalYear entity
  - [ ] Configure relationships

## Testing ⏳ TODO

### Unit Tests - Fiscal Year
- [ ] Create `tests/ThinkOnErp.API.Tests/Controllers/FiscalYearControllerTests.cs`
- [ ] Create `tests/ThinkOnErp.Infrastructure.Tests/Repositories/FiscalYearRepositoryTests.cs`
- [ ] Create `tests/ThinkOnErp.Application.Tests/Validators/FiscalYearValidatorTests.cs`
- [ ] Create `tests/ThinkOnErp.Application.Tests/Commands/CreateFiscalYearCommandTests.cs`
- [ ] Create `tests/ThinkOnErp.Application.Tests/Commands/UpdateFiscalYearCommandTests.cs`
- [ ] Create `tests/ThinkOnErp.Application.Tests/Commands/CloseFiscalYearCommandTests.cs`

### Unit Tests - Company Updates
- [ ] Update `tests/ThinkOnErp.API.Tests/Controllers/CompanyControllerTests.cs`
- [ ] Update `tests/ThinkOnErp.Infrastructure.Tests/Repositories/CompanyRepositoryTests.cs`
- [ ] Update `tests/ThinkOnErp.Application.Tests/Validators/CompanyValidatorTests.cs`
- [ ] Create `tests/ThinkOnErp.Application.Tests/Commands/UpdateCompanyLogoCommandTests.cs`

### Integration Tests
- [ ] Create `tests/ThinkOnErp.API.Tests/Integration/FiscalYearIntegrationTests.cs`
- [ ] Update `tests/ThinkOnErp.API.Tests/Integration/CompanyIntegrationTests.cs`
- [ ] Test fiscal year CRUD operations
- [ ] Test fiscal year closing
- [ ] Test company logo upload/download
- [ ] Test company with new fields

### Property-Based Tests
- [ ] Create `tests/ThinkOnErp.API.Tests/PropertyTests/FiscalYearPropertyTests.cs`
- [ ] Test fiscal year date validation
- [ ] Test company code uniqueness
- [ ] Test rounding rules validation

## Documentation ⏳ TODO

### API Documentation
- [ ] Update Swagger/OpenAPI documentation
  - [ ] Add fiscal year endpoints
  - [ ] Add company logo endpoints
  - [ ] Add request/response examples
  - [ ] Add error response examples

### Code Documentation
- [ ] Add XML documentation comments to all public methods
- [ ] Add README for fiscal year feature
- [ ] Update main README with fiscal year info

### User Documentation
- [ ] Create user guide for fiscal year management
- [ ] Create user guide for company logo upload
- [ ] Update API usage examples

## Dependency Injection ⏳ TODO

- [ ] Register FiscalYearRepository in DI container
- [ ] Register IFiscalYearRepository interface
- [ ] Update service registrations if needed

## Error Handling ⏳ TODO

- [ ] Add custom exceptions for fiscal year operations
  - [ ] FiscalYearNotFoundException
  - [ ] FiscalYearAlreadyClosedException
  - [ ] InvalidFiscalYearDateRangeException
  - [ ] DuplicateFiscalYearCodeException
- [ ] Add custom exceptions for company operations
  - [ ] DuplicateCompanyCodeException
  - [ ] InvalidCompanyLogoException
- [ ] Update global exception handler

## Security ⏳ TODO

- [ ] Add authorization policies for fiscal year management
- [ ] Add role-based access control
- [ ] Validate user permissions for closing fiscal years
- [ ] Secure logo upload endpoint (file type, size validation)
- [ ] Add audit logging for fiscal year operations

## Performance ⏳ TODO

- [ ] Add caching for fiscal year lookups
- [ ] Optimize company queries with new columns
- [ ] Add pagination for fiscal year lists
- [ ] Optimize logo retrieval (consider CDN)

## Migration & Deployment ⏳ TODO

### Database Migration
- [ ] Test scripts on development database
- [ ] Test scripts on staging database
- [ ] Create rollback scripts
- [ ] Document migration steps
- [ ] Schedule production deployment

### Application Deployment
- [ ] Update application configuration
- [ ] Deploy new application version
- [ ] Verify API endpoints
- [ ] Test end-to-end functionality

## Post-Deployment ⏳ TODO

- [ ] Monitor application logs
- [ ] Monitor database performance
- [ ] Verify fiscal year operations
- [ ] Verify company operations
- [ ] Collect user feedback
- [ ] Address any issues

## Optional Enhancements 💡 FUTURE

- [ ] Add fiscal year templates
- [ ] Add automatic fiscal year creation
- [ ] Add fiscal year comparison reports
- [ ] Add company logo versioning
- [ ] Add company logo CDN integration
- [ ] Add fiscal year period locking
- [ ] Add fiscal year rollover functionality
- [ ] Add multi-currency support enhancements
- [ ] Add company hierarchy support
- [ ] Add company merge functionality

## Notes

### Priority Levels
- **High Priority**: Entity models, repositories, controllers, basic CRUD
- **Medium Priority**: Validation, testing, documentation
- **Low Priority**: Advanced features, optimizations

### Estimated Timeline
- Database Layer: ✅ Complete (1 day)
- Domain & Application Layer: 2-3 days
- Infrastructure Layer: 1-2 days
- API Layer: 1-2 days
- Testing: 2-3 days
- Documentation: 1 day
- **Total Estimated Time**: 8-12 days

### Dependencies
- Fiscal year repository must be created before fiscal year controller
- Company repository must be updated before company controller updates
- Entity models must be created before repositories
- DTOs must be created before commands/queries

### Testing Strategy
1. Unit test each component individually
2. Integration test API endpoints
3. Property-based test validation rules
4. Manual test through Swagger UI
5. Load test with multiple concurrent users

## Completion Tracking

- Database Layer: 100% ✅
- Domain Layer: 0% ⏳
- Application Layer: 0% ⏳
- Infrastructure Layer: 0% ⏳
- API Layer: 0% ⏳
- Testing: 0% ⏳
- Documentation: 50% ⏳ (Database docs complete)

**Overall Progress: 15%**

---

Last Updated: April 17, 2026
