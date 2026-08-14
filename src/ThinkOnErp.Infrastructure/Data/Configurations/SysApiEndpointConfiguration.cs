using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Data.Configurations;

public class SysApiEndpointConfiguration : IEntityTypeConfiguration<SysApiEndpoint>
{
    public void Configure(EntityTypeBuilder<SysApiEndpoint> builder)
    {
        builder.ToTable("SYS_API_ENDPOINTS");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CategoryCode)
            .HasColumnName("CATEGORY_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ControllerName)
            .HasColumnName("CONTROLLER_NAME")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ActionName)
            .HasColumnName("ACTION_NAME")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.HttpMethod)
            .HasColumnName("HTTP_METHOD")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.RoutePath)
            .HasColumnName("ROUTE_PATH")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(e => e.DisplayName)
            .HasColumnName("DISPLAY_NAME")
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.HttpMethod, e.RoutePath })
            .IsUnique()
            .HasDatabaseName("UX_SYS_API_ENDPOINTS_ROUTE");
    }
}
