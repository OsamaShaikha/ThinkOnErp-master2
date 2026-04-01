using MediatR;
using ThinkOnErp.Application.DTOs.User;

namespace ThinkOnErp.Application.Features.Users.Queries.GetAllUsers;

public class GetAllUsersQuery : IRequest<List<UserDto>>
{
}
