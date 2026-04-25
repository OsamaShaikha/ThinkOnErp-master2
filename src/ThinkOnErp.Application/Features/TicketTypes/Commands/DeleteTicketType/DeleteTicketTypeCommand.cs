using MediatR;

namespace ThinkOnErp.Application.Features.TicketTypes.Commands.DeleteTicketType;

/// <summary>
/// Command for deleting (soft delete) a ticket type.
/// </summary>
public class DeleteTicketTypeCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the ticket type to delete
    /// </summary>
    public Int64 TicketTypeId { get; set; }

    /// <summary>
    /// Username of the user deleting this ticket type
    /// </summary>
    public string DeleteUser { get; set; } = string.Empty;
}
