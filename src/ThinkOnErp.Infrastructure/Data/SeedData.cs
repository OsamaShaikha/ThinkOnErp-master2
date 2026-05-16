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
            TaxNumber = "300123456700003",
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
    }
}
