using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.ToTable("HR_ASSET_ASSIGNMENT");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AssetTag)
            .HasColumnName("ASSET_TAG")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AssetDescription)
            .HasColumnName("ASSET_DESCRIPTION")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Category)
            .HasColumnName("CATEGORY")
            .HasMaxLength(50)
            .HasDefaultValue("LAPTOP")
            .IsRequired();

        builder.Property(a => a.SerialNumber)
            .HasColumnName("SERIAL_NUMBER")
            .HasMaxLength(100);

        builder.Property(a => a.IssuedDate)
            .HasColumnName("ISSUED_DATE")
            .IsRequired();

        builder.Property(a => a.ExpectedReturnDate)
            .HasColumnName("EXPECTED_RETURN_DATE");

        builder.Property(a => a.ReturnedDate)
            .HasColumnName("RETURNED_DATE");

        builder.Property(a => a.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(30)
            .HasDefaultValue("ASSIGNED")
            .IsRequired();

        builder.Property(a => a.IssuedCondition)
            .HasColumnName("ISSUED_CONDITION")
            .HasMaxLength(50)
            .HasDefaultValue("NEW");

        builder.Property(a => a.ReturnedCondition)
            .HasColumnName("RETURNED_CONDITION")
            .HasMaxLength(50);

        builder.Property(a => a.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(500);

        builder.Property(a => a.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(a => a.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(a => a.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
