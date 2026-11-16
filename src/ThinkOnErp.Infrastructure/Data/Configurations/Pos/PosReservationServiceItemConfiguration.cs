using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Pos;

public sealed class PosReservationServiceItemConfiguration : IEntityTypeConfiguration<PosReservationServiceItem>
{
    public void Configure(EntityTypeBuilder<PosReservationServiceItem> builder)
    {
        builder.ToTable("POS_RESERVATION_SERVICE");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.ReservationId).HasColumnName("RESERVATION_ID").IsRequired();
        builder.Property(s => s.ServiceItemId).HasColumnName("SERVICE_ITEM_ID").IsRequired();
        builder.Property(s => s.ServiceName).HasColumnName("SERVICE_NAME").HasMaxLength(150).IsRequired();
        builder.Property(s => s.DurationMinutes).HasColumnName("DURATION_MINUTES").HasDefaultValue(30);
        builder.Property(s => s.DelayMinutes).HasColumnName("DELAY_MINUTES").HasDefaultValue(0);
        builder.Property(s => s.StartTime).HasColumnName("START_TIME").IsRequired();
        builder.Property(s => s.EndTime).HasColumnName("END_TIME").IsRequired();
        builder.Property(s => s.PrimaryStaffIds).HasColumnName("PRIMARY_STAFF_IDS").HasMaxLength(200);
        builder.Property(s => s.AssistantStaffIds).HasColumnName("ASSISTANT_STAFF_IDS").HasMaxLength(200);
        builder.Property(s => s.Price).HasColumnName("PRICE").HasColumnType("NUMBER(18,4)").HasDefaultValue(0m);
        builder.Property(s => s.Notes).HasColumnName("NOTES").HasMaxLength(500);
        builder.Property(s => s.SortOrder).HasColumnName("SORT_ORDER").HasDefaultValue(1);

        builder.HasIndex(s => s.ReservationId);
    }
}
