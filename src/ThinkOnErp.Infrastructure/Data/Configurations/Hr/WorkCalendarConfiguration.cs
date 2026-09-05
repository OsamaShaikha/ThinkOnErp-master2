using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class WorkCalendarConfiguration : IEntityTypeConfiguration<WorkCalendar>
{
    public void Configure(EntityTypeBuilder<WorkCalendar> builder)
    {
        builder.ToTable("HR_WORK_CALENDAR");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        builder.Property(c => c.Code).HasColumnName("CODE").HasMaxLength(50).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("NAME_EN").HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameAr).HasColumnName("NAME_AR").HasMaxLength(200).IsRequired();
        builder.Property(c => c.IsDefault).HasColumnName("IS_DEFAULT").HasDefaultValue(false);
        builder.Property(c => c.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
        builder.Property(c => c.EffectiveFrom).HasColumnName("EFFECTIVE_FROM").IsRequired();
        builder.Property(c => c.EffectiveTo).HasColumnName("EFFECTIVE_TO");
        builder.Property(c => c.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreationDate).HasColumnName("CREATION_DATE").IsRequired();
        builder.Property(c => c.UpdateUser).HasColumnName("UPDATE_USER").HasMaxLength(100);
        builder.Property(c => c.UpdateDate).HasColumnName("UPDATE_DATE");

        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
        builder.HasMany(c => c.Days).WithOne(d => d.WorkCalendar).HasForeignKey(d => d.WorkCalendarId).OnDelete(DeleteBehavior.Cascade);
    }
}
