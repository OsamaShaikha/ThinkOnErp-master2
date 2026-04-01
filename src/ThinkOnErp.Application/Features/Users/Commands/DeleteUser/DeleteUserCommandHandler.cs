using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.DeleteUser;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, int>
{
    private readonly IUserRepository _userRepository;

    public DeleteUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<int> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        return await _userRepository.DeleteAsync(request.RowId);
    }
}
