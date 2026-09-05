using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class ExpenseClaimLineConfiguration : IEntityTypeConfiguration<ExpenseClaimLine>
{
    public void Configure(EntityTypeBuilder<ExpenseClaimLine> builder)
    {
        builder.ToTable("HR_EXPENSE_CLAIM_LINE");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(l => l.ClaimId)
            .HasColumnName("CLAIM_ID")
            .IsRequired();

        builder.Property(l => l.ExpenseDate)
            .HasColumnName("EXPENSE_DATE")
            .IsRequired();

        builder.Property(l => l.Category)
            .HasColumnName("CATEGORY")
            .HasMaxLength(50)
            .HasDefaultValue("TRAVEL")
            .IsRequired();

        builder.Property(l => l.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(l => l.Amount)
            .HasColumnName("AMOUNT")
            .HasColumnType("NUMBER(18,4)")
            .IsRequired();

        builder.Property(l => l.ReceiptFileReference)
            .HasColumnName("RECEIPT_FILE_REF")
            .HasMaxLength(500);

        builder.Property(l => l.ExpenseGlAccountCode)
            .HasColumnName("EXPENSE_GL_ACC_CODE")
            .HasMaxLength(50);

        builder.Property(l => l.CostCenterCode)
            .HasColumnName("COST_CENTER_CODE")
            .HasMaxLength(50);

        builder.HasOne(l => l.Claim)
            .WithMany(c => c.Lines)
            .HasForeignKey(l => l.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
