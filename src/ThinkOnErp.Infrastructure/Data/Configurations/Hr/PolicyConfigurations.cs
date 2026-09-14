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
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(100).IsRequired();
        builder.Property(r => r.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(r => r.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(r => r.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(r => r.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(r => r.DayType).HasColumnName("DAY_TYPE").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Multiplier).HasColumnName("MULTIPLIER").HasPrecision(5, 2).IsRequired();
        builder.Property(r => r.MinimumMinutes).HasColumnName("MINIMUM_MINUTES").IsRequired();
        builder.Property(r => r.MaximumMinutes).HasColumnName("MAXIMUM_MINUTES").IsRequired();
        builder.Property(r => r.HourlyDivisorFormula).HasColumnName("HOURLY_DIVISOR_FORMULA").HasMaxLength(100).IsRequired();
        builder.Property(r => r.RequiresApproval).HasColumnName("REQUIRES_APPROVAL").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(r => r.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(r => r.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class ProrationPolicyConfiguration : IEntityTypeConfiguration<ProrationPolicy>
{
    public void Configure(EntityTypeBuilder<ProrationPolicy> builder)
    {
        builder.ToTable("HR_PRORATION_POLICY");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(p => p.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(p => p.Code).HasColumnName("CODE").HasMaxLength(100).IsRequired();
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(p => p.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(p => p.Method).HasColumnName("METHOD").HasMaxLength(100).IsRequired();
        builder.Property(p => p.IsDefault).HasColumnName("IS_DEFAULT").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class DeductionPolicyConfiguration : IEntityTypeConfiguration<DeductionPolicy>
{
    public void Configure(EntityTypeBuilder<DeductionPolicy> builder)
    {
        builder.ToTable("HR_DEDUCTION_POLICY");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(p => p.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(p => p.Code).HasColumnName("CODE").HasMaxLength(100).IsRequired();
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(p => p.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(p => p.MaxDeductionPercentage).HasColumnName("MAX_DEDUCTION_PERCENTAGE").HasPrecision(5, 2).IsRequired();
        builder.Property(p => p.MinNetPayGuarantee).HasColumnName("MIN_NET_PAY_GUARANTEE").HasPrecision(18, 4).IsRequired();
        builder.Property(p => p.AutoCapAndCarryForward).HasColumnName("AUTO_CAP_AND_CARRY_FORWARD").IsRequired();
        builder.Property(p => p.AllowNegativeNetPay).HasColumnName("ALLOW_NEGATIVE_NET_PAY").IsRequired();
        builder.Property(p => p.IsDefault).HasColumnName("IS_DEFAULT").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public sealed class TaxPolicyConfiguration : IEntityTypeConfiguration<TaxPolicy>
{
    public void Configure(EntityTypeBuilder<TaxPolicy> builder)
    {
        builder.ToTable("HR_TAX_POLICY");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(t => t.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(t => t.Code).HasColumnName("CODE").HasMaxLength(100).IsRequired();
        builder.Property(t => t.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(t => t.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(t => t.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(t => t.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(t => t.PersonalExemptionSelf).HasColumnName("PERSONAL_EXEMPTION_SELF").HasPrecision(18, 4).IsRequired();
        builder.Property(t => t.PersonalExemptionDependent).HasColumnName("PERSONAL_EXEMPTION_DEPENDENT").HasPrecision(18, 4).IsRequired();
        builder.Property(t => t.NationalContribThreshold).HasColumnName("NATIONAL_CONTRIB_THRESHOLD").HasPrecision(18, 4).IsRequired();
        builder.Property(t => t.NationalContribRate).HasColumnName("NATIONAL_CONTRIB_RATE").HasPrecision(10, 6).IsRequired();
        builder.Property(t => t.IsSscTaxDeductible).HasColumnName("IS_SSC_TAX_DEDUCTIBLE").IsRequired();
        builder.Property(t => t.CalculationFrequency).HasColumnName("CALCULATION_FREQUENCY").HasMaxLength(100).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(t => t.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(t => t.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(t => t.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(t => t.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasMany(t => t.Brackets)
            .WithOne(b => b.TaxPolicy)
            .HasForeignKey(b => b.TaxPolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaxBracketConfiguration : IEntityTypeConfiguration<TaxBracket>
{
    public void Configure(EntityTypeBuilder<TaxBracket> builder)
    {
        builder.ToTable("HR_TAX_BRACKET");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(b => b.TaxPolicyId).HasColumnName("TAX_POLICY_ID").IsRequired();
        builder.Property(b => b.BracketOrder).HasColumnName("BRACKET_ORDER").IsRequired();
        builder.Property(b => b.LowerLimit).HasColumnName("LOWER_LIMIT").HasPrecision(18, 4).IsRequired();
        builder.Property(b => b.UpperLimit).HasColumnName("UPPER_LIMIT").HasPrecision(18, 4);
        builder.Property(b => b.RatePercent).HasColumnName("RATE_PERCENT").HasPrecision(10, 6).IsRequired();
        builder.Property(b => b.Description).HasColumnName("DESCRIPTION").HasMaxLength(400);
    }
}

public sealed class SSCPolicyConfiguration : IEntityTypeConfiguration<SSCPolicy>
{
    public void Configure(EntityTypeBuilder<SSCPolicy> builder)
    {
        builder.ToTable("HR_SSC_POLICY");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(s => s.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(s => s.Code).HasColumnName("CODE").HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameAr).HasColumnName("NAME_AR").HasMaxLength(400).IsRequired();
        builder.Property(s => s.NameEn).HasColumnName("NAME_EN").HasMaxLength(400).IsRequired();
        builder.Property(s => s.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(s => s.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(s => s.EmployeeContribRate).HasColumnName("EMPLOYEE_CONTRIB_RATE").HasPrecision(10, 6).IsRequired();
        builder.Property(s => s.EmployerContribRate).HasColumnName("EMPLOYER_CONTRIB_RATE").HasPrecision(10, 6).IsRequired();
        builder.Property(s => s.HighRiskSurchargeRate).HasColumnName("HIGH_RISK_SURCHARGE_RATE").HasPrecision(10, 6).IsRequired();
        builder.Property(s => s.MonthlyCeilingCap).HasColumnName("MONTHLY_CEILING_CAP").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.MinimumWageFloor).HasColumnName("MINIMUM_WAGE_FLOOR").HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("IS_ACTIVE").IsRequired();

        builder.Property(s => s.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        builder.Property(s => s.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(s => s.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        builder.Property(s => s.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}
