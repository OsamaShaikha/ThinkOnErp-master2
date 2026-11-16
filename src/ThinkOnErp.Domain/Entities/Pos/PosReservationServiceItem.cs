using System;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosReservationServiceItem
{
    public long Id { get; set; }
    public long ReservationId { get; set; }
    public long ServiceItemId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int DelayMinutes { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? PrimaryStaffIds { get; set; } // Comma-separated primary staff IDs e.g. "3,7"
    public string? AssistantStaffIds { get; set; } // Comma-separated assistant staff IDs e.g. "12,14"
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public PosReservation? Reservation { get; set; }
}
