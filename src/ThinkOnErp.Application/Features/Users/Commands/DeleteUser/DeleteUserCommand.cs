using MediatR;

namespace ThinkOnErp.Application.Features.Users.Commands.DeleteUser;

public class DeleteUserCommand : IRequest<Int64>
{
    public Int64 UserId { get; set; }
}
