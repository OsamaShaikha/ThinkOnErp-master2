using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.ToTable("HR_PUBLIC_HOLIDAY");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(h => h.BranchId).HasColumnName("BRANCH_ID");
        builder.Property(h => h.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(h => h.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(h => h.HolidayDate).HasColumnName("HOLIDAY_DATE").IsRequired();
        builder.Property(h => h.IsPaid).HasColumnName("IS_PAID").HasDefaultValue(true);
        builder.Property(h => h.IsRecurring).HasColumnName("IS_RECURRING").HasDefaultValue(false);
        builder.Property(h => h.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(h => h.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(h => h.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(h => h.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(h => h.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(h => h.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(h => new { h.CompanyId, h.HolidayDate });
    }
}
