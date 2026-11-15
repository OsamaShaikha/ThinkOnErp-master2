namespace ThinkOnErp.Domain.Entities.Hr;

public static class LeaveMovementKey
{
    public static string Hash(string text)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }
}

public readonly record struct LeaveBalanceState(decimal Accrued, decimal Used, decimal Carried)
{
    public static LeaveBalanceState From(LeaveBalance? balance)
    {
        return balance == null ? new(0, 0, 0)
        : new(balance.AccruedDays, balance.UsedDays, balance.CarriedForwardDays);
    }

    public decimal Available => Accrued + Carried - Used;
}

public sealed class LeaveBalanceMovement
{
    public long Id { get; set; }
    public long BalanceId { get; set; }
    public LeaveBalance Balance { get; set; } = null!;
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateTime CycleStartDate { get; set; }
    public long SequenceNo { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public string? PayloadHash { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public decimal AccruedDelta { get; set; }
    public decimal UsedDelta { get; set; }
    public decimal CarriedDelta { get; set; }
    public decimal AccruedAfter { get; set; }
    public decimal UsedAfter { get; set; }
    public decimal CarriedAfter { get; set; }
    public decimal ExpiredDays { get; set; }
    public decimal TransferredDays { get; set; }
    public long? ReversesMovementId { get; set; }

    public static LeaveBalanceMovement Create(LeaveBalance balance, LeaveBalanceState before, string kind,
        string operationKey, string source, string reason, string actor, long sequenceNo)
    {
        if (string.IsNullOrWhiteSpace(operationKey) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Movement key, source and reason are required.");
        }

        LeaveBalanceState after = LeaveBalanceState.From(balance);
        return new LeaveBalanceMovement
        {
            Balance = balance,
            BalanceId = balance.Id,
            EmployeeCode = balance.EmployeeCode,
            LeaveTypeCode = balance.LeaveTypeCode,
            CycleStartDate = balance.CycleStartDate ?? new DateTime(balance.YearNo, 1, 1),
            Kind = kind,
            OperationKey = operationKey,
            Source = source,
            Reason = reason,
            Actor = actor,
            SequenceNo = sequenceNo,
            AccruedDelta = after.Accrued - before.Accrued,
            UsedDelta = after.Used - before.Used,
            CarriedDelta = after.Carried - before.Carried,
            AccruedAfter = after.Accrued,
            UsedAfter = after.Used,
            CarriedAfter = after.Carried
        };
    }
}
