using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("HR_EMPLOYEE_DOCUMENT");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.EmployeeCode)
            .HasColumnName("EMPLOYEE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DocumentType)
            .HasColumnName("DOCUMENT_TYPE")
            .HasMaxLength(50)
            .HasDefaultValue("NATIONAL_ID")
            .IsRequired();

        builder.Property(d => d.DocumentNumber)
            .HasColumnName("DOCUMENT_NUMBER")
            .HasMaxLength(100);

        builder.Property(d => d.FileReference)
            .HasColumnName("FILE_REFERENCE")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasColumnName("FILE_NAME")
            .HasMaxLength(255);

        builder.Property(d => d.IssuedDate)
            .HasColumnName("ISSUED_DATE");

        builder.Property(d => d.ExpiryDate)
            .HasColumnName("EXPIRY_DATE");

        builder.Property(d => d.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(500);

        builder.Property(d => d.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(d => d.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(d => d.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(d => d.UpdateDate)
            .HasColumnName("UPDATE_DATE");

        builder.HasIndex(d => new { d.EmployeeCode, d.DocumentType, d.ExpiryDate })
            .HasDatabaseName("IX_HR_EMP_DOC_EXPIRY");

        // Relationships
        builder.HasOne(d => d.Employee)
            .WithMany(e => e.Documents)
            .HasForeignKey(d => d.EmployeeCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
