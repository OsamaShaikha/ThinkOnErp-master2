namespace ThinkOnErp.Domain.Exceptions;

public sealed class AccountingNotFoundException : DomainException
{
    public AccountingNotFoundException(string message, string errorCode)
        : base(message, errorCode)
    {
    }
}
