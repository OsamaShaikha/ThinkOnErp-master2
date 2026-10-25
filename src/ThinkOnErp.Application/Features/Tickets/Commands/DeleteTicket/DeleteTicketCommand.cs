using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.DeleteTicket;

public sealed record DeleteTicketCommand(long TicketId, string UserName) : IRequest<long>;

public sealed class DeleteTicketCommandHandler(ITicketRepository tickets) : IRequestHandler<DeleteTicketCommand, long>
{
    public async Task<long> Handle(DeleteTicketCommand request, CancellationToken cancellationToken) =>
        await tickets.DeleteAsync(request.TicketId, request.UserName);
}
