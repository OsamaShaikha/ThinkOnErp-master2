namespace ThinkOnErp.Domain.Exceptions;

public sealed class AccountingException : DomainException
{
    public AccountingException(string message, string errorCode)
        : base(message, errorCode)
    {
    }
}
