using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Provides serialized entity state from the database for audit logging.
/// Maps entity type strings (e.g., "Role", "User") to CLR types and fetches
/// the current state via EF Core before a data change operation.
/// </summary>
public class EntityStateProvider : IEntityStateProvider
{
    private static readonly Dictionary<string, Type> EntityTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Role", typeof(SysRole) },
        { "User", typeof(SysUser) },
        { "Company", typeof(SysCompany) },
        { "Branch", typeof(SysBranch) },
        { "Screen", typeof(SysScreen) },
        { "System", typeof(SysSystem) },
        { "SuperAdmin", typeof(SysSuperAdmin) },
        { "Currency", typeof(SysCurrency) },
        { "FiscalYear", typeof(SysFiscalYear) },
        { "Document", typeof(SysDocument) },
        { "Ticket", typeof(SysRequestTicket) },
        { "TicketCategory", typeof(SysTicketCategory) },
        { "TicketType", typeof(SysTicketType) },
        { "TicketStatus", typeof(SysTicketStatus) },
        { "TicketPriority", typeof(SysTicketPriority) },
        { "TicketComment", typeof(SysTicketComment) },
        { "TicketAttachment", typeof(SysTicketAttachment) },
        { "TicketConfig", typeof(SysTicketConfig) },
        { "Code", typeof(SysCode) },
        { "Setting", typeof(SysSetting) },
        { "UserRole", typeof(SysUserRole) },
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

    public EntityStateProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<string?> GetEntityStateAsync(string entityType, long entityId)
    {
        if (!EntityTypeMap.TryGetValue(entityType, out var clrType))
            return null;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
            var entity = await dbContext.FindAsync(clrType, entityId);
            if (entity == null) return null;

            return JsonSerializer.Serialize(entity, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
