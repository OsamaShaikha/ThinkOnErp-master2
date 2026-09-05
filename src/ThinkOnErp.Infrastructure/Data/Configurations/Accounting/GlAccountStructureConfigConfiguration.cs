using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

public class GlAccountStructureConfigConfiguration : IEntityTypeConfiguration<GlAccountStructureConfig>
{
    public void Configure(EntityTypeBuilder<GlAccountStructureConfig> builder)
    {
        builder.ToTable("GL_ACCOUNT_STRUCTURE_CONFIG");

        builder.HasKey(c => c.LevelNumber);

        builder.Property(c => c.LevelNumber)
            .HasColumnName("LEVEL_NUMBER")
            .ValueGeneratedNever();

        builder.Property(c => c.DigitLength)
            .HasColumnName("DIGIT_LENGTH")
            .IsRequired();

        builder.Property(c => c.LevelNameLocal)
            .HasColumnName("LEVEL_NAME_LOCAL")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.LevelNameEn)
            .HasColumnName("LEVEL_NAME_EN")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("DESCRIPTION")
            .HasMaxLength(500);

        builder.Property(c => c.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasColumnType("NUMBER(1)")
            .HasDefaultValue(true);

        // Seed default 5-level structure configuration
        builder.HasData(
            new GlAccountStructureConfig
            {
                LevelNumber = 1,
                DigitLength = 1,
                LevelNameLocal = "المستوى الأول - الحسابات الرئيسية العالية",
                LevelNameEn = "Level 1 - Main Primary Accounts",
                Description = "خانة واحدة للحسابات الرئيسية (1: الأصول، 2: الخصوم، 3: حقوق الملكية...)",
                IsActive = true
            },
            new GlAccountStructureConfig
            {
                LevelNumber = 2,
                DigitLength = 1,
                LevelNameLocal = "المستوى الثاني - الفئات الرئيسية",
                LevelNameEn = "Level 2 - Main Categories",
                Description = "خانة واحدة إضافية للفئات الرئيسية (11: الأصول المتداولة، 12: الأصول غير المتداولة...)",
                IsActive = true
            },
            new GlAccountStructureConfig
            {
                LevelNumber = 3,
                DigitLength = 1,
                LevelNameLocal = "المستوى الثالث - المجموعات الفرعية",
                LevelNameEn = "Level 3 - Sub Groups",
                Description = "خانة واحدة إضافية للمجموعات الفرعية (111: النقدية وما في حكمها...)",
                IsActive = true
            },
            new GlAccountStructureConfig
            {
                LevelNumber = 4,
                DigitLength = 1,
                LevelNameLocal = "المستوى الرابع - الحسابات التجميعية",
                LevelNameEn = "Level 4 - Summary Accounts",
                Description = "خانة واحدة إضافية للحسابات التجميعية (1111: البنوك...)",
                IsActive = true
            },
            new GlAccountStructureConfig
            {
                LevelNumber = 5,
                DigitLength = 2,
                LevelNameLocal = "المستوى الخامس - الحسابات الفرعية التفصيلية",
                LevelNameEn = "Level 5 - Detail Sub Accounts",
                Description = "خانة أو خانتان تفصيلية للحسابات الفرعية للتسجيل والترحيل المباشر (111101: بنك الاتحاد - حساب جاري...)",
                IsActive = true
            }
        );
    }
}
