using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class PayrollPostingConfigurationMapping : IEntityTypeConfiguration<PayrollPostingConfiguration>
{
    public void Configure(EntityTypeBuilder<PayrollPostingConfiguration> builder)
    {
        builder.ToTable("HR_GL_POSTING_CONFIG");
        builder.HasKey(e => e.BranchId);
        builder.Property(e => e.BranchId).HasColumnName("BRANCH_ID").ValueGeneratedNever();
        foreach (var name in new[] { "SalaryExpense", "EmployerSscExpense", "SalaryPayable", "SscPayable", "TaxPayable", "DeductionsPayable" })
            builder.Property<string>(name).HasColumnName(name.ToUpperInvariant()).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CurrencyId).HasColumnName("CURRENCY_ID");
        builder.Property(e => e.VoucherType).HasColumnName("VOUCHER_TYPE");
    }
}
