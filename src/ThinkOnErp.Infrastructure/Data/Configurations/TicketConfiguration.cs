using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for SysRequestTicket entity.
/// Maps to SYS_REQUEST_TICKET table in Oracle database.
/// </summary>
public class TicketConfiguration : IEntityTypeConfiguration<SysRequestTicket>
{
    public void Configure(EntityTypeBuilder<SysRequestTicket> builder)
    {
        // Table mapping
        builder.ToTable("SYS_REQUEST_TICKET");

        // Primary key
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_REQUEST_TICKET.NEXTVAL")
            .ValueGeneratedOnAdd();

        // Required properties
        builder.Property(e => e.TitleAr)
            .HasColumnName("TITLE_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.TitleEn)
            .HasColumnName("TITLE_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(e => e.CompanyId)
            .HasColumnName("COMPANY_ID")
            .IsRequired();

        builder.Property(e => e.BranchId)
            .HasColumnName("BRANCH_ID")
            .IsRequired();

        builder.Property(e => e.RequesterId)
            .HasColumnName("REQUESTER_ID")
            .IsRequired();

        builder.Property(e => e.TicketTypeId)
            .HasColumnName("TICKET_TYPE_ID")
            .IsRequired();

        builder.Property(e => e.TicketStatusId)
            .HasColumnName("TICKET_STATUS_ID")
            .IsRequired();

        builder.Property(e => e.TicketPriorityId)
            .HasColumnName("TICKET_PRIORITY_ID")
            .IsRequired();

        // Optional properties
        builder.Property(e => e.AssigneeId)
            .HasColumnName("ASSIGNEE_ID");

        builder.Property(e => e.TicketCategoryId)
            .HasColumnName("TICKET_CATEGORY_ID");

        builder.Property(e => e.ExpectedResolutionDate)
            .HasColumnName("EXPECTED_RESOLUTION_DATE");

        builder.Property(e => e.ActualResolutionDate)
            .HasColumnName("ACTUAL_RESOLUTION_DATE");

        // Audit properties
        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreationDate)
            .HasColumnName("CREATION_DATE");

        builder.Property(e => e.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(e => e.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        // Foreign key relationships
        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("FK_TICKET_COMPANY")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .HasConstraintName("FK_TICKET_BRANCH")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Requester)
            .WithMany()
            .HasForeignKey(e => e.RequesterId)
            .HasConstraintName("FK_TICKET_REQUESTER")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Assignee)
            .WithMany()
            .HasForeignKey(e => e.AssigneeId)
            .HasConstraintName("FK_TICKET_ASSIGNEE")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TicketType)
            .WithMany(t => t.Tickets)
            .HasForeignKey(e => e.TicketTypeId)
            .HasConstraintName("FK_TICKET_TYPE")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TicketStatus)
            .WithMany(s => s.Tickets)
            .HasForeignKey(e => e.TicketStatusId)
            .HasConstraintName("FK_TICKET_STATUS")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TicketPriority)
            .WithMany(p => p.Tickets)
            .HasForeignKey(e => e.TicketPriorityId)
            .HasConstraintName("FK_TICKET_PRIORITY")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TicketCategory)
            .WithMany(c => c.Tickets)
            .HasForeignKey(e => e.TicketCategoryId)
            .HasConstraintName("FK_TICKET_CATEGORY")
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many relationships
        builder.HasMany(e => e.Comments)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Attachments)
            .WithOne(a => a.Ticket)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);

        // Indexes for performance
        builder.HasIndex(e => e.CompanyId)
            .HasDatabaseName("IDX_TICKET_COMPANY");

        builder.HasIndex(e => e.BranchId)
            .HasDatabaseName("IDX_TICKET_BRANCH");

        builder.HasIndex(e => e.RequesterId)
            .HasDatabaseName("IDX_TICKET_REQUESTER");

        builder.HasIndex(e => e.AssigneeId)
            .HasDatabaseName("IDX_TICKET_ASSIGNEE");

        builder.HasIndex(e => e.TicketStatusId)
            .HasDatabaseName("IDX_TICKET_STATUS");

        builder.HasIndex(e => e.TicketPriorityId)
            .HasDatabaseName("IDX_TICKET_PRIORITY");

        builder.HasIndex(e => e.CreationDate)
            .HasDatabaseName("IDX_TICKET_CREATION_DATE");

        // Ignore computed properties
        builder.Ignore(e => e.IsOverdue);
        builder.Ignore(e => e.IsResolved);
        builder.Ignore(e => e.AgeInHours);
        builder.Ignore(e => e.IsWithinSla);
    }
}
