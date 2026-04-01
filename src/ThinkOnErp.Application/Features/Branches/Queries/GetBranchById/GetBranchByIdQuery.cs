using MediatR;
using ThinkOnErp.Application.DTOs.Branch;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchById;

public class GetBranchByIdQuery : IRequest<BranchDto?>
{
    public Int64 BranchId { get; set; }
}
