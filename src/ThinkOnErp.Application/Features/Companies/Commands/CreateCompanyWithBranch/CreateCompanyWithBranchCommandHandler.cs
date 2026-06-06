using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompanyWithBranch;

public class CreateCompanyWithBranchCommandHandler : IRequestHandler<CreateCompanyWithBranchCommand, CreateCompanyWithBranchResult>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogoStorageService _logoStorageService;
    private readonly IOracleSchemaService _oracleSchemaService;
    private readonly ILogger<CreateCompanyWithBranchCommandHandler> _logger;

    public CreateCompanyWithBranchCommandHandler(
        ICompanyRepository companyRepository,
        IBranchRepository branchRepository,
        IPermissionRepository permissionRepository,
        ILogoStorageService logoStorageService,
        IOracleSchemaService oracleSchemaService,
        ILogger<CreateCompanyWithBranchCommandHandler> logger)
    {
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
        _logoStorageService = logoStorageService ?? throw new ArgumentNullException(nameof(logoStorageService));
        _oracleSchemaService = oracleSchemaService ?? throw new ArgumentNullException(nameof(oracleSchemaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateCompanyWithBranchResult> Handle(CreateCompanyWithBranchCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating company with default branch: {CompanyCode}", request.CompanyCode);

        try
        {
            // Save logos to disk first (use temp ID 0, will update after creation)
            string? companyLogoPath = null;
            string? branchLogoPath = null;

            if (request.CompanyLogo != null)
            {
                companyLogoPath = await _logoStorageService.SaveLogoAsync(request.CompanyLogo, "companies", 0);
                _logger.LogInformation("Company logo saved to disk, size: {Size} bytes", request.CompanyLogo.Length);
            }
            if (request.BranchLogo != null)
            {
                branchLogoPath = await _logoStorageService.SaveLogoAsync(request.BranchLogo, "branches", 0);
                _logger.LogInformation("Branch logo saved to disk, size: {Size} bytes", request.BranchLogo.Length);
            }

            // Generate Oracle schema name from company code
            var companySchema = $"THINKONERP_{request.CompanyCode?.ToUpperInvariant()?.Replace("-", "_")?.Replace(" ", "_")}";

            // Check if schema already exists in the database
            var existingSchema = await _companyRepository.GetBySchemaAsync(companySchema);
            if (existingSchema != null)
                throw new InvalidOperationException($"Company schema '{companySchema}' already exists for company '{existingSchema.CompanyCode}'.");

            // Use the repository method to create company with branch and fiscal year
            var result = await _companyRepository.CreateWithBranchAsync(
                companyNameAr: request.CompanyNameAr,
                companyNameEn: request.CompanyNameEn,
                legalNameAr: request.LegalNameAr,
                legalNameEn: request.LegalNameEn,
                companyCode: request.CompanyCode,
                taxNumber: request.TaxNumber,
                countryId: request.CountryId,
                currId: request.CurrId,
                companyLogoPath: companyLogoPath,
                branchNameAr: request.BranchNameAr,
                branchNameEn: request.BranchNameEn,
                branchPhone: request.BranchPhone,
                branchMobile: request.BranchMobile,
                branchFax: request.BranchFax,
                branchEmail: request.BranchEmail,
                branchLogoPath: branchLogoPath,
                defaultLang: request.DefaultLang,
                baseCurrencyId: request.BranchBaseCurrencyId,
                roundingRules: request.BranchRoundingRules ?? 1,
                companySchema: companySchema,
                createdBySuperAdminId: request.CreatedBySuperAdminId,
                creationUser: request.CreationUser);

            // Rename logo files with correct IDs
            if (request.CompanyLogo != null)
            {
                var oldPath = companyLogoPath;
                companyLogoPath = await _logoStorageService.SaveLogoAsync(request.CompanyLogo, "companies", result.CompanyId);
                await _companyRepository.UpdateLogoPathAsync(result.CompanyId, companyLogoPath, request.CreationUser);
                if (oldPath != null) await _logoStorageService.DeleteLogoAsync(oldPath);
            }
            if (request.BranchLogo != null)
            {
                var oldPath = branchLogoPath;
                branchLogoPath = await _logoStorageService.SaveLogoAsync(request.BranchLogo, "branches", result.BranchId);
                await _branchRepository.UpdateLogoPathAsync(result.BranchId, branchLogoPath, request.CreationUser);
                if (oldPath != null) await _logoStorageService.DeleteLogoAsync(oldPath);
            }

            _logger.LogInformation(
                "Company created successfully with ID: {CompanyId}, Default branch created with ID: {BranchId}, Default fiscal year created with ID: {FiscalYearId}",
                result.CompanyId, result.BranchId, result.FiscalYearId);

            // Create the Oracle schema for this tenant (failures here will propagate up)
            _logger.LogInformation("Creating Oracle schema {Schema} for company {CompanyCode}", companySchema, request.CompanyCode);
            await _oracleSchemaService.CreateCompanySchemaAsync(companySchema, companySchema);

            // Grant systems and auto-grant all their screens to the default branch
            if (request.Systems?.Count > 0)
            {
                foreach (var systemId in request.Systems)
                {
                    await _permissionRepository.SetBranchSystemAsync(
                        result.BranchId, systemId, isAllowed: true,
                        grantedBy: null, notes: null,
                        creationUser: request.CreationUser
                    );

                    await _permissionRepository.GrantSystemScreensToBranchAsync(
                        result.BranchId, systemId,
                        grantedBy: null,
                        creationUser: request.CreationUser
                    );
                }
            }

            // Generate branch name for response (if not provided)
            var branchName = request.BranchNameEn ?? $"{request.CompanyNameEn} - Head Office";

            return new CreateCompanyWithBranchResult
            {
                CompanyId = result.CompanyId,
                BranchId = result.BranchId,
                FiscalYearId = result.FiscalYearId,
                CompanyCode = request.CompanyCode,
                CompanyName = request.CompanyNameEn,
                BranchName = branchName
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            _logger.LogWarning("Company code already exists: {CompanyCode}", request.CompanyCode);
            throw new InvalidOperationException($"Company code '{request.CompanyCode}' already exists.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error creating company: {ErrorMessage}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company with branch: {CompanyCode}", request.CompanyCode);
            throw new InvalidOperationException($"Failed to create company with branch: {ex.Message}", ex);
        }
    }
}