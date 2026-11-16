using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

// =================== FLOORS ===================

public class CreateFloorDto
{
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "BranchId is required")]
    public long BranchId { get; set; }

    [Required(ErrorMessage = "FloorCode is required")]
    [StringLength(50)]
    public string FloorCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "FloorName is required")]
    [StringLength(100)]
    public string FloorName { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public class UpdateFloorDto
{
    [Required(ErrorMessage = "FloorName is required")]
    [StringLength(100)]
    public string FloorName { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FloorDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string FloorCode { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int TableCount { get; set; }
}

// =================== TABLES ===================

public class CreateTableDto
{
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "FloorId is required")]
    public long FloorId { get; set; }

    [Required(ErrorMessage = "TableNumber is required")]
    [StringLength(50)]
    public string TableNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string? TableName { get; set; }

    public int Capacity { get; set; } = 4;
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; } = 80;
    public decimal Height { get; set; } = 80;
    public string Shape { get; set; } = "Square";
}

public class UpdateTableDto
{
    [Required(ErrorMessage = "TableNumber is required")]
    [StringLength(50)]
    public string TableNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string? TableName { get; set; }

    public int Capacity { get; set; } = 4;
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; } = 80;
    public decimal Height { get; set; } = 80;
    public string Shape { get; set; } = "Square";
    public bool IsActive { get; set; } = true;
}

public class TableDto
{
    public long Id { get; set; }
    public long FloorId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public int Capacity { get; set; }
    public PosTableStatus Status { get; set; }
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public string Shape { get; set; } = "Square";
    public long? ActiveOrderId { get; set; }
    public long? ParentTableId { get; set; }
    public List<long> MergedTableIds { get; set; } = new();
    public bool IsActive { get; set; }
}

// =================== LAYOUT & DESIGNER ===================

public class FloorLayoutDto
{
    public long Id { get; set; }
    public string FloorCode { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public List<TableLayoutDto> Tables { get; set; } = new();
}

public class TableLayoutDto
{
    public long Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public int Capacity { get; set; }
    public PosTableStatus Status { get; set; }
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public string Shape { get; set; } = "Square";
    public long? ActiveOrderId { get; set; }
    public long? ParentTableId { get; set; }
    public List<long> MergedTableIds { get; set; } = new();
}

public class UpdateTablePositionDto
{
    public long TableId { get; set; }
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
}

public class TransferTableDto
{
    public long FromTableId { get; set; }
    public long ToTableId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class MergeTablesDto
{
    /// <summary>
    /// الطاولة الرئيسية التي سيتم تجميع الطلب والحساب عليها.
    /// </summary>
    public long PrimaryTableId { get; set; }

    /// <summary>
    /// قائمة الطاولات الفرعية المطلوب دمجها وشبكها مع الطاولة الرئيسية.
    /// </summary>
    public List<long> SecondaryTableIds { get; set; } = new();

    /// <summary>
    /// سبب الدمج (مثال: عائلة كبيرة، حفل، جمع طاولات).
    /// </summary>
    public string? Reason { get; set; }
}

public class MergeTablesResultDto
{
    /// <summary>
    /// الطاولة الرئيسية بعد التحديث.
    /// </summary>
    public TableDto PrimaryTable { get; set; } = null!;

    /// <summary>
    /// قائمة الطاولات التابعة بعد دمجها.
    /// </summary>
    public List<TableDto> SecondaryTables { get; set; } = new();

    /// <summary>
    /// السعة الإجمالية المجمعة لجميع الطاولات بعد الشبك.
    /// </summary>
    public int TotalCombinedCapacity { get; set; }

    /// <summary>
    /// تفاصيل الفاتورة النشطة الموحدة (إن وجدت).
    /// </summary>
    public PosOrderSummaryDto? ActiveOrder { get; set; }
}

public class UnmergeTableDto
{
    /// <summary>
    /// الطاولة الفرعية المراد فك دمجها من الطاولة الرئيسية.
    /// </summary>
    public long TableId { get; set; }
}

// =================== RESERVATIONS ===================

public class ReservationServiceItemDto
{
    public long Id { get; set; }
    public long ServiceItemId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 30;
    public int DelayMinutes { get; set; } = 0;
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public List<long> PrimaryStaffIds { get; set; } = new();
    public List<long> AssistantStaffIds { get; set; } = new();
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}

public class AssignReservationOrderDto
{
    public long? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public PosReservationStatus? Status { get; set; }
}

public class CreateReservationDto
{
    public long BranchId { get; set; }
    public long? CustomerId { get; set; }
    public long? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime ReservationDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GuestCount { get; set; } = 1;
    public long? TableId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceIdentifier { get; set; }
    public long? StaffEmployeeId { get; set; }
    public List<long>? AdditionalStaffIds { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? SpecialRequests { get; set; }
    public List<ReservationServiceItemDto>? Services { get; set; }
}

public class UpdateReservationDto
{
    public long? CustomerId { get; set; }
    public long? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime ReservationDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GuestCount { get; set; }
    public long? TableId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceIdentifier { get; set; }
    public long? StaffEmployeeId { get; set; }
    public List<long>? AdditionalStaffIds { get; set; }
    public PosReservationStatus Status { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? SpecialRequests { get; set; }
    public List<ReservationServiceItemDto>? Services { get; set; }
}

public class ReservationSummaryDto
{
    public long Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public long? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime ReservationDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GuestCount { get; set; }
    public long? TableId { get; set; }
    public string? TableNumber { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceIdentifier { get; set; }
    public long? StaffEmployeeId { get; set; }
    public List<long>? AdditionalStaffIds { get; set; }
    public PosReservationStatus Status { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? SpecialRequests { get; set; }
    public string? RecurrenceGroupId { get; set; }
    public bool IsRecurring { get; set; }
    public List<ReservationServiceItemDto> Services { get; set; } = new();
}

public class CreateRecurringReservationDto
{
    public long BranchId { get; set; }
    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }

    public DateTime StartDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GuestCount { get; set; } = 1;

    public long? TableId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceIdentifier { get; set; }
    public long? StaffEmployeeId { get; set; }
    public List<long>? AdditionalStaffIds { get; set; }

    public decimal DepositAmountPerSession { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? SpecialRequests { get; set; }

    /// <summary>
    /// نمط التكرار: Weekly (أسبوعي), BiWeekly (كل أسبوعين), Daily (يومي), Monthly (شهري)
    /// </summary>
    public string RecurrencePattern { get; set; } = "Weekly";

    /// <summary>
    /// يوم الأسبوع (مثل: Sunday = 0, Monday = 1, ...)
    /// </summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>
    /// عدد مرات التكرار (مثلاً 4 أسابيع، 12 أسبوعاً، بحد أقصى 52)
    /// </summary>
    public int Occurrences { get; set; } = 4;
    public List<ReservationServiceItemDto>? Services { get; set; }
}

public class RecurringReservationResultDto
{
    public string RecurrenceGroupId { get; set; } = string.Empty;
    public int RequestedOccurrences { get; set; }
    public int SuccessfulBookingsCount { get; set; }
    public List<ReservationSummaryDto> CreatedReservations { get; set; } = new();
    public List<string> SkippedDatesReasons { get; set; } = new();
}

public class TimeSlotDto
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string FormattedStartTime { get; set; } = string.Empty;
    public string FormattedEndTime { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
    public string? ExistingReservationNumber { get; set; }
    public string? CustomerName { get; set; }
}

public class AvailableSlotsResultDto
{
    public long BranchId { get; set; }
    public DateTime Date { get; set; }
    public int SlotDurationMinutes { get; set; }
    public int BufferMinutes { get; set; }
    public long? StaffEmployeeId { get; set; }
    public long? TableId { get; set; }
    public string? ResourceType { get; set; }
    public int TotalSlotsCount => Slots?.Count ?? 0;
    public int AvailableSlotsCount => Slots?.Count(s => s.IsAvailable) ?? 0;
    public List<TimeSlotDto> Slots { get; set; } = new();
}

public class PosResourceTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "Salon", "Restaurant", "Sports", "Medical"
    public bool RequiresTable { get; set; }
    public bool RequiresStaff { get; set; }
    public bool IsExternal { get; set; }
    public string Description { get; set; } = string.Empty;
}
