using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Accounting;

public class GlAccountBranch
{
    public string AccountCode { get; set; } = string.Empty;
    public long BranchId { get; set; }
    public bool IsActive { get; set; } = true;

    public GlAccount? GlAccount { get; set; }
    public SysBranch? Branch { get; set; }
}

