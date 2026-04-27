namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to access a ticket without proper authorization.
/// </summary>
public class UnauthorizedTicketAccessException : DomainException
{
    public Int64 TicketId { get; }
    public Int64 UserId { get; }

    public UnauthorizedTicketAccessException(Int64 ticketId, Int64 userId) 
        : base($"User {userId} is not authorized to access ticket {ticketId}", "UNAUTHORIZED_TICKET_ACCESS")
    {
        TicketId = ticketId;
        UserId = userId;
        AddContext("TicketId", ticketId);
        AddContext("UserId", userId);
    }
}
