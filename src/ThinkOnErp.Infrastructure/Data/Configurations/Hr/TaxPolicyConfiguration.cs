using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class TaxPolicyConfiguration : IEntityTypeConfiguration<TaxPolicy>
{
    public void Configure(EntityTypeBuilder<TaxPolicy> builder)
    {
        builder.ToTable("HR_TAX_POLICY");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(p => p.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(p => p.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(p => p.CalculationFrequency).HasColumnName("CALCULATION_FREQUENCY").HasMaxLength(50).HasDefaultValue("ANNUALIZED_MONTHLY").IsRequired();
        builder.Property(p => p.PersonalExemptionSelf).HasColumnName("PERSONAL_EXEMPTION_SELF").HasColumnType("NUMBER(18,4)").HasDefaultValue(9000.0m);
        builder.Property(p => p.PersonalExemptionDependent).HasColumnName("PERSONAL_EXEMPTION_DEPENDENT").HasColumnType("NUMBER(18,4)").HasDefaultValue(9000.0m);
        builder.Property(p => p.NationalContributionThreshold).HasColumnName("NATIONAL_CONTRIB_THRESHOLD").HasColumnType("NUMBER(18,4)").HasDefaultValue(200000.0m);
        builder.Property(p => p.NationalContributionRate).HasColumnName("NATIONAL_CONTRIB_RATE").HasColumnType("NUMBER(10,6)").HasDefaultValue(0.010m);
        builder.Property(p => p.IsSscTaxDeductible).HasColumnName("IS_SSC_TAX_DEDUCTIBLE").HasDefaultValue(true);
        builder.Property(p => p.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(p => p.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(p => p.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(p => p.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(p => new { p.CompanyId, p.Code, p.EffectiveFrom }).IsUnique();
        builder.HasMany(p => p.Brackets).WithOne(b => b.TaxPolicy).HasForeignKey(b => b.TaxPolicyId).OnDelete(DeleteBehavior.Cascade);
    }
}
