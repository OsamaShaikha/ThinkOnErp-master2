using MediatR;
using ThinkOnErp.Application.DTOs.User;

namespace ThinkOnErp.Application.Features.Users.Queries.GetUsersByBranchId;

public class GetUsersByBranchIdQuery : IRequest<List<UserDto>>
{
    public Int64 BranchId { get; set; }
}
