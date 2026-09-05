using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

public sealed class SysEntityTranslationConfiguration : IEntityTypeConfiguration<SysEntityTranslation>
{
    public void Configure(EntityTypeBuilder<SysEntityTranslation> builder)
    {
        builder.ToTable("SYS_ENTITY_TRANSLATION");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.EntityType)
            .HasColumnName("ENTITY_TYPE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasColumnName("ENTITY_ID")
            .IsRequired();

        builder.Property(e => e.FieldName)
            .HasColumnName("FIELD_NAME")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.LangCode)
            .HasColumnName("LANG_CODE")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.TranslationText)
            .HasColumnName("TRANSLATION_TEXT")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.FieldName, e.LangCode })
            .IsUnique();

        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.LangCode });
    }
}
