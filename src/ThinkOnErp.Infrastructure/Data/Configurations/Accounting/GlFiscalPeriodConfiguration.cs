using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public sealed class GlFiscalPeriodConfiguration : IEntityTypeConfiguration<GlFiscalPeriod>
{
    public void Configure(EntityTypeBuilder<GlFiscalPeriod> builder)
    {
        builder.ToTable("GL_FISCAL_PERIOD");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.FiscalYearId)
            .HasColumnName("FISCAL_YEAR_ID")
            .IsRequired();

        builder.Property(e => e.PeriodNumber)
            .HasColumnName("PERIOD_NUMBER")
            .IsRequired();

        builder.Property(e => e.PeriodNameAr)
            .HasColumnName("PERIOD_NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.PeriodNameEn)
            .HasColumnName("PERIOD_NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.StartDate)
            .HasColumnName("START_DATE")
            .IsRequired();

        builder.Property(e => e.EndDate)
            .HasColumnName("END_DATE")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(20)
            .HasDefaultValue("OPEN")
            .IsRequired();

        builder.Property(e => e.IsAdjustment)
            .HasColumnName("IS_ADJUSTMENT")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(false);

        builder.Property(e => e.CloseReason)
            .HasColumnName("CLOSE_REASON")
            .HasMaxLength(500);

        builder.Property(e => e.ClosedBy)
            .HasColumnName("CLOSED_BY")
            .HasMaxLength(100);

        builder.Property(e => e.ClosedDate)
            .HasColumnName("CLOSED_DATE");

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

        builder.HasIndex(e => new { e.FiscalYearId, e.PeriodNumber })
            .IsUnique()
            .HasDatabaseName("UX_GL_FP_YEAR_NUM");

        builder.HasOne(e => e.FiscalYear)
            .WithMany(y => y.Periods)
            .HasForeignKey(e => e.FiscalYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
