using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Dedicated EF Core DbContext mapped strictly to the centralized "THINKON_AUDIT" database schema.
/// Provides isolated, high-throughput append-only persistence and fast indexing for all audit operations.
/// </summary>
public class AuditDbContext : DbContext
{
    public const string SchemaName = "THINKON_AUDIT";

    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<SysAuditLog> SysAuditLogs => Set<SysAuditLog>();
    public DbSet<SysAuditLogArchive> SysAuditLogArchives => Set<SysAuditLogArchive>();
    public DbSet<SysRetentionPolicy> SysRetentionPolicies => Set<SysRetentionPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply dedicated schema to all audit entities
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.ApplyConfiguration(new SysAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new SysAuditLogArchiveConfiguration());
        modelBuilder.ApplyConfiguration(new SysRetentionPolicyConfiguration());
    }
}
