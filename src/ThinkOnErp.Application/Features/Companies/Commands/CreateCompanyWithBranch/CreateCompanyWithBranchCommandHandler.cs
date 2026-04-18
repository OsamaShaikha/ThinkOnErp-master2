using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompanyWithBranch;

/// <summary>
/// Handler for creating a company with an automatic default branch.
/// Uses repository pattern to maintain clean architecture separation.
/// </summary>
public class CreateCompanyWithBranchCommandHandler : IRequestHandler<CreateCompanyWithBranchCommand, CreateCompanyWithBranchResult>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<CreateCompanyWithBranchCommandHandler> _logger;

    public CreateCompanyWithBranchCommandHandler(
        ICompanyRepository companyRepository,
        ILogger<CreateCompanyWithBranchCommandHandler> logger)
    {
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateCompanyWithBranchResult> Handle(CreateCompanyWithBranchCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating company with default branch: {CompanyCode}", request.CompanyCode);

        try
        {
            // Convert Base64 logos to byte arrays if provided
            byte[]? companyLogo = null;
            byte[]? branchLogo = null;

            if (!string.IsNullOrEmpty(request.CompanyLogoBase64))
            {
                try
                {
                    companyLogo = Convert.FromBase64String(request.CompanyLogoBase64);
                    _logger.LogInformation("Company logo converted from Base64, size: {Size} bytes", companyLogo.Length);
                }
                catch (FormatException)
                {
                    throw new ArgumentException("Invalid Base64 format for company logo");
                }
            }

            if (!string.IsNullOrEmpty(request.BranchLogoBase64))
            {
                try
                {
                    branchLogo = Convert.FromBase64String(request.BranchLogoBase64);
                    _logger.LogInformation("Branch logo converted from Base64, size: {Size} bytes", branchLogo.Length);
                }
                catch (FormatException)
                {
                    throw new ArgumentException("Invalid Base64 format for branch logo");
                }
            }

            // Use the repository method to create company with branch
            var result = await _companyRepository.CreateWithBranchAsync(
                companyNameAr: request.CompanyNameAr,
                companyNameEn: request.CompanyNameEn,
                legalNameAr: request.LegalNameAr,
                legalNameEn: request.LegalNameEn,
                companyCode: request.CompanyCode,
                defaultLang: request.DefaultLang,
                taxNumber: request.TaxNumber,
                fiscalYearId: request.FiscalYearId,
                baseCurrencyId: request.BaseCurrencyId,
                systemLanguage: request.SystemLanguage,
                roundingRules: request.RoundingRules,
                countryId: request.CountryId,
                currId: request.CurrId,
                branchNameAr: request.BranchNameAr,
                branchNameEn: request.BranchNameEn,
                branchPhone: request.BranchPhone,
                branchMobile: request.BranchMobile,
                branchFax: request.BranchFax,
                branchEmail: request.BranchEmail,
                branchLogo: branchLogo, // Use converted byte array
                creationUser: request.CreationUser);

            // If company logo was provided, update it separately
            if (companyLogo != null)
            {
                await _companyRepository.UpdateLogoAsync(result.CompanyId, companyLogo, request.CreationUser);
                _logger.LogInformation("Company logo updated for company ID: {CompanyId}", result.CompanyId);
            }

            _logger.LogInformation(
                "Company created successfully with ID: {CompanyId}, Default branch created with ID: {BranchId}",
                result.CompanyId, result.BranchId);

            // Generate branch name for response (if not provided)
            var branchName = request.BranchNameEn ?? $"{request.CompanyNameEn} - Head Office";

            return new CreateCompanyWithBranchResult
            {
                CompanyId = result.CompanyId,
                BranchId = result.BranchId,
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