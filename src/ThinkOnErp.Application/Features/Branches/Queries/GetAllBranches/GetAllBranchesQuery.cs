using MediatR;
using ThinkOnErp.Application.DTOs.Branch;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetAllBranches;

public class GetAllBranchesQuery : IRequest<List<BranchDto>>
{
}
