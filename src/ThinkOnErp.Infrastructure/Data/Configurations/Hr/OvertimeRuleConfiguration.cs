using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class OvertimeRuleConfiguration : IEntityTypeConfiguration<OvertimeRule>
{
    public void Configure(EntityTypeBuilder<OvertimeRule> builder)
    {
        builder.ToTable("HR_OVERTIME_RULE");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(r => r.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(r => r.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(r => r.DayType).HasColumnName("DAY_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(r => r.Multiplier).HasColumnName("MULTIPLIER").HasColumnType("NUMBER(5,2)").IsRequired();
        builder.Property(r => r.MinimumMinutes).HasColumnName("MINIMUM_MINUTES").HasDefaultValue(30);
        builder.Property(r => r.MaximumMinutes).HasColumnName("MAXIMUM_MINUTES").HasDefaultValue(480);
        builder.Property(r => r.HourlyDivisorFormula).HasColumnName("HOURLY_DIVISOR_FORMULA").HasMaxLength(50).HasDefaultValue("FIXED_240");
        builder.Property(r => r.RequiresApproval).HasColumnName("REQUIRES_APPROVAL").HasDefaultValue(true);
        builder.Property(r => r.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(r => r.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(r => r.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(r => r.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(r => r.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(r => new { r.CompanyId, r.Code, r.EffectiveFrom }).IsUnique();
    }
}
