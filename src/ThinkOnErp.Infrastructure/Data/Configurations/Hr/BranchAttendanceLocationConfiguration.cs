using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;
public sealed class BranchAttendanceLocationConfiguration : IEntityTypeConfiguration<BranchAttendanceLocation>
{
    public void Configure(EntityTypeBuilder<BranchAttendanceLocation> b)
    {
        b.ToTable("HR_BRANCH_ATTENDANCE_LOCATION", table => table.ExcludeFromMigrations()); b.HasKey(x => x.BranchId);
        b.Property(x => x.BranchId).HasColumnName("BRANCH_ID").ValueGeneratedNever();
        b.Property(x => x.CompanyId).HasColumnName("COMPANY_ID");
        b.Property(x => x.Latitude).HasColumnName("LATITUDE").HasPrecision(10, 7);
        b.Property(x => x.Longitude).HasColumnName("LONGITUDE").HasPrecision(11, 7);
        b.Property(x => x.RadiusMeters).HasColumnName("RADIUS_METERS").HasPrecision(10, 2);
        b.Property(x => x.EnforceGeofence).HasColumnName("ENFORCE_GEOFENCE").HasColumnType("NUMBER(1)");
        b.Property(x => x.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(200).IsRequired();
        b.Property(x => x.CreationDate).HasColumnName("CREATION_DATE");
        b.Property(x => x.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(200);
        b.Property(x => x.UpdateDate).HasColumnName("UPDATE_DATE");
        b.HasOne<SysBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_HR_BRANCH_LOCATION");
    }
}
