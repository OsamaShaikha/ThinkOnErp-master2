namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested ticket cannot be found.
/// </summary>
public class TicketNotFoundException : DomainException
{
    public Int64 TicketId { get; }

    public TicketNotFoundException(Int64 ticketId) 
        : base($"Ticket with ID {ticketId} was not found", "TICKET_NOT_FOUND")
    {
        TicketId = ticketId;
        AddContext("TicketId", ticketId);
    }
}
