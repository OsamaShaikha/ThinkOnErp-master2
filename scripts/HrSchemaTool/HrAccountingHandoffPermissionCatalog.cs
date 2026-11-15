using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;

internal static class HrAccountingHandoffPermissionCatalog
{
    private sealed record ScreenDefinition(string Code, string Name, string NameEn, string Route, string SystemCode, int Order, string[] Features);
    private sealed record FeatureDefinition(string Code, string Name, string NameEn, int Order);

    public static async Task EnsureAsync(OracleDbContext db, List<string> report, CancellationToken cancellationToken = default)
    {
        var definitions = new[]
        {
            new ScreenDefinition("hr-payroll", "الرواتب", "Payroll", "/hr/payroll", "hr", 3,
                ["view", "create", "edit", "approve", "reverse", "export"]),
            new ScreenDefinition("accounting-handoff", "تسليمات الموارد البشرية", "HR Handoffs", "/accounting/hr-handoffs", "accounting", 90,
                ["view", "review", "post", "pay", "fund", "reverse", "configure"])
        };
        var featureDefinitions = new[]
        {
            new FeatureDefinition("view", "عرض", "View", 1),
            new FeatureDefinition("create", "إنشاء", "Create", 2),
            new FeatureDefinition("edit", "تعديل", "Edit", 3),
            new FeatureDefinition("approve", "اعتماد", "Approve", 5),
            new FeatureDefinition("export", "تصدير", "Export", 7),
            new FeatureDefinition("reverse", "سحب أو عكس", "Reverse", 20),
            new FeatureDefinition("review", "مراجعة", "Review", 21),
            new FeatureDefinition("post", "ترحيل", "Post", 22),
            new FeatureDefinition("pay", "صرف", "Pay", 23),
            new FeatureDefinition("fund", "تمويل", "Fund", 24),
            new FeatureDefinition("configure", "تهيئة", "Configure", 25)
        };

        foreach (var definition in featureDefinitions)
        {
            var feature = await db.SysFeatures.SingleOrDefaultAsync(f => f.FeatureCode == definition.Code, cancellationToken);
            if (feature == null)
            {
                db.SysFeatures.Add(new SysFeature
                {
                    FeatureCode = definition.Code, FeatureName = definition.Name, FeatureNameE = definition.NameEn,
                    Description = definition.Name, DescriptionE = definition.NameEn, DisplayOrder = definition.Order,
                    IsActive = true, CreationUser = "HR_SCHEMA_TOOL", CreationDate = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            var system = await db.SysSystems.SingleOrDefaultAsync(s => s.SystemCode == definition.SystemCode && s.IsActive, cancellationToken)
                ?? throw new InvalidOperationException($"Active ERP system '{definition.SystemCode}' is required before registering HR handoff permissions.");
            var screen = await db.SysScreens.SingleOrDefaultAsync(s => s.ScreenCode == definition.Code, cancellationToken);
            if (screen == null)
            {
                screen = new SysScreen
                {
                    SystemId = system.Id, ScreenCode = definition.Code, ScreenName = definition.Name, ScreenNameE = definition.NameEn,
                    Route = definition.Route, DisplayOrder = definition.Order, IsActive = true,
                    CreationUser = "HR_SCHEMA_TOOL", CreationDate = DateTime.UtcNow
                };
                db.SysScreens.Add(screen);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (!await db.Set<SysSystemScreen>().AnyAsync(x => x.ScreenId == screen.Id && x.SystemId == system.Id, cancellationToken))
            {
                db.Set<SysSystemScreen>().Add(new SysSystemScreen
                {
                    ScreenId = screen.Id, SystemId = system.Id, DisplayOrder = definition.Order, IsActive = true,
                    CreationUser = "HR_SCHEMA_TOOL", CreationDate = DateTime.UtcNow
                });
            }

            foreach (var featureCode in definition.Features)
            {
                var feature = await db.SysFeatures.SingleOrDefaultAsync(f => f.FeatureCode == featureCode, cancellationToken)
                    ?? throw new InvalidOperationException($"Permission feature '{featureCode}' could not be registered.");
                if (!await db.Set<SysScreenFeature>().AnyAsync(x => x.ScreenId == screen.Id && x.FeatureId == feature.Id, cancellationToken))
                    db.Set<SysScreenFeature>().Add(new SysScreenFeature { ScreenId = screen.Id, FeatureId = feature.Id });
            }
            await db.SaveChangesAsync(cancellationToken);
            report.Add($"Registered permission metadata for {definition.Code}; existing user, role, and branch grants were preserved.");
        }
    }
}
