using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysTicketAttachment entity.
/// Maps to SYS_TICKET_ATTACHMENT table in Oracle database.
/// </summary>
public class TicketAttachmentConfiguration : IEntityTypeConfiguration<SysTicketAttachment>
{
    public void Configure(EntityTypeBuilder<SysTicketAttachment> builder)
    {
        // Table mapping
        builder.ToTable("SYS_TICKET_ATTACHMENT");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_TICKET_ATTACHMENT.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.TicketId)
            .HasColumnName("TICKET_ID")
            .IsRequired();

        builder.Property(e => e.FileName)
            .HasColumnName("FILE_NAME")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.FileSize)
            .HasColumnName("FILE_SIZE")
            .IsRequired();

        builder.Property(e => e.MimeType)
            .HasColumnName("MIME_TYPE")
            .HasMaxLength(100)
            .IsRequired();

        // BLOB property for file content
        builder.Property(e => e.FileContent)
            .HasColumnName("FILE_CONTENT")
            .HasColumnType("BLOB")
            .IsRequired();

        // Audit properties
        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        // Foreign key relationships
        builder.HasOne(e => e.Ticket)
            .WithMany(t => t.Attachments)
            .HasForeignKey(e => e.TicketId)
            .HasConstraintName("FK_TICKET_ATTACHMENT_TICKET")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(e => e.TicketId)
            .HasDatabaseName("IDX_TICKET_ATTACHMENT_TICKET");

        builder.HasIndex(e => e.CreationDate)
            .HasDatabaseName("IDX_TICKET_ATTACHMENT_CREATION_DATE");

        // Ignore computed properties
        builder.Ignore(e => e.FileExtension);
        builder.Ignore(e => e.IsFileSizeValid);
        builder.Ignore(e => e.IsFileExtensionValid);
        builder.Ignore(e => e.IsMimeTypeValid);
        builder.Ignore(e => e.IsValid);
    }
}
