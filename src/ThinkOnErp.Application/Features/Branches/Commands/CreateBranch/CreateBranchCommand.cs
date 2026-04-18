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
    public bool IsHeadBranch { get; set; }
    public string? BranchLogoBase64 { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
