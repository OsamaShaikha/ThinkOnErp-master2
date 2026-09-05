using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class SSCPolicyConfiguration : IEntityTypeConfiguration<SSCPolicy>
{
    public void Configure(EntityTypeBuilder<SSCPolicy> builder)
    {
        builder.ToTable("HR_SSC_POLICY");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(s => s.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(s => s.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(s => s.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(s => s.EmployeeContributionRate).HasColumnName("EMPLOYEE_CONTRIB_RATE").HasColumnType("NUMBER(10,6)").HasDefaultValue(0.0750m);
        builder.Property(s => s.EmployerContributionRate).HasColumnName("EMPLOYER_CONTRIB_RATE").HasColumnType("NUMBER(10,6)").HasDefaultValue(0.1425m);
        builder.Property(s => s.HighRiskSurchargeRate).HasColumnName("HIGH_RISK_SURCHARGE_RATE").HasColumnType("NUMBER(10,6)").HasDefaultValue(0.0100m);
        builder.Property(s => s.MonthlyCeilingCap).HasColumnName("MONTHLY_CEILING_CAP").HasColumnType("NUMBER(18,4)").HasDefaultValue(3349.0m);
        builder.Property(s => s.MinimumWageFloor).HasColumnName("MINIMUM_WAGE_FLOOR").HasColumnType("NUMBER(18,4)").HasDefaultValue(290.0m);
        builder.Property(s => s.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(s => s.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(s => s.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(s => s.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(s => s.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(s => new { s.CompanyId, s.Code, s.EffectiveFrom }).IsUnique();
    }
}
