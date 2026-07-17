using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysDocumentConfiguration : IEntityTypeConfiguration<SysDocument>
{
    public void Configure(EntityTypeBuilder<SysDocument> builder)
    {
        builder.ToTable("SYS_DOCUMENT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.FileName).HasColumnName("FILE_NAME").HasMaxLength(255).IsRequired();
        builder.Property(e => e.FileSize).HasColumnName("FILE_SIZE").IsRequired();
        builder.Property(e => e.MimeType).HasColumnName("MIME_TYPE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FileExtension).HasColumnName("FILE_EXTENSION").HasMaxLength(20).IsRequired();
        builder.Property(e => e.FilePath).HasColumnName("FILE_PATH").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(1000);
        builder.Property(e => e.Category).HasColumnName("CATEGORY").HasMaxLength(50);
        builder.Property(e => e.Tags).HasColumnName("TAGS").HasMaxLength(500);
        builder.Property(e => e.OwnerType).HasColumnName("OWNER_TYPE").IsRequired();
        builder.Property(e => e.OwnerId).HasColumnName("OWNER_ID").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasMaxLength(1).IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(e => new { e.OwnerType, e.OwnerId, e.IsActive }).HasDatabaseName("IX_DOC_OWNER");
        builder.HasIndex(e => e.Category).HasDatabaseName("IX_DOC_CATEGORY");
    }
}
