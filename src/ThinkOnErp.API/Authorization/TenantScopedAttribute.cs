namespace ThinkOnErp.API.Authorization;

/// <summary>
/// Marks an endpoint whose data is stored in a company tenant schema.
/// SuperAdmin requests to required tenant endpoints must explicitly select one
/// company using X-Company-Id or X-Company-Code.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class TenantScopedAttribute : Attribute
{
    public TenantScopedAttribute(bool selectionRequired = true)
    {
        SelectionRequired = selectionRequired;
    }

    public bool SelectionRequired { get; }
}
