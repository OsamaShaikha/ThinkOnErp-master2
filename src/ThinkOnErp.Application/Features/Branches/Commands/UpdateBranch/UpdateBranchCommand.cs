using MediatR;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranch;

public class UpdateBranchCommand : IRequest<int>
{
    public decimal RowId { get; set; }
    public decimal? ParRowId { get; set; }
    public string RowDesc { get; set; } = string.Empty;
    public string RowDescE { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public bool IsHeadBranch { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
