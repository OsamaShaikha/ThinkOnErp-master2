using MediatR;
using ThinkOnErp.Application.DTOs.Branch;

namespace ThinkOnErp.Application.Features.Branches.Queries.GetBranchById;

public class GetBranchByIdQuery : IRequest<BranchDto?>
{
    public decimal RowId { get; set; }
}
