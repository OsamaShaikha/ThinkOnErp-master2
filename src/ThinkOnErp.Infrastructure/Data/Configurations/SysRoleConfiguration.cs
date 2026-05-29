using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysRoleConfiguration : IEntityTypeConfiguration<SysRole>
{
    public void Configure(EntityTypeBuilder<SysRole> builder)
    {
        builder.ToTable("SYS_ROLE");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
builder.Property(e => e.RoleNameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
builder.Property(e => e.RoleNameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Note).HasColumnName("NOTE").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysCurrencyConfiguration : IEntityTypeConfiguration<SysCurrency>
{
    public void Configure(EntityTypeBuilder<SysCurrency> builder)
    {
        builder.ToTable("SYS_CURRENCY");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
builder.Property(e => e.CurrencyNameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
builder.Property(e => e.CurrencyNameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ShortNameAr).HasColumnName("SHORT_DESC").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ShortNameEn).HasColumnName("SHORT_DESC_E").HasMaxLength(50).IsRequired();
        builder.Property(e => e.SingularNameAr).HasColumnName("SINGULER_DESC").HasMaxLength(50).IsRequired();
        builder.Property(e => e.SingularNameEn).HasColumnName("SINGULER_DESC_E").HasMaxLength(50).IsRequired();
        builder.Property(e => e.DualNameAr).HasColumnName("DUAL_DESC").HasMaxLength(50).IsRequired();
        builder.Property(e => e.DualNameEn).HasColumnName("DUAL_DESC_E").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CollectiveNameAr).HasColumnName("SUM_DESC").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CollectiveNameEn).HasColumnName("SUM_DESC_E").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FractionNameAr).HasColumnName("FRAC_DESC").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FractionNameEn).HasColumnName("FRAC_DESC_E").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CurrRate).HasColumnName("CURR_RATE").HasColumnType("DECIMAL(18,6)");
        builder.Property(e => e.CurrRateDate).HasColumnName("CURR_RATE_DATE");
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysCompanyConfiguration : IEntityTypeConfiguration<SysCompany>
{
    public void Configure(EntityTypeBuilder<SysCompany> builder)
    {
        builder.ToTable("SYS_COMPANY");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
builder.Property(e => e.CompanyNameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
builder.Property(e => e.CompanyNameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CountryId).HasColumnName("COUNTRY_ID");
        builder.Property(e => e.CurrId).HasColumnName("CURR_ID");
        builder.Property(e => e.LegalName).HasColumnName("LEGAL_NAME").HasMaxLength(300);
        builder.Property(e => e.LegalNameE).HasColumnName("LEGAL_NAME_E").HasMaxLength(300);
        builder.Property(e => e.CompanyCode).HasColumnName("COMPANY_CODE").HasMaxLength(50);
        builder.Property(e => e.DefaultBranchId).HasColumnName("DEFAULT_BRANCH_ID");
        builder.Property(e => e.CompanyLogoPath).HasColumnName("COMPANY_LOGO_PATH").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        // Navigation
        builder.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.DefaultBranch).WithMany().HasForeignKey(e => e.DefaultBranchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SysBranchConfiguration : IEntityTypeConfiguration<SysBranch>
{
    public void Configure(EntityTypeBuilder<SysBranch> builder)
    {
        builder.ToTable("SYS_BRANCH");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
builder.Property(e => e.BranchNameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
builder.Property(e => e.BranchNameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(50);
        builder.Property(e => e.Mobile).HasColumnName("MOBILE").HasMaxLength(50);
        builder.Property(e => e.Fax).HasColumnName("FAX").HasMaxLength(50);
        builder.Property(e => e.Email).HasColumnName("EMAIL").HasMaxLength(200);
        builder.Property(e => e.TaxNumber).HasColumnName("TAX_NUMBER").HasMaxLength(50);
        builder.Property(e => e.IsHeadBranch).HasColumnName("IS_HEAD_BRANCH").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.DefaultLang).HasColumnName("DEFAULT_LANG");
        builder.Property(e => e.BaseCurrencyId).HasColumnName("BASE_CURRENCY_ID");
        builder.Property(e => e.RoundingRules).HasColumnName("ROUNDING_RULES");
        builder.Property(e => e.BranchLogoPath).HasColumnName("BRANCH_LOGO_PATH").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        // Navigation
        builder.HasOne(e => e.BaseCurrency).WithMany().HasForeignKey(e => e.BaseCurrencyId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Company).WithMany(e => e.Branches).HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SysUserConfiguration : IEntityTypeConfiguration<SysUser>
{
    public void Configure(EntityTypeBuilder<SysUser> builder)
    {
        builder.ToTable("SYS_USERS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
builder.Property(e => e.FullNameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
builder.Property(e => e.FullNameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.UserName).HasColumnName("USER_NAME").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.UserName).IsUnique();
        builder.Property(e => e.Password).HasColumnName("PASSWORD").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(50);
        builder.Property(e => e.Phone2).HasColumnName("PHONE2").HasMaxLength(50);
        builder.Property(e => e.Role).HasColumnName("ROLE");
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
        builder.Property(e => e.Email).HasColumnName("EMAIL").HasMaxLength(200);
        builder.Property(e => e.LastLoginDate).HasColumnName("LAST_LOGIN_DATE");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.IsAdmin).HasColumnName("IS_ADMIN").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
        builder.Property(e => e.RefreshToken).HasColumnName("REFRESH_TOKEN").HasMaxLength(500);
        builder.Property(e => e.RefreshTokenExpiry).HasColumnName("REFRESH_TOKEN_EXPIRY");
        builder.Property(e => e.ForceLogoutDate).HasColumnName("FORCE_LOGOUT_DATE");
    }
}

public class SysFiscalYearConfiguration : IEntityTypeConfiguration<SysFiscalYear>
{
    public void Configure(EntityTypeBuilder<SysFiscalYear> builder)
    {
        builder.ToTable("SYS_FISCAL_YEAR");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.FiscalYearCode).HasColumnName("FISCAL_YEAR_CODE").HasMaxLength(20).IsRequired();
builder.Property(e => e.FiscalYearNameAr).HasColumnName("NAME_AR").HasMaxLength(200);
builder.Property(e => e.FiscalYearNameEn).HasColumnName("NAME_EN").HasMaxLength(200);
        builder.Property(e => e.StartDate).HasColumnName("START_DATE").IsRequired();
        builder.Property(e => e.EndDate).HasColumnName("END_DATE").IsRequired();
        builder.Property(e => e.IsClosed).HasColumnName("IS_CLOSED").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        // Navigation
        builder.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SysSuperAdminConfiguration : IEntityTypeConfiguration<SysSuperAdmin>
{
    public void Configure(EntityTypeBuilder<SysSuperAdmin> builder)
    {
        builder.ToTable("SYS_SUPER_ADMIN");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
builder.Property(e => e.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
builder.Property(e => e.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.UserName).HasColumnName("USER_NAME").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.UserName).IsUnique();
        builder.Property(e => e.Password).HasColumnName("PASSWORD").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Email).HasColumnName("EMAIL").HasMaxLength(200);
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.Phone).HasColumnName("PHONE").HasMaxLength(50);
        builder.Property(e => e.TwoFaSecret).HasColumnName("TWO_FA_SECRET").HasMaxLength(200);
        builder.Property(e => e.TwoFaEnabled).HasColumnName("TWO_FA_ENABLED").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.LastLoginDate).HasColumnName("LAST_LOGIN_DATE");
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysSystemConfiguration : IEntityTypeConfiguration<SysSystem>
{
    public void Configure(EntityTypeBuilder<SysSystem> builder)
    {
        builder.ToTable("SYS_SYSTEM");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.SystemCode).HasColumnName("SYSTEM_CODE").HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.SystemCode).IsUnique();
        builder.Property(e => e.SystemName).HasColumnName("SYSTEM_NAME").HasMaxLength(200).IsRequired();
        builder.Property(e => e.SystemNameE).HasColumnName("SYSTEM_NAME_E").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(e => e.DescriptionE).HasColumnName("DESCRIPTION_E").HasMaxLength(500);
        builder.Property(e => e.Icon).HasColumnName("ICON").HasMaxLength(100);
        builder.Property(e => e.DisplayOrder).HasColumnName("DISPLAY_ORDER");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysScreenConfiguration : IEntityTypeConfiguration<SysScreen>
{
    public void Configure(EntityTypeBuilder<SysScreen> builder)
    {
        builder.ToTable("SYS_SCREEN");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.SystemId).HasColumnName("SYSTEM_ID").IsRequired();
        builder.Property(e => e.ParentScreenId).HasColumnName("PARENT_SCREEN_ID");
        builder.Property(e => e.ScreenCode).HasColumnName("SCREEN_CODE").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.ScreenCode).IsUnique();
        builder.Property(e => e.ScreenName).HasColumnName("SCREEN_NAME").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ScreenNameE).HasColumnName("SCREEN_NAME_E").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Route).HasColumnName("ROUTE").HasMaxLength(200);
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(e => e.DescriptionE).HasColumnName("DESCRIPTION_E").HasMaxLength(500);
        builder.Property(e => e.Icon).HasColumnName("ICON").HasMaxLength(100);
        builder.Property(e => e.DisplayOrder).HasColumnName("DISPLAY_ORDER");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.System).WithMany(e => e.Screens).HasForeignKey(e => e.SystemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ParentScreen).WithMany().HasForeignKey(e => e.ParentScreenId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SysUserRoleConfiguration : IEntityTypeConfiguration<SysUserRole>
{
    public void Configure(EntityTypeBuilder<SysUserRole> builder)
    {
        builder.ToTable("SYS_USERS_ROLES");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(e => e.RoleId).HasColumnName("ROLE_ID").IsRequired();
        builder.Property(e => e.AssignedBy).HasColumnName("ASSIGNED_BY");
        builder.Property(e => e.AssignedDate).HasColumnName("ASSIGNED_DATE");
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");

        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SysRoleScreenPermissionConfiguration : IEntityTypeConfiguration<SysRoleScreenPermission>
{
    public void Configure(EntityTypeBuilder<SysRoleScreenPermission> builder)
    {
        builder.ToTable("SYS_ROLE_SCREEN_PERMISSIONS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.RoleId).HasColumnName("ROLE_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.CanView).HasColumnName("CAN_VIEW").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanInsert).HasColumnName("CAN_INSERT").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanUpdate).HasColumnName("CAN_UPDATE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanDelete).HasColumnName("CAN_DELETE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Screen).WithMany().HasForeignKey(e => e.ScreenId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SysUserScreenPermissionConfiguration : IEntityTypeConfiguration<SysUserScreenPermission>
{
    public void Configure(EntityTypeBuilder<SysUserScreenPermission> builder)
    {
        builder.ToTable("SYS_USER_SCREEN_PERMISSIONS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.CanView).HasColumnName("CAN_VIEW").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanInsert).HasColumnName("CAN_INSERT").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanUpdate).HasColumnName("CAN_UPDATE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanDelete).HasColumnName("CAN_DELETE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.AssignedBy).HasColumnName("ASSIGNED_BY");
        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysBranchSystemConfiguration : IEntityTypeConfiguration<SysBranchSystem>
{
    public void Configure(EntityTypeBuilder<SysBranchSystem> builder)
    {
        builder.ToTable("SYS_BRANCH_SYSTEMS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.SystemId).HasColumnName("SYSTEM_ID").IsRequired();
        builder.Property(e => e.IsAllowed).HasColumnName("IS_ALLOWED").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.GrantedBy).HasColumnName("GRANTED_BY");
        builder.Property(e => e.GrantedDate).HasColumnName("GRANTED_DATE");
        builder.Property(e => e.RevokedDate).HasColumnName("REVOKED_DATE");
        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Company).WithMany(e => e.BranchSystemAccess).HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Branch).WithMany(e => e.SystemAccess).HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.System).WithMany(e => e.BranchAccess).HasForeignKey(e => e.SystemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Granter).WithMany().HasForeignKey(e => e.GrantedBy).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SysBranchScreenPermissionConfiguration : IEntityTypeConfiguration<SysBranchScreenPermission>
{
    public void Configure(EntityTypeBuilder<SysBranchScreenPermission> builder)
    {
        builder.ToTable("SYS_BRANCH_SCREEN_PERMISSIONS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.CanView).HasColumnName("CAN_VIEW").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanInsert).HasColumnName("CAN_INSERT").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanUpdate).HasColumnName("CAN_UPDATE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanDelete).HasColumnName("CAN_DELETE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.GrantedBy).HasColumnName("GRANTED_BY");
        builder.Property(e => e.GrantedDate).HasColumnName("GRANTED_DATE");
        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Company).WithMany(e => e.BranchScreenPermissions).HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Branch).WithMany(e => e.ScreenPermissions).HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Screen).WithMany(e => e.BranchPermissions).HasForeignKey(e => e.ScreenId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Granter).WithMany().HasForeignKey(e => e.GrantedBy).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SysCompanyScreenPermissionConfiguration : IEntityTypeConfiguration<SysCompanyScreenPermission>
{
    public void Configure(EntityTypeBuilder<SysCompanyScreenPermission> builder)
    {
        builder.ToTable("SYS_COMPANY_SCREEN_PERMISSIONS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(e => e.ScreenId).HasColumnName("SCREEN_ID").IsRequired();
        builder.Property(e => e.CanView).HasColumnName("CAN_VIEW").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanInsert).HasColumnName("CAN_INSERT").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanUpdate).HasColumnName("CAN_UPDATE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.CanDelete).HasColumnName("CAN_DELETE").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.GrantedBy).HasColumnName("GRANTED_BY");
        builder.Property(e => e.GrantedDate).HasColumnName("GRANTED_DATE");
        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Company).WithMany(e => e.ScreenPermissions).HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Screen).WithMany(e => e.CompanyPermissions).HasForeignKey(e => e.ScreenId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Granter).WithMany().HasForeignKey(e => e.GrantedBy).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SysCompanySystemConfiguration : IEntityTypeConfiguration<SysCompanySystem>
{
    public void Configure(EntityTypeBuilder<SysCompanySystem> builder)
    {
        builder.ToTable("SYS_COMPANY_SYSTEMS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(e => e.SystemId).HasColumnName("SYSTEM_ID").IsRequired();
        builder.Property(e => e.IsAllowed).HasColumnName("IS_ALLOWED").HasConversion<string>(v => v ? "Y" : "N", v => v == "Y").HasMaxLength(1);
        builder.Property(e => e.GrantedBy).HasColumnName("GRANTED_BY");
        builder.Property(e => e.GrantedDate).HasColumnName("GRANTED_DATE");
        builder.Property(e => e.RevokedDate).HasColumnName("REVOKED_DATE");
        builder.Property(e => e.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysCodeConfiguration : IEntityTypeConfiguration<SysCode>
{
    public void Configure(EntityTypeBuilder<SysCode> builder)
    {
        builder.ToTable("SYS_CODE");
        builder.HasKey(e => new { e.CodeMgr, e.CodeMnr, e.CodeLang });
        builder.Property(e => e.CodeMgr).HasColumnName("CODE_MGR").IsRequired();
        builder.Property(e => e.CodeMnr).HasColumnName("CODE_MNR").IsRequired();
        builder.Property(e => e.CodeLang).HasColumnName("CODE_LANG").IsRequired();
        builder.Property(e => e.CodeDesc).HasColumnName("CODE_DESC").HasMaxLength(1000);
        builder.Property(e => e.CodeValue).HasColumnName("CODE_VALUE").HasMaxLength(200);
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasConversion<int>().HasDefaultValue(1);
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysSettingConfiguration : IEntityTypeConfiguration<SysSetting>
{
    public void Configure(EntityTypeBuilder<SysSetting> builder)
    {
        builder.ToTable("SYS_SETTINGS");
        builder.HasKey(e => e.SettingCode);
        builder.Property(e => e.SettingCode).HasColumnName("SETTING_CODE").ValueGeneratedNever();
        builder.Property(e => e.SettingDesc).HasColumnName("SETTING_DESC").HasMaxLength(500).IsRequired();
        builder.Property(e => e.SettingValue).HasColumnName("SETTING_VALUE").HasMaxLength(2000).IsRequired();
    }
}
