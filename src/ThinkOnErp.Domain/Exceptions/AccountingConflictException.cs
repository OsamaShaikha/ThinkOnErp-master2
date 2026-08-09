namespace ThinkOnErp.Domain.Exceptions;

public sealed class AccountingConflictException : DomainException
{
    public AccountingConflictException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    public AccountingConflictException(
        string message,
        string errorCode,
        Exception innerException)
        : base(message, errorCode, innerException)
    {
    }
}
