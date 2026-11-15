namespace ThinkOnErp.Application.DTOs.Hr;

public record LeaveMovementDto(long Id, long BalanceId, string EmployeeCode, string LeaveTypeCode, DateTime CycleStartDate,
    long SequenceNo, string Kind, string Source, string Reason, string Actor, DateTime CreatedDate,
    decimal AccruedDelta, decimal UsedDelta, decimal CarriedDelta, decimal AccruedAfter, decimal UsedAfter, decimal CarriedAfter,
    decimal AvailableAfter, decimal ExpiredDays, decimal TransferredDays, long? ReversesMovementId);

public record LeaveMovementPageDto(IReadOnlyList<LeaveMovementDto> Items, int TotalCount, int PageIndex, int PageSize);
