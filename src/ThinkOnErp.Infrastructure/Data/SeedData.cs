using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(OracleDbContext context)
    {
        var passwordHasher = new PasswordHashingService();
        var now = DateTime.UtcNow;
        var seedUser = "seed";

        // 1. Currencies
        var sar = await context.SysCurrencies
            .FirstOrDefaultAsync(c => c.CurrencyNameEn == "Saudi Riyal")
            ?? new SysCurrency
            {
                CurrencyNameLocal = "ريال سعودي",
                CurrencyNameEn = "Saudi Riyal",
                ShortNameLocal = "ر.س",
                ShortNameEn = "SAR",
                SingularNameLocal = "ريال",
                SingularNameEn = "Riyal",
                DualNameLocal = "ريالان",
                DualNameEn = "Two Riyals",
                CollectiveNameLocal = "ريالات",
                CollectiveNameEn = "Riyals",
                FractionNameLocal = "هللة",
                FractionNameEn = "Halala",
                CurrRate = 1.0m,
                CurrRateDate = now,
                CreationUser = seedUser,
                CreationDate = now
            };

        var usd = await context.SysCurrencies
            .FirstOrDefaultAsync(c => c.CurrencyNameEn == "US Dollar")
            ?? new SysCurrency
            {
                CurrencyNameLocal = "دولار أمريكي",
                CurrencyNameEn = "US Dollar",
                ShortNameLocal = "دولار",
                ShortNameEn = "USD",
                SingularNameLocal = "دولار",
                SingularNameEn = "Dollar",
                DualNameLocal = "دولاران",
                DualNameEn = "Two Dollars",
                CollectiveNameLocal = "دولارات",
                CollectiveNameEn = "Dollars",
                FractionNameLocal = "سنت",
                FractionNameEn = "Cent",
                CurrRate = 3.75m,
                CurrRateDate = now,
                CreationUser = seedUser,
                CreationDate = now
            };

        if (sar.Id == 0) context.SysCurrencies.Add(sar);
        if (usd.Id == 0) context.SysCurrencies.Add(usd);
        await context.SaveChangesAsync();

        // 2. Company (TECH01)
        if (!await context.SysCompanies.AnyAsync(c => c.CompanyCode == "TECH01"))
        {
            var company = new SysCompany
            {
                CompanyNameLocal = "شركة تقنية",
                CompanyNameEn = "Tech Company",
                CurrId = sar.Id,
                LegalName = "Tech Company For Information Technology",
                LegalNameE = "Tech Company For Information Technology",
                CompanyCode = "TECH01",
                CompanySchema = "THINKONERP_TECH01",
                IsActive = true,
                CreationUser = seedUser,
                CreationDate = now
            };
            context.SysCompanies.Add(company);
            await context.SaveChangesAsync();

            // 3. Branches
            var hqBranch = new SysBranch
            {
                CompanyId = company.Id,
                BranchNameLocal = "المركز الرئيسي",
                BranchNameEn = "Headquarters",
                Phone = "+966112345678",
                Mobile = "+966501234567",
                Email = "hq@techcompany.com",
                TaxNumber = "300123456700003",
                IsHeadBranch = true,
                BaseCurrencyId = sar.Id,
                IsActive = true,
                CreationUser = seedUser,
                CreationDate = now
            };
            context.SysBranches.Add(hqBranch);
            await context.SaveChangesAsync();

            var branch2 = new SysBranch
            {
                CompanyId = company.Id,
                BranchNameLocal = "فرع الرياض",
                BranchNameEn = "Riyadh Branch",
                Phone = "+966112345679",
                Mobile = "+966501234568",
                Email = "riyadh@techcompany.com",
                TaxNumber = "300123456700004",
                IsHeadBranch = false,
                BaseCurrencyId = sar.Id,
                IsActive = true,
                CreationUser = seedUser,
                CreationDate = now
            };
            context.SysBranches.Add(branch2);
            await context.SaveChangesAsync();
        }

        // 4. Developer company (DEV02)
        if (!await context.SysCompanies.AnyAsync(c => c.CompanyCode == "DEV02"))
        {
            var devCompany = new SysCompany
            {
                CompanyNameLocal = "شركة المطورين",
                CompanyNameEn = "Developer Company",
                CurrId = sar.Id,
                LegalName = "Developer Company For Testing",
                LegalNameE = "Developer Company For Testing",
                CompanyCode = "DEV02",
                CompanySchema = "THINKONERP_DEV02",
                IsActive = true,
                CreationUser = "system",
                CreationDate = now
            };
            context.SysCompanies.Add(devCompany);
            await context.SaveChangesAsync();

            var devHqBranch = new SysBranch
            {
                CompanyId = devCompany.Id,
                BranchNameLocal = "المركز الرئيسي",
                BranchNameEn = "Headquarters",
                Phone = "+966112345678",
                Mobile = "+966501234567",
                Email = "hq@devcompany.com",
                TaxNumber = "300123456700005",
                IsHeadBranch = true,
                BaseCurrencyId = sar.Id,
                IsActive = true,
                CreationUser = "system",
                CreationDate = now
            };
            context.SysBranches.Add(devHqBranch);
            await context.SaveChangesAsync();
        }

        // 3. SuperAdmin
        if (!await context.SysSuperAdmins.AnyAsync())
        {
            var admin = new SysSuperAdmin
            {
                NameLocal = "مدير النظام",
                NameEn = "System Administrator",
                UserName = "admin",
                Password = passwordHasher.HashPassword("Admin@123"),
                Email = "admin@thinkonerp.com",
                Phone = "+966500000001",
                TwoFaEnabled = false,
                IsActive = true,
                CreationUser = seedUser,
                CreationDate = now
            };
            context.SysSuperAdmins.Add(admin);
            await context.SaveChangesAsync();
        }

        // 4. Systems
        if (!await context.SysSystems.AnyAsync())
        {
            var systems = new List<SysSystem>
            {
                new() { SystemCode = "support", SystemName = "خدمة العملاء", SystemNameE = "Customer Support", Description = "نظام تذاكر الدعم الفني", DescriptionE = "Technical support ticketing system", Icon = "headset", DisplayOrder = 1, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "hr", SystemName = "الموارد البشرية", SystemNameE = "Human Resources", Description = "نظام إدارة الموارد البشرية", DescriptionE = "HR management system", Icon = "people", DisplayOrder = 2, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "administration", SystemName = "الإدارة العامة", SystemNameE = "Administration", Description = "نظام الإدارة العامة للشركة", DescriptionE = "General administration system", Icon = "building", DisplayOrder = 3, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "security", SystemName = "الأمان", SystemNameE = "Security", Description = "نظام إدارة الأمان والصلاحيات", DescriptionE = "Security and permissions management", Icon = "shield", DisplayOrder = 4, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "accounting", SystemName = "المحاسبة", SystemNameE = "Accounting", Description = "نظام المحاسبة والمالية", DescriptionE = "Accounting and finance system", Icon = "calculator", DisplayOrder = 5, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "inventory", SystemName = "المخزون", SystemNameE = "Inventory", Description = "نظام إدارة المخزون", DescriptionE = "Inventory management system", Icon = "box", DisplayOrder = 6, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "pos", SystemName = "نقاط البيع", SystemNameE = "POS", Description = "نظام نقاط البيع", DescriptionE = "Point of sale system", Icon = "cash-register", DisplayOrder = 7, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "crm", SystemName = "إدارة العملاء", SystemNameE = "CRM", Description = "نظام إدارة علاقات العملاء", DescriptionE = "Customer relationship management", Icon = "users", DisplayOrder = 8, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "procurement", SystemName = "المشتريات", SystemNameE = "Procurement", Description = "نظام إدارة المشتريات", DescriptionE = "Procurement management system", Icon = "shopping-cart", DisplayOrder = 9, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemCode = "system", SystemName = "النظام", SystemNameE = "System", Description = "نظام إدارة النظام الأساسي", DescriptionE = "Core system management", Icon = "cog", DisplayOrder = 10, IsActive = true, CreationUser = seedUser, CreationDate = now },
            };
            context.SysSystems.AddRange(systems);
            await context.SaveChangesAsync();
        }

        // 5. Screens
        if (!await context.SysScreens.AnyAsync())
        {
            var sysSupport = await context.SysSystems.FirstAsync(s => s.SystemCode == "support");
            var sysHr = await context.SysSystems.FirstAsync(s => s.SystemCode == "hr");
            var screens = new List<SysScreen>
            {
                new() { SystemId = sysSupport.Id, ScreenCode = "support-dashboard", ScreenName = "لوحة التحكم", ScreenNameE = "Dashboard", Route = "/support/dashboard", Icon = "grid", DisplayOrder = 1, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemId = sysSupport.Id, ScreenCode = "support-tickets", ScreenName = "التذاكر", ScreenNameE = "Tickets", Route = "/support/tickets", Icon = "ticket", DisplayOrder = 2, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemId = sysSupport.Id, ScreenCode = "support-reports", ScreenName = "التقارير", ScreenNameE = "Reports", Route = "/support/reports", Icon = "chart", DisplayOrder = 3, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemId = sysHr.Id, ScreenCode = "hr-employees", ScreenName = "الموظفين", ScreenNameE = "Employees", Route = "/hr/employees", Icon = "person", DisplayOrder = 1, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { SystemId = sysHr.Id, ScreenCode = "hr-attendance", ScreenName = "الحضور والانصراف", ScreenNameE = "Attendance", Route = "/hr/attendance", Icon = "clock", DisplayOrder = 2, IsActive = true, CreationUser = seedUser, CreationDate = now },
            };
            context.SysScreens.AddRange(screens);
            await context.SaveChangesAsync();
        }

        // 6. Generic Features (always seed)
        if (!await context.SysFeatures.AnyAsync())
        {
            var features = new List<SysFeature>
            {
                new() { FeatureCode = "view", FeatureName = "عرض", FeatureNameE = "View", Description = "Ability to view records", DescriptionE = "Ability to view records", DisplayOrder = 1, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { FeatureCode = "create", FeatureName = "إنشاء", FeatureNameE = "Create", Description = "Ability to create new records", DescriptionE = "Ability to create new records", DisplayOrder = 2, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { FeatureCode = "edit", FeatureName = "تعديل", FeatureNameE = "Edit", Description = "Ability to edit existing records", DescriptionE = "Ability to edit existing records", DisplayOrder = 3, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { FeatureCode = "delete", FeatureName = "حذف", FeatureNameE = "Delete", Description = "Ability to delete records", DescriptionE = "Ability to delete records", DisplayOrder = 4, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { FeatureCode = "approve", FeatureName = "اعتماد", FeatureNameE = "Approve", Description = "Ability to approve records", DescriptionE = "Ability to approve records", DisplayOrder = 5, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { FeatureCode = "reject", FeatureName = "رفض", FeatureNameE = "Reject", Description = "Ability to reject records", DescriptionE = "Ability to reject records", DisplayOrder = 6, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { FeatureCode = "export", FeatureName = "تصدير", FeatureNameE = "Export", Description = "Ability to export records", DescriptionE = "Ability to export records", DisplayOrder = 7, IsActive = true, CreationUser = seedUser, CreationDate = now },
                new() { FeatureCode = "print", FeatureName = "طباعة", FeatureNameE = "Print", Description = "Ability to print records", DescriptionE = "Ability to print records", DisplayOrder = 8, IsActive = true, CreationUser = seedUser, CreationDate = now },
            };
            context.SysFeatures.AddRange(features);
            await context.SaveChangesAsync();
        }
    }
}


