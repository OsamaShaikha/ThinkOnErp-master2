using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class CandidateApplicationConfiguration : IEntityTypeConfiguration<CandidateApplication>
{
    public void Configure(EntityTypeBuilder<CandidateApplication> builder)
    {
        builder.ToTable("HR_CANDIDATE_APP");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.CandidateCode)
            .HasColumnName("CANDIDATE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.RequisitionCode)
            .HasColumnName("REQUISITION_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Stage)
            .HasColumnName("STAGE")
            .HasMaxLength(30)
            .HasDefaultValue("APPLIED")
            .IsRequired();

        builder.Property(a => a.OfferedSalary)
            .HasColumnName("OFFERED_SALARY")
            .HasColumnType("NUMBER(18,4)");

        builder.Property(a => a.InterviewDate)
            .HasColumnName("INTERVIEW_DATE");

        builder.Property(a => a.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(1000);

        builder.Property(a => a.HiredEmployeeCode)
            .HasColumnName("HIRED_EMPLOYEE_CODE")
            .HasMaxLength(50);

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

        builder.HasOne(a => a.Candidate)
            .WithMany(c => c.Applications)
            .HasForeignKey(a => a.CandidateCode)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Requisition)
            .WithMany(r => r.Applications)
            .HasForeignKey(a => a.RequisitionCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
