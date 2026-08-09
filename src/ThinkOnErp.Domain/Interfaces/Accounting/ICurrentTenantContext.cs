namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface ICurrentTenantContext
{
    long GetRequiredCompanyId();
}
