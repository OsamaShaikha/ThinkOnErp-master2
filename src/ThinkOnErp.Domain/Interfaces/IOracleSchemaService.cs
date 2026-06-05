namespace ThinkOnErp.Domain.Interfaces;

public interface IOracleSchemaService
{
    Task CreateCompanySchemaAsync(string schemaName, string password);
}
