using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PayrollRunLineComponentConfiguration : IEntityTypeConfiguration<PayrollRunLineComponent>
{
    public void Configure(EntityTypeBuilder<PayrollRunLineComponent> builder)
    {
        builder.ToTable("HR_PAYROLL_LINE_COMP");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.PayrollRunLineId)
            .HasColumnName("PAYROLL_RUN_LINE_ID")
            .IsRequired();

        builder.Property(c => c.ComponentCode)
            .HasColumnName("COMPONENT_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ComponentNameEn)
            .HasColumnName("COMPONENT_NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.ComponentNameAr)
            .HasColumnName("COMPONENT_NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.ComponentType)
            .HasColumnName("COMPONENT_TYPE")
            .HasMaxLength(30)
            .HasDefaultValue("EARNING")
            .IsRequired();

        builder.Property(c => c.Amount)
            .HasColumnName("AMOUNT")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(c => c.GlAccountCode)
            .HasColumnName("GL_ACCOUNT_CODE")
            .HasMaxLength(50);

        builder.Property(c => c.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.HasOne(c => c.PayrollRunLine)
            .WithMany(l => l.Components)
            .HasForeignKey(c => c.PayrollRunLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
