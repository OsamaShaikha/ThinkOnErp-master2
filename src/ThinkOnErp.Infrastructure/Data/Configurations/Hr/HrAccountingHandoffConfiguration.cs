using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Infrastructure.Data.Configurations.Hr;

public sealed class HrAccountingHandoffConfiguration : IEntityTypeConfiguration<HrAccountingHandoff>
{
    public void Configure(EntityTypeBuilder<HrAccountingHandoff> b)
    {
        b.ToTable("HR_ACCOUNTING_HANDOFF");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.CompanyId).HasColumnName("COMPANY_ID").IsRequired();
        b.Property(x => x.BranchId).HasColumnName("BRANCH_ID");
        b.Property(x => x.SourceType).HasColumnName("SOURCE_TYPE").HasMaxLength(20).IsRequired();
        b.Property(x => x.SourceId).HasColumnName("SOURCE_ID").HasMaxLength(100).IsRequired();
        b.Property(x => x.SourceVersion).HasColumnName("SOURCE_VERSION").IsRequired();
        b.Property(x => x.Operation).HasColumnName("OPERATION").HasMaxLength(20).IsRequired();
        b.Property(x => x.PayPeriod).HasColumnName("PAY_PERIOD").HasMaxLength(40);
        b.Property(x => x.SourceApprovedAtUtc).HasColumnName("SOURCE_APPROVED_AT_UTC");
        b.Property(x => x.HandoffReference).HasColumnName("HANDOFF_REFERENCE").HasMaxLength(100).IsRequired();
        b.Property(x => x.PayloadHash).HasColumnName("PAYLOAD_HASH").HasMaxLength(64).IsRequired();
        b.Property(x => x.DeliveryStatus).HasColumnName("DELIVERY_STATUS").HasMaxLength(20).IsRequired().IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnName("CREATED_AT_UTC").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("CREATED_BY").HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.CompanyId, x.SourceType, x.SourceId, x.SourceVersion, x.Operation }).IsUnique().HasDatabaseName("UX_HR_HANDOFF_SOURCE");
        b.HasIndex(x => new { x.CompanyId, x.HandoffReference }).IsUnique().HasDatabaseName("UX_HR_HANDOFF_REF");
        b.HasMany(x => x.Lines).WithOne(x => x.Handoff).HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_HR_HANDOFF_LINE");
        b.HasMany(x => x.Transitions).WithOne(x => x.Handoff).HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_HR_HANDOFF_TRANS");
    }
}

public sealed class HrAccountingHandoffLineConfiguration : IEntityTypeConfiguration<HrAccountingHandoffLine>
{
    public void Configure(EntityTypeBuilder<HrAccountingHandoffLine> b)
    {
        b.ToTable("HR_ACCOUNTING_HANDOFF_LINE");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.LineType).HasColumnName("LINE_TYPE").HasMaxLength(40).IsRequired();
        b.Property(x => x.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(20).IsRequired();
        b.Property(x => x.Amount).HasColumnName("AMOUNT").HasPrecision(18, 4).IsRequired();
        b.Property(x => x.BranchId).HasColumnName("BRANCH_ID");
        b.Property(x => x.CostCenterReference).HasColumnName("COST_CENTER_REFERENCE").HasMaxLength(100);
        b.Property(x => x.SourceReference).HasColumnName("SOURCE_REFERENCE").HasMaxLength(200);
        b.HasIndex(x => x.HandoffId).HasDatabaseName("IX_HR_HANDOFF_LINE");
    }
}

public sealed class HrAccountingHandoffTransitionConfiguration : IEntityTypeConfiguration<HrAccountingHandoffTransition>
{
    public void Configure(EntityTypeBuilder<HrAccountingHandoffTransition> b)
    {
        b.ToTable("HR_ACCOUNTING_HANDOFF_TRANSITION");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.FromStatus).HasColumnName("FROM_STATUS").HasMaxLength(20).IsRequired();
        b.Property(x => x.ToStatus).HasColumnName("TO_STATUS").HasMaxLength(20).IsRequired();
        b.Property(x => x.Actor).HasColumnName("ACTOR").HasMaxLength(200).IsRequired();
        b.Property(x => x.Reason).HasColumnName("REASON").HasMaxLength(1000);
        b.Property(x => x.OccurredAtUtc).HasColumnName("OCCURRED_AT_UTC").IsRequired();
        b.HasIndex(x => x.HandoffId).HasDatabaseName("IX_HR_HANDOFF_TRANS");
    }
}

public sealed class HrAccountingOutboxMessageConfiguration : IEntityTypeConfiguration<HrAccountingOutboxMessage>
{
    public void Configure(EntityTypeBuilder<HrAccountingOutboxMessage> b)
    {
        b.ToTable("HR_ACCOUNTING_OUTBOX");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.EventKey).HasColumnName("EVENT_KEY").HasMaxLength(160).IsRequired();
        b.Property(x => x.EventType).HasColumnName("EVENT_TYPE").HasMaxLength(40).IsRequired();
        b.Property(x => x.PayloadHash).HasColumnName("PAYLOAD_HASH").HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired().IsConcurrencyToken();
        b.Property(x => x.AttemptCount).HasColumnName("ATTEMPT_COUNT").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("CREATED_AT_UTC").IsRequired();
        b.Property(x => x.LastAttemptAtUtc).HasColumnName("LAST_ATTEMPT_AT_UTC").IsConcurrencyToken();
        b.Property(x => x.LastError).HasColumnName("LAST_ERROR").HasMaxLength(1000);
        b.HasIndex(x => x.EventKey).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAtUtc }).HasDatabaseName("IX_HR_HANDOFF_OUTBOX");
        b.HasOne(x => x.Handoff).WithMany(x => x.OutboxMessages).HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_HR_HANDOFF_OUTBOX");
    }
}

public sealed class AccountingHandoffInboxMessageConfiguration : IEntityTypeConfiguration<AccountingHandoffInboxMessage>
{
    public void Configure(EntityTypeBuilder<AccountingHandoffInboxMessage> b)
    {
        b.ToTable("ACC_HANDOFF_INBOX");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.EventKey).HasColumnName("EVENT_KEY").HasMaxLength(160).IsRequired();
        b.Property(x => x.PayloadHash).HasColumnName("PAYLOAD_HASH").HasMaxLength(64).IsRequired();
        b.Property(x => x.ReceivedAtUtc).HasColumnName("RECEIVED_AT_UTC").IsRequired();
        b.Property(x => x.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired().IsConcurrencyToken();
        b.HasIndex(x => x.EventKey).IsUnique();
        b.HasIndex(x => new { x.HandoffId, x.ReceivedAtUtc }).HasDatabaseName("IX_ACC_HANDOFF_INBOX");
        b.HasOne(x => x.Handoff).WithMany(x => x.InboxMessages).HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_HANDOFF_INBOX");
    }
}

public sealed class AccountingHandoffPostingReceiptConfiguration : IEntityTypeConfiguration<AccountingHandoffPostingReceipt>
{
    public void Configure(EntityTypeBuilder<AccountingHandoffPostingReceipt> b)
    {
        b.ToTable("ACC_HANDOFF_POSTING_RECEIPT");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.VoucherId).HasColumnName("VOUCHER_ID").IsRequired();
        b.Property(x => x.EventKey).HasColumnName("EVENT_KEY").HasMaxLength(160).IsRequired();
        b.Property(x => x.PayloadHash).HasColumnName("PAYLOAD_HASH").HasMaxLength(64).IsRequired();
        b.Property(x => x.ReceivedAtUtc).HasColumnName("RECEIVED_AT_UTC").IsRequired();
        b.Property(x => x.ReceivedBy).HasColumnName("RECEIVED_BY").HasMaxLength(200).IsRequired();
        b.Property(x => x.SettlementSnapshotJson).HasColumnName("SETTLEMENT_SNAPSHOT_JSON").HasColumnType("CLOB");
        b.HasIndex(x => x.EventKey).IsUnique();
        b.HasIndex(x => x.HandoffId).IsUnique();
        b.HasOne<HrAccountingHandoff>().WithMany().HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_HANDOFF_RECEIPT");
    }
}

public sealed class AccountingHandoffJournalLinkConfiguration : IEntityTypeConfiguration<AccountingHandoffJournalLink>
{
    public void Configure(EntityTypeBuilder<AccountingHandoffJournalLink> b)
    {
        b.ToTable("ACC_HANDOFF_JOURNAL_LINK");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.VoucherId).HasColumnName("VOUCHER_ID").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("CREATED_AT_UTC").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("CREATED_BY").HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.HandoffId).IsUnique();
        b.HasIndex(x => x.VoucherId).IsUnique();
        b.HasOne<HrAccountingHandoff>().WithMany().HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_HANDOFF_JOURNAL");
        b.HasOne<GlVoucherHeader>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_HANDOFF_JV");
    }
}

public sealed class AccountingHandoffPaymentConfiguration : IEntityTypeConfiguration<AccountingHandoffPayment>
{
    public void Configure(EntityTypeBuilder<AccountingHandoffPayment> b)
    {
        b.ToTable("ACC_HANDOFF_PAYMENT");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.PaymentSequence).HasColumnName("PAYMENT_SEQUENCE").IsRequired();
        b.Property(x => x.IdempotencyKey).HasColumnName("IDEMPOTENCY_KEY").HasMaxLength(160).IsRequired();
        b.Property(x => x.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(20).IsRequired();
        b.Property(x => x.Amount).HasColumnName("AMOUNT").HasPrecision(18, 4).IsRequired();
        b.Property(x => x.Method).HasColumnName("METHOD").HasMaxLength(20).IsRequired();
        b.Property(x => x.PaymentMethodCode).HasColumnName("PAYMENT_METHOD_CODE").HasMaxLength(50);
        b.Property(x => x.Status).HasColumnName("STATUS").HasMaxLength(20).IsRequired().IsConcurrencyToken();
        b.Property(x => x.ExternalReference).HasColumnName("EXTERNAL_REFERENCE").HasMaxLength(200);
        b.Property(x => x.EvidenceReference).HasColumnName("EVIDENCE_REFERENCE").HasMaxLength(500);
        b.Property(x => x.FailureReason).HasColumnName("FAILURE_REASON").HasMaxLength(1000);
        b.Property(x => x.FailedAtUtc).HasColumnName("FAILED_AT_UTC");
        b.Property(x => x.FailedBy).HasColumnName("FAILED_BY").HasMaxLength(200);
        b.Property(x => x.ReversalReason).HasColumnName("REVERSAL_REASON").HasMaxLength(1000);
        b.Property(x => x.ReversalExternalReference).HasColumnName("REVERSAL_EXTERNAL_REFERENCE").HasMaxLength(200);
        b.Property(x => x.ReversalEvidenceReference).HasColumnName("REVERSAL_EVIDENCE_REFERENCE").HasMaxLength(500);
        b.Property(x => x.ReversedAtUtc).HasColumnName("REVERSED_AT_UTC");
        b.Property(x => x.ReversedBy).HasColumnName("REVERSED_BY").HasMaxLength(200);
        b.Property(x => x.ReversalVoucherId).HasColumnName("REVERSAL_VOUCHER_ID");
        b.Property(x => x.CreatedAtUtc).HasColumnName("CREATED_AT_UTC").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("CREATED_BY").HasMaxLength(200).IsRequired();
        b.Property(x => x.ConfirmedAtUtc).HasColumnName("CONFIRMED_AT_UTC");
        b.Property(x => x.ConfirmedBy).HasColumnName("CONFIRMED_BY").HasMaxLength(200);
        b.HasIndex(x => new { x.HandoffId, x.IdempotencyKey }).IsUnique().HasDatabaseName("UX_ACC_HANDOFF_PAY_KEY");
        b.HasIndex(x => new { x.HandoffId, x.PaymentSequence }).IsUnique().HasDatabaseName("UX_ACC_HANDOFF_PAY_SEQ");
        b.HasIndex(x => x.ExternalReference).IsUnique().HasDatabaseName("UX_ACC_HANDOFF_PAY_REF");
        b.HasOne<HrAccountingHandoff>().WithMany().HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_HANDOFF_PAYMENT");
    }
}

public sealed class AccountingLoanFundingReceiptConfiguration : IEntityTypeConfiguration<AccountingLoanFundingReceipt>
{
    public void Configure(EntityTypeBuilder<AccountingLoanFundingReceipt> b)
    {
        b.ToTable("ACC_LOAN_FUNDING_RECEIPT");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        b.Property(x => x.HandoffId).HasColumnName("HANDOFF_ID").IsRequired();
        b.Property(x => x.SourceType).HasColumnName("SOURCE_TYPE").HasMaxLength(20).IsRequired();
        b.Property(x => x.SourceId).HasColumnName("SOURCE_ID").HasMaxLength(100).IsRequired();
        b.Property(x => x.SourceVersion).HasColumnName("SOURCE_VERSION").IsRequired().HasDefaultValue(1);
        b.Property(x => x.FundedAmount).HasColumnName("FUNDED_AMOUNT").HasPrecision(18, 4).IsRequired();
        b.Property(x => x.CurrencyCode).HasColumnName("CURRENCY_CODE").HasMaxLength(20).IsRequired();
        b.Property(x => x.FundingVoucherId).HasColumnName("FUNDING_VOUCHER_ID");
        b.Property(x => x.ReversalVoucherId).HasColumnName("REVERSAL_VOUCHER_ID");
        b.Property(x => x.EventKey).HasColumnName("EVENT_KEY").HasMaxLength(160).IsRequired();
        b.Property(x => x.ExternalReference).HasColumnName("EXTERNAL_REFERENCE").HasMaxLength(200).IsRequired();
        b.Property(x => x.ReversalEventKey).HasColumnName("REVERSAL_EVENT_KEY").HasMaxLength(160);
        b.Property(x => x.ReversalReason).HasColumnName("REVERSAL_REASON").HasMaxLength(1000);
        b.Property(x => x.ReversalExternalReference).HasColumnName("REVERSAL_EXTERNAL_REFERENCE").HasMaxLength(200);
        b.Property(x => x.ReversalEvidenceReference).HasColumnName("REVERSAL_EVIDENCE_REFERENCE").HasMaxLength(500);
        b.Property(x => x.ReceivedAtUtc).HasColumnName("RECEIVED_AT_UTC").IsRequired();
        b.Property(x => x.ReceivedBy).HasColumnName("RECEIVED_BY").HasMaxLength(200).IsRequired();
        b.Property(x => x.ReversedAtUtc).HasColumnName("REVERSED_AT_UTC");
        b.Property(x => x.ReversedBy).HasColumnName("REVERSED_BY").HasMaxLength(200);
        b.HasIndex(x => x.HandoffId).IsUnique().HasDatabaseName("UX_ACC_FUND_HANDOFF");
        b.HasIndex(x => x.EventKey).IsUnique().HasDatabaseName("UX_ACC_FUND_EVENT");
        b.HasIndex(x => new { x.SourceType, x.SourceId, x.SourceVersion }).IsUnique().HasDatabaseName("UX_ACC_FUND_SRCVER");
        b.HasIndex(x => x.ExternalReference).IsUnique().HasDatabaseName("UX_ACC_FUND_REF");
        b.HasIndex(x => x.FundingVoucherId).IsUnique().HasDatabaseName("UX_ACC_FUND_VOUCHER");
        b.HasIndex(x => x.ReversalEventKey).IsUnique().HasDatabaseName("UX_ACC_FUND_REV_EVENT");
        b.HasIndex(x => x.ReversalExternalReference).IsUnique().HasDatabaseName("UX_ACC_FUND_REV_REF");
        b.HasIndex(x => x.ReversalVoucherId).IsUnique().HasDatabaseName("UX_ACC_FUND_REV_VCH");
        b.HasOne<HrAccountingHandoff>().WithMany().HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_LOAN_FUND_RECEIPT");
        b.HasOne<GlVoucherHeader>().WithMany().HasForeignKey(x => x.FundingVoucherId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_FUNDING_GL_VOUCHER");
        b.HasOne<GlVoucherHeader>().WithMany().HasForeignKey(x => x.ReversalVoucherId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ACC_FUND_REV_GL_VCH");
    }
}
