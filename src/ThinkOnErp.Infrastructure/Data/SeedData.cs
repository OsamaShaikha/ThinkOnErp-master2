using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(OracleDbContext context)
    {
        if (await context.SysCompanies.AnyAsync()) return;

        var passwordHasher = new PasswordHashingService();
        var now = DateTime.UtcNow;
        var seedUser = "seed";

        // 1. Currencies
        var sar = new SysCurrency
        {
            CurrencyNameAr = "ريال سعودي",
            CurrencyNameEn = "Saudi Riyal",
            ShortNameAr = "ر.س",
            ShortNameEn = "SAR",
            SingularNameAr = "ريال",
            SingularNameEn = "Riyal",
            DualNameAr = "ريالان",
            DualNameEn = "Two Riyals",
            CollectiveNameAr = "ريالات",
            CollectiveNameEn = "Riyals",
            FractionNameAr = "هللة",
            FractionNameEn = "Halala",
            CurrRate = 1.0m,
            CurrRateDate = now,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysCurrencies.Add(sar);
        await context.SaveChangesAsync();

        var usd = new SysCurrency
        {
            CurrencyNameAr = "دولار أمريكي",
            CurrencyNameEn = "US Dollar",
            ShortNameAr = "دولار",
            ShortNameEn = "USD",
            SingularNameAr = "دولار",
            SingularNameEn = "Dollar",
            DualNameAr = "دولاران",
            DualNameEn = "Two Dollars",
            CollectiveNameAr = "دولارات",
            CollectiveNameEn = "Dollars",
            FractionNameAr = "سنت",
            FractionNameEn = "Cent",
            CurrRate = 3.75m,
            CurrRateDate = now,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysCurrencies.Add(usd);
        await context.SaveChangesAsync();

        // 2. Company
        var company = new SysCompany
        {
            CompanyNameAr = "شركة تقنية",
            CompanyNameEn = "Tech Company",
            CurrId = sar.Id,
            LegalName = "Tech Company For Information Technology",
            LegalNameE = "Tech Company For Information Technology",
            CompanyCode = "TECH01",
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
            BranchNameAr = "المركز الرئيسي",
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
            BranchNameAr = "فرع الرياض",
            BranchNameEn = "Riyadh Branch",
            Phone = "+966112345679",
            Mobile = "+966501234568",
            Email = "riyadh@techcompany.com",
            IsHeadBranch = false,
            BaseCurrencyId = sar.Id,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysBranches.Add(branch2);
        await context.SaveChangesAsync();

        company.DefaultBranchId = hqBranch.Id;
        await context.SaveChangesAsync();

        // 4. SuperAdmin
        var admin = new SysSuperAdmin
        {
            NameAr = "مدير النظام",
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

        // 5. Systems
        var sysSupport = new SysSystem
        {
            SystemCode = "support",
            SystemName = "خدمة العملاء",
            SystemNameE = "Customer Support",
            Description = "نظام تذاكر الدعم الفني",
            DescriptionE = "Technical support ticketing system",
            Icon = "headset",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysSupport);

        var sysHr = new SysSystem
        {
            SystemCode = "hr",
            SystemName = "الموارد البشرية",
            SystemNameE = "Human Resources",
            Description = "نظام إدارة الموارد البشرية",
            DescriptionE = "HR management system",
            Icon = "people",
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysHr);

        var sysAdmin = new SysSystem
        {
            SystemCode = "administration",
            SystemName = "الإدارة العامة",
            SystemNameE = "Administration",
            Description = "نظام الإدارة العامة للشركة",
            DescriptionE = "General administration system",
            Icon = "building",
            DisplayOrder = 3,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysAdmin);

        var sysSecurity = new SysSystem
        {
            SystemCode = "security",
            SystemName = "الأمان",
            SystemNameE = "Security",
            Description = "نظام إدارة الأمان والصلاحيات",
            DescriptionE = "Security and permissions management",
            Icon = "shield",
            DisplayOrder = 4,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysSecurity);

        var sysAccounting = new SysSystem
        {
            SystemCode = "accounting",
            SystemName = "المحاسبة",
            SystemNameE = "Accounting",
            Description = "نظام المحاسبة والمالية",
            DescriptionE = "Accounting and finance system",
            Icon = "calculator",
            DisplayOrder = 5,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysAccounting);

        var sysInventory = new SysSystem
        {
            SystemCode = "inventory",
            SystemName = "المخزون",
            SystemNameE = "Inventory",
            Description = "نظام إدارة المخزون",
            DescriptionE = "Inventory management system",
            Icon = "box",
            DisplayOrder = 6,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysInventory);

        var sysPos = new SysSystem
        {
            SystemCode = "pos",
            SystemName = "نقاط البيع",
            SystemNameE = "POS",
            Description = "نظام نقاط البيع",
            DescriptionE = "Point of sale system",
            Icon = "cash-register",
            DisplayOrder = 7,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysPos);

        var sysCrm = new SysSystem
        {
            SystemCode = "crm",
            SystemName = "إدارة العملاء",
            SystemNameE = "CRM",
            Description = "نظام إدارة علاقات العملاء",
            DescriptionE = "Customer relationship management",
            Icon = "users",
            DisplayOrder = 8,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysCrm);

        var sysProcurement = new SysSystem
        {
            SystemCode = "procurement",
            SystemName = "المشتريات",
            SystemNameE = "Procurement",
            Description = "نظام إدارة المشتريات",
            DescriptionE = "Procurement management system",
            Icon = "shopping-cart",
            DisplayOrder = 9,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysProcurement);

        var sysSystem = new SysSystem
        {
            SystemCode = "system",
            SystemName = "النظام",
            SystemNameE = "System",
            Description = "نظام إدارة النظام الأساسي",
            DescriptionE = "Core system management",
            Icon = "cog",
            DisplayOrder = 10,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysSystems.Add(sysSystem);

        await context.SaveChangesAsync();

        // 6. Screens
        var screenDashboard = new SysScreen
        {
            SystemId = sysSupport.Id,
            ScreenCode = "support-dashboard",
            ScreenName = "لوحة التحكم",
            ScreenNameE = "Dashboard",
            Route = "/support/dashboard",
            Icon = "grid",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysScreens.Add(screenDashboard);

        var screenTickets = new SysScreen
        {
            SystemId = sysSupport.Id,
            ScreenCode = "support-tickets",
            ScreenName = "التذاكر",
            ScreenNameE = "Tickets",
            Route = "/support/tickets",
            Icon = "ticket",
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysScreens.Add(screenTickets);

        var screenReports = new SysScreen
        {
            SystemId = sysSupport.Id,
            ScreenCode = "support-reports",
            ScreenName = "التقارير",
            ScreenNameE = "Reports",
            Route = "/support/reports",
            Icon = "chart",
            DisplayOrder = 3,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysScreens.Add(screenReports);

        var screenEmployees = new SysScreen
        {
            SystemId = sysHr.Id,
            ScreenCode = "hr-employees",
            ScreenName = "الموظفين",
            ScreenNameE = "Employees",
            Route = "/hr/employees",
            Icon = "person",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysScreens.Add(screenEmployees);

        var screenAttendance = new SysScreen
        {
            SystemId = sysHr.Id,
            ScreenCode = "hr-attendance",
            ScreenName = "الحضور والانصراف",
            ScreenNameE = "Attendance",
            Route = "/hr/attendance",
            Icon = "clock",
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysScreens.Add(screenAttendance);
        await context.SaveChangesAsync();

        // 7. Roles
        var roleAdmin = new SysRole
        {
            RoleNameAr = "مدير",
            RoleNameEn = "Administrator",
            Note = "Full system access",
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysRoles.Add(roleAdmin);

        var roleSupport = new SysRole
        {
            RoleNameAr = "فني دعم",
            RoleNameEn = "Support Technician",
            Note = "Support ticket management",
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysRoles.Add(roleSupport);

        var roleViewer = new SysRole
        {
            RoleNameAr = "مشاهد",
            RoleNameEn = "Viewer",
            Note = "Read-only access",
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysRoles.Add(roleViewer);
        await context.SaveChangesAsync();

        // 8. Users
        var user1 = new SysUser
        {
            FullNameAr = "أحمد محمد",
            FullNameEn = "Ahmed Mohammed",
            UserName = "ahmed",
            Password = passwordHasher.HashPassword("User@123"),
            Phone = "+966500000002",
            Role = roleAdmin.Id,
            BranchId = hqBranch.Id,
            CompanyId = company.Id,
            Email = "ahmed@techcompany.com",
            IsActive = true,
            IsAdmin = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysUsers.Add(user1);

        var user2 = new SysUser
        {
            FullNameAr = "سارة خالد",
            FullNameEn = "Sara Khaled",
            UserName = "sara",
            Password = passwordHasher.HashPassword("User@123"),
            Phone = "+966500000003",
            Role = roleSupport.Id,
            BranchId = hqBranch.Id,
            CompanyId = company.Id,
            Email = "sara@techcompany.com",
            IsActive = true,
            IsAdmin = false,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysUsers.Add(user2);

        var user3 = new SysUser
        {
            FullNameAr = "خالد عمر",
            FullNameEn = "Khaled Omar",
            UserName = "khaled",
            Password = passwordHasher.HashPassword("User@123"),
            Phone = "+966500000004",
            Role = roleViewer.Id,
            BranchId = branch2.Id,
            CompanyId = company.Id,
            Email = "khaled@techcompany.com",
            IsActive = true,
            IsAdmin = false,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysUsers.Add(user3);
        await context.SaveChangesAsync();

        // 9. Fiscal Years
        var fy2025 = new SysFiscalYear
        {
            CompanyId = company.Id,
            BranchId = hqBranch.Id,
            FiscalYearCode = "FY2025",
            FiscalYearNameAr = "السنة المالية 2025",
            FiscalYearNameEn = "Fiscal Year 2025",
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            IsClosed = false,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysFiscalYears.Add(fy2025);

        var fy2026 = new SysFiscalYear
        {
            CompanyId = company.Id,
            BranchId = hqBranch.Id,
            FiscalYearCode = "FY2026",
            FiscalYearNameAr = "السنة المالية 2026",
            FiscalYearNameEn = "Fiscal Year 2026",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            IsClosed = false,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysFiscalYears.Add(fy2026);
        await context.SaveChangesAsync();

        // 10. User Roles
        context.SysUserRoles.Add(new SysUserRole
        {
            UserId = user1.Id,
            RoleId = roleAdmin.Id,
            AssignedBy = admin.Id,
            AssignedDate = now,
            CreationUser = seedUser,
            CreationDate = now
        });
        context.SysUserRoles.Add(new SysUserRole
        {
            UserId = user2.Id,
            RoleId = roleSupport.Id,
            AssignedBy = admin.Id,
            AssignedDate = now,
            CreationUser = seedUser,
            CreationDate = now
        });
        context.SysUserRoles.Add(new SysUserRole
        {
            UserId = user3.Id,
            RoleId = roleViewer.Id,
            AssignedBy = admin.Id,
            AssignedDate = now,
            CreationUser = seedUser,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 11. Role Screen Permissions
        var allScreens = new[] { screenDashboard, screenTickets, screenReports, screenEmployees, screenAttendance };
        foreach (var screen in allScreens)
        {
            context.SysRoleScreenPermissions.Add(new SysRoleScreenPermission
            {
                RoleId = roleAdmin.Id,
                ScreenId = screen.Id,
                CanView = true,
                CanInsert = true,
                CanUpdate = true,
                CanDelete = true,
                CreationUser = seedUser,
                CreationDate = now
            });
        }

        foreach (var screen in new[] { screenDashboard, screenTickets })
        {
            context.SysRoleScreenPermissions.Add(new SysRoleScreenPermission
            {
                RoleId = roleSupport.Id,
                ScreenId = screen.Id,
                CanView = true,
                CanInsert = true,
                CanUpdate = true,
                CanDelete = false,
                CreationUser = seedUser,
                CreationDate = now
            });
        }

        context.SysRoleScreenPermissions.Add(new SysRoleScreenPermission
        {
            RoleId = roleViewer.Id,
            ScreenId = screenDashboard.Id,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = seedUser,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 12. User Screen Permissions
        context.SysUserScreenPermissions.Add(new SysUserScreenPermission
        {
            UserId = user2.Id,
            ScreenId = screenReports.Id,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            AssignedBy = admin.Id,
            AssignedDate = now,
            Notes = "Additional read access to reports",
            CreationUser = seedUser,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 13. Company System access
        foreach (var sys in new[] { sysSupport, sysHr })
        {
            context.SysCompanySystems.Add(new SysCompanySystem
            {
                CompanyId = company.Id,
                SystemId = sys.Id,
                IsAllowed = true,
                GrantedBy = admin.Id,
                GrantedDate = now,
                CreationUser = seedUser,
                CreationDate = now
            });
        }
        await context.SaveChangesAsync();

        // 14. Branch System access
        foreach (var branch in new[] { hqBranch, branch2 })
        {
            foreach (var sys in new[] { sysSupport, sysHr })
            {
                context.SysBranchSystems.Add(new SysBranchSystem
                {
                    BranchId = branch.Id,
                    SystemId = sys.Id,
                    IsAllowed = true,
                    GrantedBy = admin.Id,
                    GrantedDate = now,
                    CreationUser = seedUser,
                    CreationDate = now
                });
            }
        }
        await context.SaveChangesAsync();

        // 15. Branch Screen Permissions
        foreach (var screen in allScreens)
        {
            context.SysBranchScreenPermissions.Add(new SysBranchScreenPermission
            {
                BranchId = hqBranch.Id,
                ScreenId = screen.Id,
                CanView = true,
                CanInsert = true,
                CanUpdate = true,
                CanDelete = true,
                GrantedBy = admin.Id,
                GrantedDate = now,
                CreationUser = seedUser,
                CreationDate = now
            });
        }
        await context.SaveChangesAsync();

        // 16. Company Screen Permissions
        foreach (var screen in allScreens)
        {
            context.SysCompanyScreenPermissions.Add(new SysCompanyScreenPermission
            {
                CompanyId = company.Id,
                ScreenId = screen.Id,
                CanView = true,
                CanInsert = true,
                CanUpdate = true,
                CanDelete = true,
                GrantedBy = admin.Id,
                GrantedDate = now,
                CreationUser = seedUser,
                CreationDate = now
            });
        }
        await context.SaveChangesAsync();

        // 17. Ticket Priority
        var priorityCritical = new SysTicketPriority
        {
            PriorityNameAr = "حرج",
            PriorityNameEn = "Critical",
            PriorityLevel = 1,
            SlaTargetHours = 1,
            EscalationThresholdHours = 0.5m,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketPriorities.Add(priorityCritical);

        var priorityHigh = new SysTicketPriority
        {
            PriorityNameAr = "عالية",
            PriorityNameEn = "High",
            PriorityLevel = 2,
            SlaTargetHours = 4,
            EscalationThresholdHours = 2,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketPriorities.Add(priorityHigh);

        var priorityMedium = new SysTicketPriority
        {
            PriorityNameAr = "متوسطة",
            PriorityNameEn = "Medium",
            PriorityLevel = 3,
            SlaTargetHours = 8,
            EscalationThresholdHours = 4,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketPriorities.Add(priorityMedium);

        var priorityLow = new SysTicketPriority
        {
            PriorityNameAr = "منخفضة",
            PriorityNameEn = "Low",
            PriorityLevel = 4,
            SlaTargetHours = 24,
            EscalationThresholdHours = 12,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketPriorities.Add(priorityLow);
        await context.SaveChangesAsync();

        // 18. Ticket Status
        var statusOpen = new SysTicketStatus
        {
            StatusNameAr = "مفتوحة",
            StatusNameEn = "Open",
            StatusCode = "OPEN",
            DisplayOrder = 1,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketStatuses.Add(statusOpen);

        var statusInProgress = new SysTicketStatus
        {
            StatusNameAr = "قيد المعالجة",
            StatusNameEn = "In Progress",
            StatusCode = "IN_PROGRESS",
            DisplayOrder = 2,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketStatuses.Add(statusInProgress);

        var statusResolved = new SysTicketStatus
        {
            StatusNameAr = "تم الحل",
            StatusNameEn = "Resolved",
            StatusCode = "RESOLVED",
            DisplayOrder = 3,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketStatuses.Add(statusResolved);

        var statusClosed = new SysTicketStatus
        {
            StatusNameAr = "مغلقة",
            StatusNameEn = "Closed",
            StatusCode = "CLOSED",
            DisplayOrder = 4,
            IsFinalStatus = true,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        };
        context.SysTicketStatuses.Add(statusClosed);
        await context.SaveChangesAsync();

        // 19. Ticket Type
        context.SysTicketTypes.Add(new SysTicketType
        {
            TypeNameAr = "خطأ برمجي",
            TypeNameEn = "Bug Report",
            DefaultPriorityId = priorityHigh.Id,
            SlaTargetHours = 4,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        });
        context.SysTicketTypes.Add(new SysTicketType
        {
            TypeNameAr = "طلب تطوير",
            TypeNameEn = "Feature Request",
            DefaultPriorityId = priorityMedium.Id,
            SlaTargetHours = 24,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        });
        context.SysTicketTypes.Add(new SysTicketType
        {
            TypeNameAr = "استفسار",
            TypeNameEn = "Inquiry",
            DefaultPriorityId = priorityLow.Id,
            SlaTargetHours = 48,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 20. Ticket Category
        context.SysTicketCategories.Add(new SysTicketCategory
        {
            CategoryNameAr = "شبكات",
            CategoryNameEn = "Network",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        });
        context.SysTicketCategories.Add(new SysTicketCategory
        {
            CategoryNameAr = "برمجيات",
            CategoryNameEn = "Software",
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        });
        context.SysTicketCategories.Add(new SysTicketCategory
        {
            CategoryNameAr = "أجهزة",
            CategoryNameEn = "Hardware",
            DisplayOrder = 3,
            IsActive = true,
            CreationUser = seedUser,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 21. Ticket Config
        context.SysTicketConfigs.AddRange(
            new SysTicketConfig { ConfigKey = "SLA.Priority.Critical.Hours", ConfigValue = "1", ConfigType = "SLA", IsActive = true, CreationUser = seedUser, CreationDate = now },
            new SysTicketConfig { ConfigKey = "SLA.Priority.High.Hours", ConfigValue = "4", ConfigType = "SLA", IsActive = true, CreationUser = seedUser, CreationDate = now },
            new SysTicketConfig { ConfigKey = "Attachment.MaxFileSize", ConfigValue = "10485760", ConfigType = "FileAttachment", IsActive = true, CreationUser = seedUser, CreationDate = now },
            new SysTicketConfig { ConfigKey = "Attachment.AllowedExtensions", ConfigValue = ".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt", ConfigType = "FileAttachment", IsActive = true, CreationUser = seedUser, CreationDate = now }
        );
        await context.SaveChangesAsync();

        // 22. Request Tickets
        var ticket1 = new SysRequestTicket
        {
            TitleAr = "تعطل الشبكة في الفرع الرئيسي",
            TitleEn = "Network outage at headquarters",
            Description = "All network services are down at HQ since 9:00 AM.",
            CompanyId = company.Id,
            BranchId = hqBranch.Id,
            RequesterId = user1.Id,
            AssigneeId = user2.Id,
            TicketTypeId = 1,
            TicketStatusId = statusInProgress.Id,
            TicketPriorityId = priorityCritical.Id,
            TicketCategoryId = 1,
            IsActive = true,
            CreationUser = user1.UserName,
            CreationDate = now
        };
        context.SysRequestTickets.Add(ticket1);

        var ticket2 = new SysRequestTicket
        {
            TitleAr = "طلب تثبيت برنامج محاسبة",
            TitleEn = "Request to install accounting software",
            Description = "Need to install accounting software on 5 new machines in Riyadh branch.",
            CompanyId = company.Id,
            BranchId = branch2.Id,
            RequesterId = user3.Id,
            AssigneeId = user2.Id,
            TicketTypeId = 2,
            TicketStatusId = statusOpen.Id,
            TicketPriorityId = priorityMedium.Id,
            TicketCategoryId = 2,
            IsActive = true,
            CreationUser = user3.UserName,
            CreationDate = now
        };
        context.SysRequestTickets.Add(ticket2);
        await context.SaveChangesAsync();

        // 23. Ticket Comments
        context.SysTicketComments.Add(new SysTicketComment
        {
            TicketId = ticket1.Id,
            CommentText = "We are investigating the issue. Will provide update in 30 minutes.",
            IsInternal = false,
            CreationUser = user2.UserName,
            CreationDate = now
        });
        context.SysTicketComments.Add(new SysTicketComment
        {
            TicketId = ticket1.Id,
            CommentText = "Internal note: Check the main switch in server room.",
            IsInternal = true,
            CreationUser = user2.UserName,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 24. Saved Searches
        context.SysSavedSearches.Add(new SysSavedSearch
        {
            UserId = user1.Id,
            SearchName = "My Open Tickets",
            SearchDescription = "All open tickets assigned to me",
            SearchCriteria = "{\"status\":\"OPEN\",\"assigneeId\":\"current\"}",
            IsPublic = false,
            IsDefault = true,
            UsageCount = 5,
            LastUsedDate = now,
            IsActive = true,
            CreationUser = user1.UserName,
            CreationDate = now
        });
        context.SysSavedSearches.Add(new SysSavedSearch
        {
            UserId = user2.Id,
            SearchName = "Critical Tickets",
            SearchDescription = "All unassigned critical tickets",
            SearchCriteria = "{\"priority\":\"CRITICAL\",\"assigneeId\":null}",
            IsPublic = true,
            IsDefault = false,
            UsageCount = 12,
            LastUsedDate = now,
            IsActive = true,
            CreationUser = user2.UserName,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 25. Search Analytics
        context.SysSearchAnalytics.Add(new SysSearchAnalytics
        {
            UserId = user1.Id,
            SearchTerm = "network",
            SearchCriteria = "{\"keyword\":\"network\"}",
            FilterLogic = "OR",
            ResultCount = 3,
            ExecutionTimeMs = 45,
            SearchDate = now,
            CompanyId = company.Id,
            BranchId = hqBranch.Id
        });
        context.SysSearchAnalytics.Add(new SysSearchAnalytics
        {
            UserId = user2.Id,
            SearchTerm = "open tickets",
            ResultCount = 15,
            ExecutionTimeMs = 120,
            SearchDate = now,
            CompanyId = company.Id,
            BranchId = hqBranch.Id
        });
        await context.SaveChangesAsync();

        // 26. Security Threats
        context.SysSecurityThreats.Add(new SysSecurityThreat
        {
            ThreatType = "BRUTE_FORCE",
            Severity = "HIGH",
            IpAddress = "192.168.1.100",
            UserId = user1.Id,
            CompanyId = company.Id,
            Description = "Multiple failed login attempts detected from unknown IP",
            DetectionDate = now.AddHours(-2),
            Status = "Acknowledged",
            AcknowledgedBy = admin.Id,
            AcknowledgedDate = now.AddHours(-1)
        });
        await context.SaveChangesAsync();

        // 27. Failed Logins
        context.SysFailedLogins.Add(new SysFailedLogin
        {
            IpAddress = "10.0.0.50",
            Username = "unknown_user",
            FailureReason = "Invalid username",
            AttemptDate = now.AddHours(-3)
        });
        context.SysFailedLogins.Add(new SysFailedLogin
        {
            IpAddress = "192.168.1.100",
            Username = "ahmed",
            FailureReason = "Invalid password",
            AttemptDate = now.AddHours(-2)
        });
        await context.SaveChangesAsync();

        // 28. Performance Metrics
        context.SysPerformanceMetrics.Add(new SysPerformanceMetric
        {
            EndpointPath = "/api/auth/login",
            HourTimestamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc),
            RequestCount = 150,
            AvgExecutionTimeMs = 235.5m,
            MinExecutionTimeMs = 120m,
            MaxExecutionTimeMs = 890m,
            P50ExecutionTimeMs = 210m,
            P95ExecutionTimeMs = 450m,
            P99ExecutionTimeMs = 780m,
            AvgDatabaseTimeMs = 85.3m,
            AvgQueryCount = 3.2m,
            ErrorCount = 2,
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 29. Slow Queries
        context.SysSlowQueries.Add(new SysSlowQuery
        {
            CorrelationId = Guid.NewGuid().ToString(),
            SqlStatement = "SELECT * FROM SysRequestTickets WHERE TitleAr LIKE '%network%'",
            ExecutionTimeMs = 3200,
            RowsAffected = 150,
            EndpointPath = "/api/tickets/search",
            UserId = user1.Id,
            CompanyId = company.Id,
            CreationDate = now.AddMinutes(-30)
        });
        await context.SaveChangesAsync();

        // 30. Audit Logs
        context.SysAuditLogs.Add(new SysAuditLog
        {
            ActorType = "USER",
            ActorId = user1.Id,
            CompanyId = company.Id,
            BranchId = hqBranch.Id,
            Action = "LOGIN",
            EntityType = "SysUser",
            EntityId = user1.Id,
            IpAddress = "192.168.1.10",
            UserAgent = "Mozilla/5.0",
            CorrelationId = Guid.NewGuid().ToString(),
            HttpMethod = "POST",
            EndpointPath = "/api/auth/login",
            ExecutionTimeMs = 150,
            StatusCode = 200,
            Severity = "Info",
            EventCategory = "Authentication",
            CreationDate = now
        });
        await context.SaveChangesAsync();

        // 31. Retention Policies
        context.SysRetentionPolicies.Add(new SysRetentionPolicy
        {
            EventCategory = "Authentication",
            RetentionDays = 365,
            ArchiveEnabled = true,
            Description = "Keep authentication logs for 1 year, then archive",
            LastModifiedDate = now,
            LastModifiedBy = admin.Id
        });
        context.SysRetentionPolicies.Add(new SysRetentionPolicy
        {
            EventCategory = "DataChange",
            RetentionDays = 730,
            ArchiveEnabled = true,
            Description = "Keep data change audit logs for 2 years, then archive",
            LastModifiedDate = now,
            LastModifiedBy = admin.Id
        });
        context.SysRetentionPolicies.Add(new SysRetentionPolicy
        {
            EventCategory = "Security",
            RetentionDays = 1825,
            ArchiveEnabled = true,
            Description = "Keep security events for 5 years",
            LastModifiedDate = now,
            LastModifiedBy = admin.Id
        });
        await context.SaveChangesAsync();

        // 32. Report Schedules
        context.SysReportSchedules.Add(new SysReportSchedule
        {
            ReportType = "TicketSummary",
            Frequency = "Daily",
            TimeOfDay = "08:00",
            Recipients = "managers@techcompany.com",
            ExportFormat = "PDF",
            IsActive = true,
            CreatedByUserId = user1.Id,
            CreatedAt = now
        });
        context.SysReportSchedules.Add(new SysReportSchedule
        {
            ReportType = "SlaCompliance",
            Frequency = "Weekly",
            DayOfWeek = 1,
            TimeOfDay = "09:00",
            Recipients = "support-team@techcompany.com",
            ExportFormat = "Excel",
            IsActive = true,
            CreatedByUserId = user1.Id,
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        // 33. SysCode lookup values (replaces all static enums/constants)
        // Each code has two rows: CODE_LANG=1 (Arabic), CODE_LANG=2 (English)
        var sysCodes = new List<SysCode>();
        int mgr = 0;

        // Helper to add a code value in both languages
        void AddCode(int codeMnr, string nameAr, string nameEn, string codeValue)
        {
            sysCodes.Add(new SysCode { CodeMgr = mgr, CodeMnr = codeMnr, CodeLang = 1, CodeDesc = nameAr, CodeValue = codeValue, IsActive = 1, CreationUser = seedUser, CreationDate = now });
            sysCodes.Add(new SysCode { CodeMgr = mgr, CodeMnr = codeMnr, CodeLang = 2, CodeDesc = nameEn, CodeValue = codeValue, IsActive = 1, CreationUser = seedUser, CreationDate = now });
        }

        // Document Categories (mgr=1)
        mgr = 1; AddCode(1, "عقود", "Contracts", "Contracts"); AddCode(2, "تقارير", "Reports", "Reports"); AddCode(3, "فواتير", "Invoices", "Invoices");
        AddCode(4, "إيصالات", "Receipts", "Receipts"); AddCode(5, "هوية شخصية", "Identification", "Identification"); AddCode(6, "شهادات", "Certificates", "Certificates");
        AddCode(7, "مالية", "Financial", "Financial"); AddCode(8, "موارد بشرية", "HR", "HR"); AddCode(9, "قانونية", "Legal", "Legal");
        AddCode(10, "فنية", "Technical", "Technical"); AddCode(11, "تسويق", "Marketing", "Marketing"); AddCode(12, "أخرى", "Other", "Other");

        // Owner Types (mgr=2)
        mgr = 2; AddCode(1, "شركة", "Company", "Company"); AddCode(2, "فرع", "Branch", "Branch"); AddCode(3, "مدير النظام", "Super Admin", "Super Admin");

        // Threat Types (mgr=3)
        mgr = 3; AddCode(1, "هجوم تخمين كلمة المرور", "Brute Force Attack", "Brute Force Attack"); AddCode(2, "حقن SQL", "SQL Injection", "SQL Injection");
        AddCode(3, "برمجة عبر المواقع", "Cross-Site Scripting", "Cross-Site Scripting"); AddCode(4, "هجوم حجب الخدمة", "Denial of Service", "Denial of Service");
        AddCode(5, "محاولة دخول مشبوهة", "Suspicious Login", "Suspicious Login"); AddCode(6, "وصول غير مصرح", "Unauthorized Access", "Unauthorized Access");
        AddCode(7, "تسريب بيانات", "Data Exfiltration", "Data Exfiltration"); AddCode(8, "برمجيات خبيثة", "Malware Detected", "Malware Detected");

        // Threat Severity (mgr=4)
        mgr = 4; AddCode(1, "منخفض", "Low", "Low"); AddCode(2, "متوسط", "Medium", "Medium"); AddCode(3, "عالي", "High", "High"); AddCode(4, "حرج", "Critical", "Critical");

        // Audit Severity (mgr=5)
        mgr = 5; AddCode(1, "معلومات", "Info", "Info"); AddCode(2, "تحذير", "Warning", "Warning"); AddCode(3, "خطأ", "Error", "Error"); AddCode(4, "حرج", "Critical", "Critical");

        // Event Categories (mgr=6)
        mgr = 6; AddCode(1, "مصادقة", "Authentication", "Authentication"); AddCode(2, "تفويض", "Authorization", "Authorization"); AddCode(3, "تغيير بيانات", "Data Change", "DataChange");
        AddCode(4, "إعدادات", "Configuration", "Configuration"); AddCode(5, "أمان", "Security", "Security"); AddCode(6, "نظام", "System", "System"); AddCode(7, "تكامل", "Integration", "Integration");
        AddCode(8, "صلاحيات", "Permission", "Permission"); AddCode(9, "استثناء", "Exception", "Exception"); AddCode(10, "طلب", "Request", "Request");

        // Actor Types (mgr=7)
        mgr = 7; AddCode(1, "مستخدم", "User", "USER"); AddCode(2, "مدير النظام", "Super Admin", "SUPER_ADMIN"); AddCode(3, "النظام", "System", "SYSTEM"); AddCode(4, "مجهول", "Anonymous", "ANONYMOUS");
        AddCode(5, "مدير الشركة", "Company Admin", "COMPANY_ADMIN");
        AddCode(6, "مسؤول", "Admin", "ADMIN");
 
        // Audit Event Types (mgr=8)
        mgr = 8; AddCode(1, "طلب", "Request", "Request"); AddCode(2, "استثناء", "Exception", "Exception"); AddCode(3, "أمني", "Security", "Security");

        // Payload Logging Levels (mgr=9)
        mgr = 9; AddCode(1, "لا شيء", "None", "None"); AddCode(2, "البيانات الوصفية فقط", "Metadata Only", "Metadata Only"); AddCode(3, "كامل", "Full", "Full");

        // System Health Status (mgr=10)
        mgr = 10; AddCode(1, "سليم", "Healthy", "Healthy"); AddCode(2, "متدهور", "Degraded", "Degraded"); AddCode(3, "غير سليم", "Unhealthy", "Unhealthy"); AddCode(4, "غير مستجيب", "Unresponsive", "Unresponsive");

        // Memory Pressure Severity (mgr=11)
        mgr = 11; AddCode(1, "طبيعي", "Normal", "Normal"); AddCode(2, "تحذير", "Warning", "Warning"); AddCode(3, "حرج", "Critical", "Critical"); AddCode(4, "شديد", "Severe", "Severe"); AddCode(5, "غير معروف", "Unknown", "Unknown");

        // Key Types (mgr=12)
        mgr = 12; AddCode(1, "مفتاح API", "API Key", "API Key"); AddCode(2, "مفتاح توقيع", "Signing Key", "Signing Key"); AddCode(3, "مفتاح تشفير", "Encryption Key", "Encryption Key");
        AddCode(4, "مفتاح داخلي", "Internal Key", "Internal Key"); AddCode(5, "مفتاح خارجي", "External Key", "External Key");

        // Alert Types (mgr=13)
        mgr = 13; AddCode(1, "تنبيه أمني", "Security Alert", "Security Alert"); AddCode(2, "تنبيه أداء", "Performance Alert", "Performance Alert"); AddCode(3, "تنبيه نظام", "System Alert", "System Alert");
        AddCode(4, "تنبيه أعمال", "Business Alert", "Business Alert");

        // Languages (mgr=14)
        mgr = 14; AddCode(1, "العربية", "Arabic", "Arabic"); AddCode(2, "الإنجليزية", "English", "English");

        // Audit Status (mgr=15)
        mgr = 15; AddCode(1, "غير محلول", "Unresolved", "Unresolved"); AddCode(2, "قيد المعالجة", "In Progress", "In Progress"); AddCode(3, "تم الحل", "Resolved", "Resolved"); AddCode(4, "حرج", "Critical", "Critical");

        context.SysCodes.AddRange(sysCodes);
        await context.SaveChangesAsync();

        // 34. System Settings
        var settings = new List<SysSetting>
        {
            new() { SettingCode = 1, SettingDesc = "Upload files path", SettingValue = "/THINKON_FILES/UPLOADS/" },
            new() { SettingCode = 2, SettingDesc = "Maximum file size (bytes)", SettingValue = "52428800" },
            new() { SettingCode = 3, SettingDesc = "Allowed file extensions", SettingValue = ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.jpg,.jpeg,.png,.gif,.zip,.rar,.7z" },
            new() { SettingCode = 4, SettingDesc = "Logs path", SettingValue = "/THINKON_FILES/LOGS/" },
            new() { SettingCode = 5, SettingDesc = "Audit fallback path", SettingValue = "/THINKON_FILES/LOGS/audit-fallback/" },
            new() { SettingCode = 6, SettingDesc = "Logos path", SettingValue = "/THINKON_FILES/LOGOS/" },
        };
        context.SysSettings.AddRange(settings);
        await context.SaveChangesAsync();
    }
}
