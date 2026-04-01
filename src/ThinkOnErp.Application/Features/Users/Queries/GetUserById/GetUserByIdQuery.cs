using MediatR;
using ThinkOnErp.Application.DTOs.User;

namespace ThinkOnErp.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQuery : IRequest<UserDto?>
{
    public Int64 RowId { get; set; }
}
