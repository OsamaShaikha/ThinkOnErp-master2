using System;

namespace ThinkOnErp.Domain.Exceptions;

public sealed class HrNotFoundException : DomainException
{
    public HrNotFoundException(string message, string errorCode = "HR_NOT_FOUND")
        : base(message, errorCode)
    {
    }
}

public sealed class HrValidationException : DomainException
{
    public HrValidationException(string message, string errorCode = "HR_VALIDATION_ERROR")
        : base(message, errorCode)
    {
    }
}

public sealed class HrConflictException : DomainException
{
    public HrConflictException(string message, string errorCode = "HR_CONFLICT")
        : base(message, errorCode)
    {
    }
}
