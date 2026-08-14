namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlVoucherSerial
{
    public long BranchId { get; set; }
    public int SerialYear { get; set; }
    public int SerialMonth { get; set; }
    public int VoucherType { get; set; }
    public long LastSerialNo { get; set; }
}
