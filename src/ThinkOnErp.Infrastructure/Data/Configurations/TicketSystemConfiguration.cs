using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

public class SysTicketTypeConfiguration : IEntityTypeConfiguration<SysTicketType>
{
    public void Configure(EntityTypeBuilder<SysTicketType> builder)
    {
        builder.ToTable("SYS_TICKET_TYPE", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.TypeNameAr).HasColumnName("TYPE_NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(e => e.TypeNameEn).HasColumnName("TYPE_NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.DescriptionAr).HasColumnName("DESCRIPTION_AR").HasMaxLength(500);
        builder.Property(e => e.DescriptionEn).HasColumnName("DESCRIPTION_EN").HasMaxLength(500);
        builder.Property(e => e.DefaultPriorityId).HasColumnName("DEFAULT_PRIORITY_ID").IsRequired();
        builder.Property(e => e.SlaTargetHours).HasColumnName("SLA_TARGET_HOURS").HasColumnType("NUMBER");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.DefaultPriority).WithMany(p => p.TicketTypesWithDefaultPriority).HasForeignKey(e => e.DefaultPriorityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SysTicketPriorityConfiguration : IEntityTypeConfiguration<SysTicketPriority>
{
    public void Configure(EntityTypeBuilder<SysTicketPriority> builder)
    {
        builder.ToTable("SYS_TICKET_PRIORITY", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.PriorityNameAr).HasColumnName("PRIORITY_NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(e => e.PriorityNameEn).HasColumnName("PRIORITY_NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.PriorityLevel).HasColumnName("PRIORITY_LEVEL").IsRequired();
        builder.Property(e => e.SlaTargetHours).HasColumnName("SLA_TARGET_HOURS").HasColumnType("NUMBER");
        builder.Property(e => e.EscalationThresholdHours).HasColumnName("ESCALATION_THRESHOLD_HOURS").HasColumnType("NUMBER");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
    }
}

public class SysTicketStatusConfiguration : IEntityTypeConfiguration<SysTicketStatus>
{
    public void Configure(EntityTypeBuilder<SysTicketStatus> builder)
    {
        builder.ToTable("SYS_TICKET_STATUS", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.StatusNameAr).HasColumnName("STATUS_NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(e => e.StatusNameEn).HasColumnName("STATUS_NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.StatusCode).HasColumnName("STATUS_CODE").HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.StatusCode).IsUnique();
        builder.Property(e => e.DisplayOrder).HasColumnName("DISPLAY_ORDER");
        builder.Property(e => e.IsFinalStatus).HasColumnName("IS_FINAL_STATUS").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
    }
}

public class SysTicketCategoryConfiguration : IEntityTypeConfiguration<SysTicketCategory>
{
    public void Configure(EntityTypeBuilder<SysTicketCategory> builder)
    {
        builder.ToTable("SYS_TICKET_CATEGORY", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.CategoryNameAr).HasColumnName("CATEGORY_NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CategoryNameEn).HasColumnName("CATEGORY_NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.DescriptionAr).HasColumnName("DESCRIPTION_AR").HasMaxLength(500);
        builder.Property(e => e.DescriptionEn).HasColumnName("DESCRIPTION_EN").HasMaxLength(500);
        builder.Property(e => e.DisplayOrder).HasColumnName("DISPLAY_ORDER");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysTicketCommentConfiguration : IEntityTypeConfiguration<SysTicketComment>
{
    public void Configure(EntityTypeBuilder<SysTicketComment> builder)
    {
        builder.ToTable("SYS_TICKET_COMMENT", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.TicketId).HasColumnName("TICKET_ID").IsRequired();
        builder.Property(e => e.CommentText).HasColumnName("COMMENT_TEXT").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.IsInternal).HasColumnName("IS_INTERNAL").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");

        builder.HasOne(e => e.Ticket).WithMany(t => t.Comments).HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SysTicketAttachmentConfiguration : IEntityTypeConfiguration<SysTicketAttachment>
{
    public void Configure(EntityTypeBuilder<SysTicketAttachment> builder)
    {
        builder.ToTable("SYS_TICKET_ATTACHMENT", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.TicketId).HasColumnName("TICKET_ID").IsRequired();
        builder.Property(e => e.FileName).HasColumnName("FILE_NAME").HasMaxLength(255).IsRequired();
        builder.Property(e => e.FileContent).HasColumnName("FILE_CONTENT").HasColumnType("CLOB").IsRequired();
        builder.Property(e => e.MimeType).HasColumnName("MIME_TYPE").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FileSize).HasColumnName("FILE_SIZE");
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");

        builder.HasOne(e => e.Ticket).WithMany(t => t.Attachments).HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SysRequestTicketConfiguration : IEntityTypeConfiguration<SysRequestTicket>
{
    public void Configure(EntityTypeBuilder<SysRequestTicket> builder)
    {
        builder.ToTable("SYS_REQUEST_TICKET", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.TitleAr).HasColumnName("TITLE_AR").HasMaxLength(200).IsRequired();
        builder.Property(e => e.TitleEn).HasColumnName("TITLE_EN").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(5000).IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(e => e.RequesterId).HasColumnName("REQUESTER_ID").IsRequired();
        builder.Property(e => e.AssigneeId).HasColumnName("ASSIGNEE_ID");
        builder.Property(e => e.TicketTypeId).HasColumnName("TICKET_TYPE_ID").IsRequired();
        builder.Property(e => e.TicketStatusId).HasColumnName("TICKET_STATUS_ID").IsRequired();
        builder.Property(e => e.TicketPriorityId).HasColumnName("TICKET_PRIORITY_ID").IsRequired();
        builder.Property(e => e.TicketCategoryId).HasColumnName("TICKET_CATEGORY_ID");
        builder.Property(e => e.ExpectedResolutionDate).HasColumnName("EXPECTED_RESOLUTION_DATE");
        builder.Property(e => e.ActualResolutionDate).HasColumnName("ACTUAL_RESOLUTION_DATE");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Requester).WithMany().HasForeignKey(e => e.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Assignee).WithMany().HasForeignKey(e => e.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.TicketType).WithMany(t => t.Tickets).HasForeignKey(e => e.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TicketStatus).WithMany(s => s.Tickets).HasForeignKey(e => e.TicketStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TicketPriority).WithMany(p => p.Tickets).HasForeignKey(e => e.TicketPriorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TicketCategory).WithMany(c => c.Tickets).HasForeignKey(e => e.TicketCategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.Comments).WithOne(c => c.Ticket).HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Attachments).WithOne(a => a.Ticket).HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
        
        builder.Ignore(e => e.IsOverdue);
        builder.Ignore(e => e.IsResolved);
        builder.Ignore(e => e.AgeInHours);
        builder.Ignore(e => e.IsWithinSla);
    }
}

public class SysTicketConfigConfiguration : IEntityTypeConfiguration<SysTicketConfig>
{
    public void Configure(EntityTypeBuilder<SysTicketConfig> builder)
    {
        builder.ToTable("SYS_TICKET_CONFIG", "THINKON_SUPPORT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.ConfigKey).HasColumnName("CONFIG_KEY").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.ConfigKey).IsUnique();
        builder.Property(e => e.ConfigValue).HasColumnName("CONFIG_VALUE").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.DescriptionAr).HasColumnName("DESCRIPTION_AR").HasMaxLength(500);
        builder.Property(e => e.DescriptionEn).HasColumnName("DESCRIPTION_EN").HasMaxLength(500);
        builder.Property(e => e.ConfigType).HasColumnName("CONFIG_TYPE").HasMaxLength(50).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysSavedSearchConfiguration : IEntityTypeConfiguration<SysSavedSearch>
{
    public void Configure(EntityTypeBuilder<SysSavedSearch> builder)
    {
        builder.ToTable("SYS_SAVED_SEARCH", t => t.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.SearchName).HasColumnName("SEARCH_NAME").HasMaxLength(200).IsRequired();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(e => e.SearchCriteria).HasColumnName("SEARCH_CRITERIA").HasColumnType("CLOB").IsRequired();
        builder.Property(e => e.SearchDescription).HasColumnName("SEARCH_DESCRIPTION").HasMaxLength(500);
        builder.Property(e => e.IsPublic).HasColumnName("IS_PUBLIC").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.IsDefault).HasColumnName("IS_DEFAULT").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.UsageCount).HasColumnName("USAGE_COUNT");
        builder.Property(e => e.LastUsedDate).HasColumnName("LAST_USED_DATE");
        builder.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").IsRequired();
        builder.Property(e => e.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreationDate).HasColumnName("CREATION_DATE");
        builder.Property(e => e.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(e => e.UpdateDate).HasColumnName("UPDATE_DATE");
    }
}

public class SysSearchAnalyticsConfiguration : IEntityTypeConfiguration<SysSearchAnalytics>
{
    public void Configure(EntityTypeBuilder<SysSearchAnalytics> builder)
    {
        builder.ToTable("SYS_SEARCH_ANALYTICS", t => t.ExcludeFromMigrations());
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("USER_ID").IsRequired();
        builder.Property(e => e.SearchTerm).HasColumnName("SEARCH_TERM").HasMaxLength(500);
        builder.Property(e => e.SearchCriteria).HasColumnName("SEARCH_CRITERIA").HasColumnType("CLOB");
        builder.Property(e => e.FilterLogic).HasColumnName("FILTER_LOGIC").HasMaxLength(10);
        builder.Property(e => e.ResultCount).HasColumnName("RESULT_COUNT");
        builder.Property(e => e.ExecutionTimeMs).HasColumnName("EXECUTION_TIME_MS");
        builder.Property(e => e.SearchDate).HasColumnName("SEARCH_DATE");
        builder.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID");
    }
}