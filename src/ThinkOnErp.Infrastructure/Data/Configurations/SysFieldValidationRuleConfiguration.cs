using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysFieldValidationRuleConfiguration : IEntityTypeConfiguration<SysFieldValidationRule>
{
    public void Configure(EntityTypeBuilder<SysFieldValidationRule> builder)
    {
        builder.ToTable("SYS_FIELD_VALIDATION_RULE");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(e => e.EntityName).HasColumnName("ENTITY_NAME").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FieldName).HasColumnName("FIELD_NAME").HasMaxLength(100).IsRequired();
        builder.Property(e => e.RuleType).HasColumnName("RULE_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.RuleValue).HasColumnName("RULE_VALUE").HasMaxLength(500);
        builder.Property(e => e.ErrorCode).HasColumnName("ERROR_CODE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CountryCode).HasColumnName("COUNTRY_CODE").HasMaxLength(10);
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(1).IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => new { e.EntityName, e.CountryCode, e.CompanyId, e.IsActive })
            .HasDatabaseName("IX_VAL_RULE_LOOKUP");
    }
}
