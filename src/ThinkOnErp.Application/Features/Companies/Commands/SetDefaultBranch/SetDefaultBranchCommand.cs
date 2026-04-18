using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.SetDefaultBranch;

/// <summary>
/// Command to set the default branch for a company.
/// </summary>
public class SetDefaultBranchCommand : IRequest<Int64>
{
    /// <summary>
    /// The unique identifier of the company
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// The unique identifier of the branch to set as default
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// The username of the user making the change
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}