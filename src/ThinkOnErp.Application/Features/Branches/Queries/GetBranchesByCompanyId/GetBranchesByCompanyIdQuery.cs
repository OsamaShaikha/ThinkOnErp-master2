using MediatR;
using ThinkOnErp.Application.DTOs.Branch;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchesByCompanyId;

public class GetBranchesByCompanyIdQuery : IRequest<List<BranchDto>>
{
    public Int64 CompanyId { get; set; }
}
