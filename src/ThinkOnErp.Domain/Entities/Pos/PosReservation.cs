using System;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosReservation
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;

    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }

    public DateTime ReservationDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GuestCount { get; set; } = 1;

    // Resource allocation (Restaurant Table OR Salon Room/Staff)
    public long? TableId { get; set; }
    public string? ResourceType { get; set; } // e.g., "Table", "Room", "Station"
    public string? ResourceIdentifier { get; set; }
    public long? StaffEmployeeId { get; set; } // Service provider (therapist, stylist, doctor)

    public PosReservationStatus Status { get; set; } = PosReservationStatus.Booked;

    // Deposit & financial commitment
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public string? DepositPaymentRef { get; set; }

    public string? SpecialRequests { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public SysBranch? Branch { get; set; }
    public Customer? Customer { get; set; }
    public PosTable? Table { get; set; }
}
