using MediatR;

namespace ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;

public class CreateBranchCommand : IRequest<Int64>
{
    public Int64? CompanyId { get; set; }
    public string BranchNameAr { get; set; } = string.Empty;
    public string BranchNameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }
    public bool IsHeadBranch { get; set; }
    public int? DefaultLang { get; set; }
    public Int64? BaseCurrencyId { get; set; }
    public int? RoundingRules { get; set; }
    public Int64? FiscalYearId { get; set; }
    public string? BranchLogoBase64 { get; set; }
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Systems to grant to the branch after creation.
    /// All screens of each system will be auto-granted with full CRUD.
    /// </summary>
    public List<long>? Systems { get; set; }
}
