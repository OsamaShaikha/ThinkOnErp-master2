using MediatR;

namespace ThinkOnErp.Application.Features.Users.Commands.DeleteUser;

public class DeleteUserCommand : IRequest<int>
{
    public decimal RowId { get; set; }
}
