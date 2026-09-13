using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosReservationConfiguration : IEntityTypeConfiguration<PosReservation>
{
    public void Configure(EntityTypeBuilder<PosReservation> builder)
    {
        builder.ToTable("POS_RESERVATION");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.BranchId).HasColumnName("BRANCH_ID").IsRequired();
        builder.Property(r => r.ReservationNumber).HasColumnName("RESERVATION_NUMBER").HasMaxLength(50).IsRequired();

        builder.Property(r => r.CustomerId).HasColumnName("CUSTOMER_ID");
        builder.Property(r => r.CustomerName).HasColumnName("CUSTOMER_NAME").HasMaxLength(150).IsRequired();
        builder.Property(r => r.CustomerPhone).HasColumnName("CUSTOMER_PHONE").HasMaxLength(50).IsRequired();
        builder.Property(r => r.CustomerEmail).HasColumnName("CUSTOMER_EMAIL").HasMaxLength(100);

        builder.Property(r => r.ReservationDate).HasColumnName("RESERVATION_DATE").HasColumnType("TIMESTAMP").IsRequired();
        builder.Property(r => r.StartTime).HasColumnName("START_TIME").IsRequired();
        builder.Property(r => r.EndTime).HasColumnName("END_TIME").IsRequired();
        builder.Property(r => r.GuestCount).HasColumnName("GUEST_COUNT").HasDefaultValue(1);

        builder.Property(r => r.TableId).HasColumnName("TABLE_ID");
        builder.Property(r => r.ResourceType).HasColumnName("RESOURCE_TYPE").HasMaxLength(50);
        builder.Property(r => r.ResourceIdentifier).HasColumnName("RESOURCE_IDENTIFIER").HasMaxLength(100);
        builder.Property(r => r.StaffEmployeeId).HasColumnName("STAFF_EMPLOYEE_ID");

        builder.Property(r => r.Status).HasColumnName("STATUS").HasConversion<int>().IsRequired();

        builder.Property(r => r.DepositAmount).HasColumnName("DEPOSIT_AMOUNT").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(r => r.IsDepositPaid).HasColumnName("IS_DEPOSIT_PAID").HasColumnType("NUMBER(1)").HasDefaultValue(false);
        builder.Property(r => r.DepositPaymentRef).HasColumnName("DEPOSIT_PAYMENT_REF").HasMaxLength(100);

        builder.Property(r => r.SpecialRequests).HasColumnName("SPECIAL_REQUESTS").HasMaxLength(500);
        builder.Property(r => r.CreationUser).HasColumnName("CREATION_USER").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreationDate).HasColumnName("CREATION_DATE").HasColumnType("TIMESTAMP");

        builder.HasIndex(r => new { r.BranchId, r.ReservationNumber }).IsUnique();
        builder.HasOne(r => r.Branch).WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Table).WithMany().HasForeignKey(r => r.TableId).OnDelete(DeleteBehavior.SetNull);
    }
}
