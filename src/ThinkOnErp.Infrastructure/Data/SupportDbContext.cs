using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Dedicated EF Core DbContext mapped strictly to the centralized "THINKON_SUPPORT" database schema.
/// Provides centralized management for support tickets, comments, attachments, SLAs, and categories across all tenants.
/// </summary>
public class SupportDbContext : DbContext
{
    public const string SchemaName = "THINKON_SUPPORT";

    public SupportDbContext(DbContextOptions<SupportDbContext> options)
        : base(options)
    {
    }

    public DbSet<SysRequestTicket> SysRequestTickets => Set<SysRequestTicket>();
    public DbSet<SysTicketType> SysTicketTypes => Set<SysTicketType>();
    public DbSet<SysTicketPriority> SysTicketPriorities => Set<SysTicketPriority>();
    public DbSet<SysTicketStatus> SysTicketStatuses => Set<SysTicketStatus>();
    public DbSet<SysTicketCategory> SysTicketCategories => Set<SysTicketCategory>();
    public DbSet<SysTicketComment> SysTicketComments => Set<SysTicketComment>();
    public DbSet<SysTicketAttachment> SysTicketAttachments => Set<SysTicketAttachment>();
    public DbSet<SysTicketConfig> SysTicketConfigs => Set<SysTicketConfig>();

    // Core reference entities for navigations (via Synonyms)
    public DbSet<SysCompany> SysCompanies => Set<SysCompany>();
    public DbSet<SysBranch> SysBranches => Set<SysBranch>();
    public DbSet<SysUser> SysUsers => Set<SysUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply dedicated schema to all support entities
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.ApplyConfiguration(new SysCompanyConfiguration());
        modelBuilder.ApplyConfiguration(new SysBranchConfiguration());
        modelBuilder.ApplyConfiguration(new SysUserConfiguration());

        modelBuilder.ApplyConfiguration(new SysTicketTypeConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketPriorityConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketStatusConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketCommentConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new SysRequestTicketConfiguration());
        modelBuilder.ApplyConfiguration(new SysTicketConfigConfiguration());
    }
}
