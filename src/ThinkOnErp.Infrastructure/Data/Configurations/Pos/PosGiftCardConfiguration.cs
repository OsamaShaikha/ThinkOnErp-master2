using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosGiftCardConfiguration : IEntityTypeConfiguration<PosGiftCard>
{
    public void Configure(EntityTypeBuilder<PosGiftCard> builder)
    {
        builder.ToTable("POS_GIFT_CARD");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(g => g.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(g => g.CardCode).HasColumnName("CARD_CODE").HasMaxLength(50).IsRequired();
        builder.Property(g => g.PinHash).HasColumnName("PIN_HASH").HasMaxLength(256);
        builder.Property(g => g.InitialBalance).HasColumnName("INITIAL_BALANCE").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(g => g.CurrentBalance).HasColumnName("CURRENT_BALANCE").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(g => g.IssueDate).HasColumnName("ISSUE_DATE").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(g => g.ExpiryDate).HasColumnName("EXPIRY_DATE").HasColumnType("TIMESTAMP");
        builder.Property(g => g.IsActive).HasColumnName("IS_ACTIVE").HasColumnType("NUMBER(1)").HasDefaultValue(true);

        builder.Property(g => g.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(g => g.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(g => new { g.BranchId, g.CardCode }).IsUnique();
        builder.HasOne(g => g.Branch).WithMany().HasForeignKey(g => g.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PosGiftCardTransactionConfiguration : IEntityTypeConfiguration<PosGiftCardTransaction>
{
    public void Configure(EntityTypeBuilder<PosGiftCardTransaction> builder)
    {
        builder.ToTable("POS_GIFT_CARD_TRANSACTION");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.GiftCardId).HasColumnName("GIFT_CARD_ID").IsRequired();
        builder.Property(t => t.OrderId).HasColumnName("ORDER_ID");
        builder.Property(t => t.TransactionType).HasColumnName("TRANSACTION_TYPE").HasMaxLength(30).IsRequired();
        builder.Property(t => t.Amount).HasColumnName("AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(t => t.BalanceBefore).HasColumnName("BALANCE_BEFORE").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(t => t.BalanceAfter).HasColumnName("BALANCE_AFTER").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(t => t.TransactionDate).HasColumnName("TRANSACTION_DATE").HasColumnType("TIMESTAMP");
        builder.Property(t => t.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();

        builder.HasOne(t => t.GiftCard).WithMany(g => g.Transactions).HasForeignKey(t => t.GiftCardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.Order).WithMany().HasForeignKey(t => t.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}
