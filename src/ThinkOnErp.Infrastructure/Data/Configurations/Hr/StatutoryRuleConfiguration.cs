using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class StatutoryRuleConfiguration : IEntityTypeConfiguration<StatutoryRule>
{
    public void Configure(EntityTypeBuilder<StatutoryRule> builder)
    {
        builder.ToTable("HR_STATUTORY_RULE");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.RuleType)
            .HasColumnName("RULE_TYPE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.RuleName)
            .HasColumnName("RULE_NAME")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.BracketLow)
            .HasColumnName("BRACKET_LOW")
            .HasColumnType("NUMBER(18,4)");

        builder.Property(r => r.BracketHigh)
            .HasColumnName("BRACKET_HIGH")
            .HasColumnType("NUMBER(18,4)");

        builder.Property(r => r.RatePercent)
            .HasColumnName("RATE_PERCENT")
            .HasColumnType("NUMBER(10,6)");

        builder.Property(r => r.Value)
            .HasColumnName("VALUE")
            .HasColumnType("NUMBER(18,4)");

        builder.Property(r => r.CurrencyCode)
            .HasColumnName("CURRENCY_CODE")
            .HasMaxLength(10)
            .HasDefaultValue("JOD")
            .IsRequired();

        builder.Property(r => r.EffectiveFrom)
            .HasColumnName("EFFECTIVE_FROM")
            .IsRequired();

        builder.Property(r => r.EffectiveTo)
            .HasColumnName("EFFECTIVE_TO");

        builder.Property(r => r.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(r => r.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(r => r.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(r => r.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(r => r.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(r => new { r.RuleType, r.EffectiveFrom, r.EffectiveTo })
            .HasDatabaseName("IX_HR_STAT_RULE_TYPE_EFF");
    }
}
