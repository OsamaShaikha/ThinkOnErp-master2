namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when an invalid ticket status transition is attempted.
/// </summary>
public class InvalidStatusTransitionException : DomainException
{
    public Int64 CurrentStatusId { get; }
    public Int64 NewStatusId { get; }

    public InvalidStatusTransitionException(Int64 currentStatusId, Int64 newStatusId) 
        : base($"Cannot transition from status {currentStatusId} to status {newStatusId}", "INVALID_STATUS_TRANSITION")
    {
        CurrentStatusId = currentStatusId;
        NewStatusId = newStatusId;
        AddContext("CurrentStatusId", currentStatusId);
        AddContext("NewStatusId", newStatusId);
    }
}
