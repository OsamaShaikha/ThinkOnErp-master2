using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysTicketComment entity.
/// Maps to SYS_TICKET_COMMENT table in Oracle database.
/// </summary>
public class TicketCommentConfiguration : IEntityTypeConfiguration<SysTicketComment>
{
    public void Configure(EntityTypeBuilder<SysTicketComment> builder)
    {
        // Table mapping
        builder.ToTable("SYS_TICKET_COMMENT");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_TICKET_COMMENT.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.TicketId)
            .HasColumnName("TICKET_ID")
            .IsRequired();

        builder.Property(e => e.CommentText)
            .HasColumnName("COMMENT_TEXT")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.IsInternal)
            .HasColumnName("IS_INTERNAL")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
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
            .WithMany(t => t.Comments)
            .HasForeignKey(e => e.TicketId)
            .HasConstraintName("FK_TICKET_COMMENT_TICKET")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(e => e.TicketId)
            .HasDatabaseName("IDX_TICKET_COMMENT_TICKET");

        builder.HasIndex(e => e.CreationDate)
            .HasDatabaseName("IDX_TICKET_COMMENT_CREATION_DATE");

        // Ignore computed properties
        builder.Ignore(e => e.IsVisibleToRequester);
        builder.Ignore(e => e.IsAdminOnly);
        builder.Ignore(e => e.AgeInHours);
    }
}
