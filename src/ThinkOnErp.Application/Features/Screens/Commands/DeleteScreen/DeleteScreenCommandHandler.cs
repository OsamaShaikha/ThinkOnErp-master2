using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Screens.Commands.DeleteScreen;

public class DeleteScreenCommandHandler : IRequestHandler<DeleteScreenCommand, long>
{
    private readonly IScreenRepository _screenRepository;

    public DeleteScreenCommandHandler(IScreenRepository screenRepository)
    {
        _screenRepository = screenRepository;
    }

    public async Task<long> Handle(DeleteScreenCommand request, CancellationToken cancellationToken)
    {
        var screen = await _screenRepository.GetScreenByIdAsync(request.ScreenId);
        if (screen == null)
            return 0;

        await _screenRepository.DeleteScreenAsync(request.ScreenId, request.UpdateUser);
        return 1;
    }
}
