using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosOrderPaymentConfiguration : IEntityTypeConfiguration<PosOrderPayment>
{
    public void Configure(EntityTypeBuilder<PosOrderPayment> builder)
    {
        builder.ToTable("POS_ORDER_PAYMENT");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.OrderId).HasColumnName("ORDER_ID").IsRequired();
        builder.Property(p => p.PaymentMethod).HasColumnName("PAYMENT_METHOD").HasConversion<int>().IsRequired();
        builder.Property(p => p.Amount).HasColumnName("AMOUNT").HasColumnType("NUMBER(18,4)").IsRequired();
        builder.Property(p => p.TenderedAmount).HasColumnName("TENDERED_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(p => p.ChangeAmount).HasColumnName("CHANGE_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);

        builder.Property(p => p.CardNumberMasked).HasColumnName("CARD_NUMBER_MASKED").HasMaxLength(30);
        builder.Property(p => p.CardType).HasColumnName("CARD_TYPE").HasMaxLength(50);
        builder.Property(p => p.TransactionReference).HasColumnName("TRANSACTION_REFERENCE").HasMaxLength(100);
        builder.Property(p => p.AuthCode).HasColumnName("AUTH_CODE").HasMaxLength(50);
        builder.Property(p => p.TerminalId).HasColumnName("TERMINAL_ID").HasMaxLength(50);

        builder.Property(p => p.ChequeNumber).HasColumnName("CHEQUE_NUMBER").HasMaxLength(50);
        builder.Property(p => p.GiftCardCode).HasColumnName("GIFT_CARD_CODE").HasMaxLength(50);
        builder.Property(p => p.LoyaltyPointsRedeemed).HasColumnName("LOYALTY_POINTS_REDEEMED");

        builder.Property(p => p.PaymentDate).HasColumnName("PAYMENT_DATE").HasColumnType("TIMESTAMP");
        builder.Property(p => p.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();

        builder.HasOne(p => p.Order).WithMany(o => o.Payments).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
