using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("HR_CANDIDATE");

        builder.HasKey(c => c.CandidateCode);

        builder.Property(c => c.CandidateCode)
            .HasColumnName("CANDIDATE_CODE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.NameAr)
            .HasColumnName("NAME_AR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.NameEn)
            .HasColumnName("NAME_EN")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasColumnName("EMAIL")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Phone)
            .HasColumnName("PHONE")
            .HasMaxLength(50);

        builder.Property(c => c.NationalId)
            .HasColumnName("NATIONAL_ID")
            .HasMaxLength(50);

        builder.Property(c => c.ResumeFileReference)
            .HasColumnName("RESUME_FILE_REF")
            .HasMaxLength(500);

        builder.Property(c => c.Source)
            .HasColumnName("SOURCE")
            .HasMaxLength(50)
            .HasDefaultValue("DIRECT")
            .IsRequired();

        builder.Property(c => c.CreationUser)
            .HasColumnName("CREATION_USER")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CreationDate)
            .HasColumnName("CREATION_DATE")
            .IsRequired();

        builder.Property(c => c.UpdateUser)
            .HasColumnName("UPDATE_USER")
            .HasMaxLength(100);

        builder.Property(c => c.UpdateDate)
            .HasColumnName("UPDATE_DATE");
    }
}
