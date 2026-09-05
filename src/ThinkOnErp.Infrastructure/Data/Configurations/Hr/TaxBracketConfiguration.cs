using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class TaxBracketConfiguration : IEntityTypeConfiguration<TaxBracket>
{
    public void Configure(EntityTypeBuilder<TaxBracket> builder)
    {
        builder.ToTable("HR_TAX_BRACKET");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.TaxPolicyId).HasColumnName("TAX_POLICY_ID").IsRequired();
        builder.Property(b => b.BracketOrder).HasColumnName("BRACKET_ORDER").IsRequired();
        builder.Property(b => b.LowerLimit).HasColumnName("LOWER_LIMIT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(b => b.UpperLimit).HasColumnName("UPPER_LIMIT").HasColumnType("NUMBER(18,4)");
        builder.Property(b => b.RatePercent).HasColumnName("RATE_PERCENT").HasColumnType("NUMBER(10,6)").IsRequired();
        builder.Property(b => b.Description).HasColumnName("DESCRIPTION").HasMaxLength(200);

        builder.HasIndex(b => new { b.TaxPolicyId, b.BracketOrder }).IsUnique();
    }
}
