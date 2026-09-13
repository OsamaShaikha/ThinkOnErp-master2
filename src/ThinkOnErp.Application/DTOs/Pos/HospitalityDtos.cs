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

// =================== RESERVATIONS ===================

public class CreateReservationDto
{
    public long BranchId { get; set; }
    public long? CustomerId { get; set; }
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
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? SpecialRequests { get; set; }
}

public class UpdateReservationDto
{
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
    public PosReservationStatus Status { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? SpecialRequests { get; set; }
}

public class ReservationSummaryDto
{
    public long Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
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
    public PosReservationStatus Status { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? SpecialRequests { get; set; }
}
