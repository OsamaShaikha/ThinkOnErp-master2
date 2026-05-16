namespace ThinkOnErp.Domain.Exceptions;

/// <summary>
/// Exception thrown when a database constraint violation occurs.
/// </summary>
public class ConstraintViolationException : DomainException
{
    public string ConstraintName { get; }
    public string ConstraintType { get; }

    public ConstraintViolationException(string constraintName, string constraintType, string message, Exception innerException) 
        : base(message, "CONSTRAINT_VIOLATION", innerException)
    {
        ConstraintName = constraintName;
        ConstraintType = constraintType;
        AddContext("ConstraintName", constraintName);
        AddContext("ConstraintType", constraintType);
    }

    public ConstraintViolationException(string constraintName, string constraintType, string message) 
        : base(message, "CONSTRAINT_VIOLATION")
    {
        ConstraintName = constraintName;
        ConstraintType = constraintType;
        AddContext("ConstraintName", constraintName);
        AddContext("ConstraintType", constraintType);
    }
}
