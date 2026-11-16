using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosReservationService
{
    Task<ApiResponse<ReservationSummaryDto>> CreateReservationAsync(CreateReservationDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<ReservationSummaryDto>> GetReservationByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<ReservationSummaryDto>>> GetReservationsForDayAsync(long branchId, DateTime date, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<ReservationSummaryDto>>> GetReservationsPagedAsync(
        long branchId,
        long? customerId = null,
        long? tableId = null,
        PosReservationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<ApiResponse<AvailableSlotsResultDto>> GetAvailableSlotsAsync(
        long branchId,
        DateTime date,
        long? staffEmployeeId = null,
        long? tableId = null,
        string? resourceType = null,
        int durationMinutes = 30,
        int bufferMinutes = 10,
        TimeSpan? openingTime = null,
        TimeSpan? closingTime = null,
        CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<PosResourceTypeDto>>> GetResourceTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<RecurringReservationResultDto>> CreateRecurringReservationAsync(CreateRecurringReservationDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> CancelRecurringSeriesAsync(string recurrenceGroupId, string username, CancellationToken ct = default);
    Task<ApiResponse<ReservationSummaryDto>> UpdateReservationAsync(long id, UpdateReservationDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<ReservationSummaryDto>> AssignOrderToReservationAsync(long reservationId, AssignReservationOrderDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateReservationStatusAsync(long reservationId, PosReservationStatus status, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteReservationAsync(long id, CancellationToken ct = default);
}

public class PosReservationService : IPosReservationService
{
    private readonly IPosReservationRepository _reservationRepository;

    public PosReservationService(IPosReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<ApiResponse<ReservationSummaryDto>> CreateReservationAsync(CreateReservationDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Request body cannot be null", null, 400);

        if (string.IsNullOrWhiteSpace(dto.CustomerName) || string.IsNullOrWhiteSpace(dto.CustomerPhone))
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Customer name and phone are required", null, 400);

        if (dto.BranchId <= 0)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Valid BranchId is required", null, 400);

        List<PosReservationServiceItem> serviceEntities = new();
        if (dto.Services != null && dto.Services.Count > 0)
        {
            var prep = PrepareReservationServices(dto.StartTime, dto.Services);
            dto.EndTime = prep.EndTime;
            serviceEntities = prep.Entities;
        }

        if (dto.EndTime <= dto.StartTime)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("وقت نهاية الحجز يجب أن يكون أكبر من وقت البداية (EndTime must be greater than StartTime)", null, 400);

        var isExternal = string.Equals(dto.ResourceType, "HomeService", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(dto.ResourceType, "External", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(dto.ResourceType, "HomeVisit", StringComparison.OrdinalIgnoreCase);

        if (isExternal)
        {
            dto.TableId = null; // External/home services do not occupy internal tables
            var hasAnyStaff = (dto.StaffEmployeeId.HasValue && dto.StaffEmployeeId.Value > 0) ||
                              (dto.Services != null && dto.Services.Any(s => (s.PrimaryStaffIds != null && s.PrimaryStaffIds.Count > 0) || (s.AssistantStaffIds != null && s.AssistantStaffIds.Count > 0)));
            if (!hasAnyStaff)
            {
                return ApiResponse<ReservationSummaryDto>.CreateFailure("يجب تحديد الموظف / مقدم الخدمة للخدمات الخارجية والمنزلية (StaffEmployeeId is required for external / home services)", null, 400);
            }
        }

        // Multi-Staff Requested
        var requestedStaff = new HashSet<long>();
        if (dto.StaffEmployeeId.HasValue && dto.StaffEmployeeId.Value > 0)
            requestedStaff.Add(dto.StaffEmployeeId.Value);
        if (dto.AdditionalStaffIds != null && dto.AdditionalStaffIds.Count > 0)
        {
            foreach (var id in dto.AdditionalStaffIds.Where(x => x > 0))
                requestedStaff.Add(id);
        }
        if (dto.Services != null)
        {
            foreach (var s in dto.Services)
            {
                foreach (var id in GetStaffIdsForService(s))
                    requestedStaff.Add(id);
            }
        }

        if ((!dto.StaffEmployeeId.HasValue || dto.StaffEmployeeId.Value <= 0) && requestedStaff.Count > 0)
        {
            dto.StaffEmployeeId = requestedStaff.First();
        }

        // Conflict / Overlap Detection
        var dayReservations = await _reservationRepository.GetReservationsForDayAsync(dto.BranchId, dto.ReservationDate, ct);
        var activeReservations = dayReservations.Where(r =>
            r.Status == PosReservationStatus.Booked ||
            r.Status == PosReservationStatus.Confirmed ||
            r.Status == PosReservationStatus.Seated).ToList();

        var conflictMessage = CheckReservationConflict(
            dto.StartTime,
            dto.EndTime,
            dto.TableId,
            dto.ResourceType,
            dto.ResourceIdentifier,
            dto.Services,
            requestedStaff,
            activeReservations);

        if (!string.IsNullOrWhiteSpace(conflictMessage))
        {
            return ApiResponse<ReservationSummaryDto>.CreateFailure(conflictMessage, null, 400);
        }

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var resNumber = $"RES-{dto.BranchId}-{today}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

        var reservation = new PosReservation
        {
            BranchId = dto.BranchId,
            ReservationNumber = resNumber,
            CustomerId = dto.CustomerId,
            OrderId = dto.OrderId,
            OrderNumber = dto.OrderNumber?.Trim(),
            CustomerName = dto.CustomerName.Trim(),
            CustomerPhone = dto.CustomerPhone.Trim(),
            CustomerEmail = dto.CustomerEmail?.Trim(),
            ReservationDate = dto.ReservationDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            GuestCount = dto.GuestCount > 0 ? dto.GuestCount : 1,
            TableId = dto.TableId,
            ResourceType = dto.ResourceType ?? "Table",
            ResourceIdentifier = dto.ResourceIdentifier,
            StaffEmployeeId = dto.StaffEmployeeId,
            AdditionalStaffIds = FormatStaffIds(dto.AdditionalStaffIds),
            Status = PosReservationStatus.Booked,
            DepositAmount = dto.DepositAmount,
            IsDepositPaid = dto.IsDepositPaid,
            SpecialRequests = dto.SpecialRequests,
            CreationUser = username,
            CreationDate = DateTime.UtcNow,
            Services = serviceEntities
        };

        await _reservationRepository.AddReservationAsync(reservation, ct);
        await _reservationRepository.SaveChangesAsync(ct);

        return ApiResponse<ReservationSummaryDto>.CreateSuccess(MapToSummary(reservation), "Reservation created successfully");
    }

    public async Task<ApiResponse<ReservationSummaryDto>> GetReservationByIdAsync(long id, CancellationToken ct = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(id, ct);
        if (reservation == null)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Reservation not found", null, 404);

        return ApiResponse<ReservationSummaryDto>.CreateSuccess(MapToSummary(reservation));
    }

    public async Task<ApiResponse<IReadOnlyList<ReservationSummaryDto>>> GetReservationsForDayAsync(long branchId, DateTime date, CancellationToken ct = default)
    {
        var reservations = await _reservationRepository.GetReservationsForDayAsync(branchId, date, ct);
        var result = reservations.Select(MapToSummary).ToList();
        return ApiResponse<IReadOnlyList<ReservationSummaryDto>>.CreateSuccess(result);
    }

    public async Task<ApiResponse<PagedResultDto<ReservationSummaryDto>>> GetReservationsPagedAsync(
        long branchId,
        long? customerId = null,
        long? tableId = null,
        PosReservationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _reservationRepository.GetReservationsPagedAsync(
            branchId, customerId, tableId, status, fromDate, toDate, pageIndex, pageSize, ct);

        var dtos = items.Select(MapToSummary).ToList();
        var paged = new PagedResultDto<ReservationSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<ReservationSummaryDto>>.CreateSuccess(paged);
    }

    public async Task<ApiResponse<ReservationSummaryDto>> UpdateReservationAsync(long id, UpdateReservationDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Request body cannot be null", null, 400);

        var reservation = await _reservationRepository.GetByIdAsync(id, ct);
        if (reservation == null)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Reservation not found", null, 404);

        List<PosReservationServiceItem> updateServiceEntities = new();
        if (dto.Services != null && dto.Services.Count > 0)
        {
            var prep = PrepareReservationServices(dto.StartTime, dto.Services);
            dto.EndTime = prep.EndTime;
            updateServiceEntities = prep.Entities;
        }

        if (dto.EndTime <= dto.StartTime)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("وقت نهاية الحجز يجب أن يكون أكبر من وقت البداية (EndTime must be greater than StartTime)", null, 400);

        var isExternal = string.Equals(dto.ResourceType, "HomeService", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(dto.ResourceType, "External", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(dto.ResourceType, "HomeVisit", StringComparison.OrdinalIgnoreCase);

        if (isExternal)
        {
            dto.TableId = null;
            var hasAnyStaff = (dto.StaffEmployeeId.HasValue && dto.StaffEmployeeId.Value > 0) ||
                              (dto.Services != null && dto.Services.Any(s => (s.PrimaryStaffIds != null && s.PrimaryStaffIds.Count > 0) || (s.AssistantStaffIds != null && s.AssistantStaffIds.Count > 0)));
            if (!hasAnyStaff)
            {
                return ApiResponse<ReservationSummaryDto>.CreateFailure("يجب تحديد الموظف / مقدم الخدمة للخدمات الخارجية والمنزلية (StaffEmployeeId is required for external / home services)", null, 400);
            }
        }

        // Multi-Staff Requested
        var requestedStaff = new HashSet<long>();
        if (dto.StaffEmployeeId.HasValue && dto.StaffEmployeeId.Value > 0)
            requestedStaff.Add(dto.StaffEmployeeId.Value);
        if (dto.AdditionalStaffIds != null && dto.AdditionalStaffIds.Count > 0)
        {
            foreach (var sid in dto.AdditionalStaffIds.Where(x => x > 0))
                requestedStaff.Add(sid);
        }
        if (dto.Services != null)
        {
            foreach (var s in dto.Services)
            {
                foreach (var sid in GetStaffIdsForService(s))
                    requestedStaff.Add(sid);
            }
        }

        if ((!dto.StaffEmployeeId.HasValue || dto.StaffEmployeeId.Value <= 0) && requestedStaff.Count > 0)
        {
            dto.StaffEmployeeId = requestedStaff.First();
        }

        // Conflict check excluding current reservation
        var dayReservations = await _reservationRepository.GetReservationsForDayAsync(reservation.BranchId, dto.ReservationDate, ct);
        var activeReservations = dayReservations.Where(r =>
            r.Id != id &&
            (r.Status == PosReservationStatus.Booked ||
             r.Status == PosReservationStatus.Confirmed ||
             r.Status == PosReservationStatus.Seated)).ToList();

        var conflictMessage = CheckReservationConflict(
            dto.StartTime,
            dto.EndTime,
            dto.TableId,
            dto.ResourceType,
            dto.ResourceIdentifier,
            dto.Services,
            requestedStaff,
            activeReservations,
            excludeReservationId: id);

        if (!string.IsNullOrWhiteSpace(conflictMessage))
        {
            return ApiResponse<ReservationSummaryDto>.CreateFailure(conflictMessage, null, 400);
        }

        if (dto.CustomerId.HasValue)
            reservation.CustomerId = dto.CustomerId.Value > 0 ? dto.CustomerId : null;
        if (dto.OrderId.HasValue)
            reservation.OrderId = dto.OrderId.Value > 0 ? dto.OrderId : null;
        if (dto.OrderNumber != null)
            reservation.OrderNumber = string.IsNullOrWhiteSpace(dto.OrderNumber) ? null : dto.OrderNumber.Trim();
        if (!string.IsNullOrWhiteSpace(dto.CustomerName))
            reservation.CustomerName = dto.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.CustomerPhone))
            reservation.CustomerPhone = dto.CustomerPhone.Trim();
        reservation.CustomerEmail = dto.CustomerEmail?.Trim();
        reservation.ReservationDate = dto.ReservationDate;
        reservation.StartTime = dto.StartTime;
        reservation.EndTime = dto.EndTime;
        reservation.GuestCount = dto.GuestCount > 0 ? dto.GuestCount : 1;
        reservation.TableId = dto.TableId;
        reservation.ResourceType = dto.ResourceType;
        reservation.ResourceIdentifier = dto.ResourceIdentifier;
        reservation.StaffEmployeeId = dto.StaffEmployeeId;
        reservation.AdditionalStaffIds = FormatStaffIds(dto.AdditionalStaffIds);
        reservation.Status = dto.Status;
        reservation.DepositAmount = dto.DepositAmount;
        reservation.IsDepositPaid = dto.IsDepositPaid;
        reservation.SpecialRequests = dto.SpecialRequests;

        if (dto.Services != null)
        {
            reservation.Services = updateServiceEntities;
        }

        await _reservationRepository.UpdateReservationAsync(reservation, ct);
        await _reservationRepository.SaveChangesAsync(ct);

        return ApiResponse<ReservationSummaryDto>.CreateSuccess(MapToSummary(reservation), "Reservation updated successfully");
    }

    public async Task<ApiResponse<ReservationSummaryDto>> AssignOrderToReservationAsync(
        long reservationId,
        AssignReservationOrderDto dto,
        CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Request body cannot be null", null, 400);

        var reservation = await _reservationRepository.GetByIdAsync(reservationId, ct);
        if (reservation == null)
            return ApiResponse<ReservationSummaryDto>.CreateFailure("Reservation not found", null, 404);

        if (!dto.OrderId.HasValue && string.IsNullOrWhiteSpace(dto.OrderNumber))
        {
            return ApiResponse<ReservationSummaryDto>.CreateFailure("يجب إرسال إما رقم الطلب (OrderNumber) أو معرّف الطلب (OrderId)", null, 400);
        }

        if (dto.OrderId.HasValue && dto.OrderId.Value > 0)
        {
            reservation.OrderId = dto.OrderId.Value;
            var order = await _reservationRepository.GetOrderByIdAsync(dto.OrderId.Value, ct);
            if (order != null)
            {
                reservation.OrderNumber = order.OrderNumber;
            }
            else if (!string.IsNullOrWhiteSpace(dto.OrderNumber))
            {
                reservation.OrderNumber = dto.OrderNumber.Trim();
            }
        }
        else if (!string.IsNullOrWhiteSpace(dto.OrderNumber))
        {
            var trimmedNumber = dto.OrderNumber.Trim();
            reservation.OrderNumber = trimmedNumber;
            var order = await _reservationRepository.GetOrderByNumberAsync(reservation.BranchId, trimmedNumber, ct);
            if (order != null)
            {
                reservation.OrderId = order.Id;
            }
        }

        if (dto.Status.HasValue)
        {
            reservation.Status = dto.Status.Value;
        }
        else if (reservation.Status == PosReservationStatus.Booked || reservation.Status == PosReservationStatus.Confirmed)
        {
            reservation.Status = PosReservationStatus.Seated;
        }

        await _reservationRepository.UpdateReservationAsync(reservation, ct);
        await _reservationRepository.SaveChangesAsync(ct);

        return ApiResponse<ReservationSummaryDto>.CreateSuccess(
            MapToSummary(reservation),
            $"تم ربط الطلب {reservation.OrderNumber} بنجاح بالحجز رقم {reservation.ReservationNumber}");
    }

    public async Task<ApiResponse<bool>> UpdateReservationStatusAsync(long reservationId, PosReservationStatus status, CancellationToken ct = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, ct);
        if (reservation == null)
            return ApiResponse<bool>.CreateFailure("Reservation not found", null, 404);

        reservation.Status = status;
        await _reservationRepository.UpdateReservationAsync(reservation, ct);
        await _reservationRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Reservation status updated");
    }

    public async Task<ApiResponse<bool>> DeleteReservationAsync(long id, CancellationToken ct = default)
    {
        var reservation = await _reservationRepository.GetByIdAsync(id, ct);
        if (reservation == null)
            return ApiResponse<bool>.CreateFailure("Reservation not found", null, 404);

        await _reservationRepository.DeleteReservationAsync(reservation, ct);
        await _reservationRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Reservation deleted successfully");
    }

    public async Task<ApiResponse<AvailableSlotsResultDto>> GetAvailableSlotsAsync(
        long branchId,
        DateTime date,
        long? staffEmployeeId = null,
        long? tableId = null,
        string? resourceType = null,
        int durationMinutes = 30,
        int bufferMinutes = 10,
        TimeSpan? openingTime = null,
        TimeSpan? closingTime = null,
        CancellationToken ct = default)
    {
        if (branchId <= 0)
            return ApiResponse<AvailableSlotsResultDto>.CreateFailure("Valid BranchId is required", null, 400);

        if (durationMinutes <= 0) durationMinutes = 30;
        if (bufferMinutes < 0) bufferMinutes = 0;

        var open = openingTime ?? new TimeSpan(9, 0, 0);   // Default 09:00 AM
        var close = closingTime ?? new TimeSpan(23, 0, 0); // Default 11:00 PM

        var dayReservations = await _reservationRepository.GetReservationsForDayAsync(branchId, date, ct);
        var activeReservations = dayReservations.Where(r =>
            r.Status == PosReservationStatus.Booked ||
            r.Status == PosReservationStatus.Confirmed ||
            r.Status == PosReservationStatus.Seated).ToList();

        // Filter active reservations if staff, table, or resourceType is specified
        if (staffEmployeeId.HasValue)
        {
            activeReservations = activeReservations
                .Where(r => GetAllStaffIdsInReservation(r).Contains(staffEmployeeId.Value))
                .ToList();
        }
        else if (tableId.HasValue)
        {
            activeReservations = activeReservations.Where(r => r.TableId == tableId.Value).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(resourceType))
        {
            activeReservations = activeReservations.Where(r => string.Equals(r.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var slots = new List<TimeSlotDto>();
        var slotDuration = TimeSpan.FromMinutes(durationMinutes);
        var current = open;

        while (current + slotDuration <= close)
        {
            var slotEnd = current + slotDuration;

            // Conflict check with active reservations
            var conflict = activeReservations.FirstOrDefault(r => current < r.EndTime && slotEnd > r.StartTime);

            if (conflict == null)
            {
                slots.Add(new TimeSlotDto
                {
                    StartTime = current,
                    EndTime = slotEnd,
                    FormattedStartTime = current.ToString(@"hh\:mm"),
                    FormattedEndTime = slotEnd.ToString(@"hh\:mm"),
                    IsAvailable = true
                });
            }
            else
            {
                slots.Add(new TimeSlotDto
                {
                    StartTime = current,
                    EndTime = slotEnd,
                    FormattedStartTime = current.ToString(@"hh\:mm"),
                    FormattedEndTime = slotEnd.ToString(@"hh\:mm"),
                    IsAvailable = false,
                    Reason = $"محجوز ({conflict.CustomerName})",
                    ExistingReservationNumber = conflict.ReservationNumber,
                    CustomerName = conflict.CustomerName
                });
            }

            current = current.Add(slotDuration);
        }

        var result = new AvailableSlotsResultDto
        {
            BranchId = branchId,
            Date = date.Date,
            SlotDurationMinutes = durationMinutes,
            BufferMinutes = bufferMinutes,
            StaffEmployeeId = staffEmployeeId,
            TableId = tableId,
            ResourceType = resourceType,
            Slots = slots
        };

        return ApiResponse<AvailableSlotsResultDto>.CreateSuccess(result);
    }

    public Task<ApiResponse<IReadOnlyList<PosResourceTypeDto>>> GetResourceTypesAsync(CancellationToken ct = default)
    {
        var list = new List<PosResourceTypeDto>
        {
            // Salon
            new() { Code = "Chair", NameAr = "كرسي حلاقة / تصفيف", NameEn = "Salon Chair / Station", Category = "Salon", RequiresTable = false, RequiresStaff = true, IsExternal = false, Description = "كرسي أو محطة عمل داخل الصالون" },
            new() { Code = "Room", NameAr = "غرفة سبا / VIP", NameEn = "Spa / VIP Room", Category = "Salon", RequiresTable = false, RequiresStaff = true, IsExternal = false, Description = "غرفة خدمات خاصة داخل المركز" },
            new() { Code = "HomeService", NameAr = "خدمة منزلية / خارجية", NameEn = "Home / Outdoor Service", Category = "Salon", RequiresTable = false, RequiresStaff = true, IsExternal = true, Description = "زيارة خارجية أو منزلية لموقع العميل" },

            // Restaurant / Hospitality
            new() { Code = "Table", NameAr = "طاولة صالة", NameEn = "Dining Table", Category = "Restaurant", RequiresTable = true, RequiresStaff = false, IsExternal = false, Description = "طاولة عادية في صالة المطعم" },
            new() { Code = "Cabin", NameAr = "كابينة عائلية", NameEn = "Family Cabin", Category = "Restaurant", RequiresTable = true, RequiresStaff = false, IsExternal = false, Description = "جلسة عائلية بخصوصية" },
            new() { Code = "Outdoor", NameAr = "جلسة خارجية / تراس", NameEn = "Outdoor Terrace", Category = "Restaurant", RequiresTable = true, RequiresStaff = false, IsExternal = false, Description = "جلسة في الهواء الطلق أو التراس" },
            new() { Code = "VIPLounge", NameAr = "صالة كبار الشخصيات", NameEn = "VIP Lounge", Category = "Restaurant", RequiresTable = true, RequiresStaff = false, IsExternal = false, Description = "صالة أو قاعة خاصة للمناسبات" },

            // Sports / Entertainment
            new() { Code = "Court", NameAr = "ملعب رياضي (بادل / تنس)", NameEn = "Sports Court", Category = "Sports", RequiresTable = false, RequiresStaff = false, IsExternal = false, Description = "ملعب رياضي محجوز بالساعة" },
            new() { Code = "Lane", NameAr = "مسار بولينغ", NameEn = "Bowling Lane", Category = "Sports", RequiresTable = false, RequiresStaff = false, IsExternal = false, Description = "مسار بولينغ أو ألعاب" },

            // Medical
            new() { Code = "Clinic", NameAr = "عيادة كشف", NameEn = "Consultation Clinic", Category = "Medical", RequiresTable = false, RequiresStaff = true, IsExternal = false, Description = "عيادة فحص واستشارة طبية" },
            new() { Code = "HomeVisit", NameAr = "زيارة منزلية طبية", NameEn = "Medical Home Visit", Category = "Medical", RequiresTable = false, RequiresStaff = true, IsExternal = true, Description = "كشف أو رعاية طبية منزلية" }
        };

        return Task.FromResult(ApiResponse<IReadOnlyList<PosResourceTypeDto>>.CreateSuccess(list));
    }

    public async Task<ApiResponse<RecurringReservationResultDto>> CreateRecurringReservationAsync(CreateRecurringReservationDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<RecurringReservationResultDto>.CreateFailure("Request body cannot be null", null, 400);

        if (string.IsNullOrWhiteSpace(dto.CustomerName) || string.IsNullOrWhiteSpace(dto.CustomerPhone))
            return ApiResponse<RecurringReservationResultDto>.CreateFailure("Customer name and phone are required", null, 400);

        if (dto.BranchId <= 0)
            return ApiResponse<RecurringReservationResultDto>.CreateFailure("Valid BranchId is required", null, 400);

        if (dto.Services != null && dto.Services.Count > 0)
        {
            var prep = PrepareReservationServices(dto.StartTime, dto.Services);
            dto.EndTime = prep.EndTime;
        }

        if (dto.EndTime <= dto.StartTime)
            return ApiResponse<RecurringReservationResultDto>.CreateFailure("EndTime must be greater than StartTime", null, 400);

        if (dto.Occurrences < 1 || dto.Occurrences > 52)
            return ApiResponse<RecurringReservationResultDto>.CreateFailure("Occurrences must be between 1 and 52", null, 400);

        var isExternal = string.Equals(dto.ResourceType, "HomeService", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(dto.ResourceType, "External", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(dto.ResourceType, "HomeVisit", StringComparison.OrdinalIgnoreCase);

        if (isExternal)
        {
            dto.TableId = null;
            var hasAnyStaff = (dto.StaffEmployeeId.HasValue && dto.StaffEmployeeId.Value > 0) ||
                              (dto.Services != null && dto.Services.Any(s => (s.PrimaryStaffIds != null && s.PrimaryStaffIds.Count > 0) || (s.AssistantStaffIds != null && s.AssistantStaffIds.Count > 0)));
            if (!hasAnyStaff)
            {
                return ApiResponse<RecurringReservationResultDto>.CreateFailure("StaffEmployeeId is required for external / home services", null, 400);
            }
        }

        var recurrenceGroupId = $"REC-{dto.BranchId}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        // Multi-Staff Requested
        var requestedStaff = new HashSet<long>();
        if (dto.StaffEmployeeId.HasValue && dto.StaffEmployeeId.Value > 0)
            requestedStaff.Add(dto.StaffEmployeeId.Value);
        if (dto.AdditionalStaffIds != null && dto.AdditionalStaffIds.Count > 0)
        {
            foreach (var id in dto.AdditionalStaffIds.Where(x => x > 0))
                requestedStaff.Add(id);
        }
        if (dto.Services != null)
        {
            foreach (var s in dto.Services)
            {
                foreach (var id in GetStaffIdsForService(s))
                    requestedStaff.Add(id);
            }
        }

        if ((!dto.StaffEmployeeId.HasValue || dto.StaffEmployeeId.Value <= 0) && requestedStaff.Count > 0)
        {
            dto.StaffEmployeeId = requestedStaff.First();
        }

        // Determine starting date aligned with DayOfWeek if provided
        var currentDate = dto.StartDate.Date;
        if (dto.DayOfWeek.HasValue)
        {
            while (currentDate.DayOfWeek != dto.DayOfWeek.Value)
            {
                currentDate = currentDate.AddDays(1);
            }
        }

        var pattern = dto.RecurrencePattern?.Trim().ToLowerInvariant() ?? "weekly";
        var createdList = new List<PosReservation>();
        var skippedReasons = new List<string>();

        for (int i = 0; i < dto.Occurrences; i++)
        {
            var dateToBook = currentDate;

            // Advance date for next iteration
            currentDate = pattern switch
            {
                "daily" => currentDate.AddDays(1),
                "biweekly" => currentDate.AddDays(14),
                "monthly" => currentDate.AddMonths(1),
                _ => currentDate.AddDays(7) // default weekly
            };

            // Check conflict for dateToBook
            var dayReservations = await _reservationRepository.GetReservationsForDayAsync(dto.BranchId, dateToBook, ct);
            var activeReservations = dayReservations.Where(r =>
                r.Status == PosReservationStatus.Booked ||
                r.Status == PosReservationStatus.Confirmed ||
                r.Status == PosReservationStatus.Seated).ToList();

            var conflictMessage = CheckReservationConflict(
                dto.StartTime,
                dto.EndTime,
                dto.TableId,
                dto.ResourceType,
                dto.ResourceIdentifier,
                dto.Services,
                requestedStaff,
                activeReservations);

            if (!string.IsNullOrWhiteSpace(conflictMessage))
            {
                skippedReasons.Add($"تاريخ {dateToBook:yyyy-MM-dd}: {conflictMessage}");
                continue;
            }

            var dateStr = dateToBook.ToString("yyyyMMdd");
            var resNumber = $"RES-{dto.BranchId}-{dateStr}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

            List<PosReservationServiceItem> occServices = new();
            if (dto.Services != null && dto.Services.Count > 0)
            {
                var prep = PrepareReservationServices(dto.StartTime, dto.Services);
                occServices = prep.Entities;
            }

            var reservation = new PosReservation
            {
                BranchId = dto.BranchId,
                ReservationNumber = resNumber,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName.Trim(),
                CustomerPhone = dto.CustomerPhone.Trim(),
                CustomerEmail = dto.CustomerEmail?.Trim(),
                ReservationDate = dateToBook,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                GuestCount = dto.GuestCount > 0 ? dto.GuestCount : 1,
                TableId = dto.TableId,
                ResourceType = dto.ResourceType ?? "Chair",
                ResourceIdentifier = dto.ResourceIdentifier,
                StaffEmployeeId = dto.StaffEmployeeId,
                AdditionalStaffIds = FormatStaffIds(dto.AdditionalStaffIds),
                Status = PosReservationStatus.Booked,
                DepositAmount = dto.DepositAmountPerSession,
                IsDepositPaid = dto.IsDepositPaid,
                SpecialRequests = dto.SpecialRequests,
                RecurrenceGroupId = recurrenceGroupId,
                IsRecurring = true,
                CreationUser = username,
                CreationDate = DateTime.UtcNow,
                Services = occServices
            };

            await _reservationRepository.AddReservationAsync(reservation, ct);
            createdList.Add(reservation);
        }

        if (createdList.Count > 0)
        {
            await _reservationRepository.SaveChangesAsync(ct);
        }

        var result = new RecurringReservationResultDto
        {
            RecurrenceGroupId = recurrenceGroupId,
            RequestedOccurrences = dto.Occurrences,
            SuccessfulBookingsCount = createdList.Count,
            CreatedReservations = createdList.Select(MapToSummary).ToList(),
            SkippedDatesReasons = skippedReasons
        };

        var message = createdList.Count == dto.Occurrences
            ? $"تم حجز جميع المواعيد الـ {createdList.Count} بنجاح ضمن السلسلة {recurrenceGroupId}"
            : $"تم حجز {createdList.Count} من أصل {dto.Occurrences} موعد، وتخطي {skippedReasons.Count} بسبب تعارض المواعيد.";

        return ApiResponse<RecurringReservationResultDto>.CreateSuccess(result, message);
    }

    public async Task<ApiResponse<bool>> CancelRecurringSeriesAsync(string recurrenceGroupId, string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recurrenceGroupId))
            return ApiResponse<bool>.CreateFailure("RecurrenceGroupId is required", null, 400);

        var seriesReservations = await _reservationRepository.GetByRecurrenceGroupIdAsync(recurrenceGroupId.Trim(), ct);
        if (seriesReservations.Count == 0)
            return ApiResponse<bool>.CreateFailure($"No reservations found for recurrence group '{recurrenceGroupId}'", null, 404);

        int cancelledCount = 0;
        foreach (var r in seriesReservations)
        {
            if (r.Status != PosReservationStatus.Cancelled && r.Status != PosReservationStatus.Seated)
            {
                r.Status = PosReservationStatus.Cancelled;
                await _reservationRepository.UpdateReservationAsync(r, ct);
                cancelledCount++;
            }
        }

        if (cancelledCount > 0)
        {
            await _reservationRepository.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.CreateSuccess(true, $"Successfully cancelled {cancelledCount} reservations in recurring series {recurrenceGroupId}");
    }

    private static ReservationSummaryDto MapToSummary(PosReservation r)
    {
        return new ReservationSummaryDto
        {
            Id = r.Id,
            ReservationNumber = r.ReservationNumber,
            CustomerId = r.CustomerId,
            OrderId = r.OrderId,
            OrderNumber = r.OrderNumber,
            CustomerName = r.CustomerName,
            CustomerPhone = r.CustomerPhone,
            CustomerEmail = r.CustomerEmail,
            ReservationDate = r.ReservationDate,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            GuestCount = r.GuestCount,
            TableId = r.TableId,
            TableNumber = r.Table?.TableNumber,
            ResourceType = r.ResourceType,
            ResourceIdentifier = r.ResourceIdentifier,
            StaffEmployeeId = r.StaffEmployeeId,
            AdditionalStaffIds = ParseStaffIds(r.AdditionalStaffIds),
            Status = r.Status,
            DepositAmount = r.DepositAmount,
            IsDepositPaid = r.IsDepositPaid,
            SpecialRequests = r.SpecialRequests,
            RecurrenceGroupId = r.RecurrenceGroupId,
            IsRecurring = r.IsRecurring,
            Services = r.Services?.OrderBy(s => s.SortOrder).Select(s => new ReservationServiceItemDto
            {
                Id = s.Id,
                ServiceItemId = s.ServiceItemId,
                ServiceName = s.ServiceName,
                DurationMinutes = s.DurationMinutes,
                DelayMinutes = s.DelayMinutes,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                PrimaryStaffIds = ParseStaffIds(s.PrimaryStaffIds),
                AssistantStaffIds = ParseStaffIds(s.AssistantStaffIds),
                Price = s.Price,
                Notes = s.Notes,
                SortOrder = s.SortOrder
            }).ToList() ?? new List<ReservationServiceItemDto>()
        };
    }

    private static (TimeSpan EndTime, List<PosReservationServiceItem> Entities) PrepareReservationServices(
        TimeSpan baseStartTime,
        List<ReservationServiceItemDto>? serviceDtos)
    {
        if (serviceDtos == null || serviceDtos.Count == 0)
            return (baseStartTime, new List<PosReservationServiceItem>());

        var list = new List<PosReservationServiceItem>();
        var currentTime = baseStartTime;

        for (int i = 0; i < serviceDtos.Count; i++)
        {
            var s = serviceDtos[i];
            var duration = s.DurationMinutes > 0 ? s.DurationMinutes : 30;
            var delay = s.DelayMinutes >= 0 ? s.DelayMinutes : 0;

            var sStart = s.StartTime ?? currentTime;
            var sEnd = s.EndTime ?? (sStart + TimeSpan.FromMinutes(duration));
            if (sEnd <= sStart)
            {
                sEnd = sStart + TimeSpan.FromMinutes(duration);
            }

            s.StartTime = sStart;
            s.EndTime = sEnd;
            s.DurationMinutes = duration;
            s.DelayMinutes = delay;

            var entity = new PosReservationServiceItem
            {
                ServiceItemId = s.ServiceItemId,
                ServiceName = !string.IsNullOrWhiteSpace(s.ServiceName) ? s.ServiceName.Trim() : $"Service #{i + 1}",
                DurationMinutes = duration,
                DelayMinutes = delay,
                StartTime = sStart,
                EndTime = sEnd,
                PrimaryStaffIds = FormatStaffIds(s.PrimaryStaffIds),
                AssistantStaffIds = FormatStaffIds(s.AssistantStaffIds),
                Price = s.Price,
                Notes = s.Notes?.Trim(),
                SortOrder = s.SortOrder > 0 ? s.SortOrder : (i + 1)
            };

            list.Add(entity);
            currentTime = sEnd + TimeSpan.FromMinutes(delay);
        }

        var overallEndTime = list.Count > 0 ? list.Last().EndTime : baseStartTime;
        return (overallEndTime, list);
    }

    private static HashSet<long> GetStaffIdsForService(ReservationServiceItemDto service)
    {
        var set = new HashSet<long>();
        if (service.PrimaryStaffIds != null)
        {
            foreach (var id in service.PrimaryStaffIds.Where(x => x > 0))
                set.Add(id);
        }

        if (service.AssistantStaffIds != null)
        {
            foreach (var id in service.AssistantStaffIds.Where(x => x > 0))
                set.Add(id);
        }

        return set;
    }

    private static HashSet<long> GetStaffIdsForService(PosReservationServiceItem service)
    {
        var set = new HashSet<long>();
        foreach (var id in ParseStaffIds(service.PrimaryStaffIds))
            set.Add(id);

        foreach (var id in ParseStaffIds(service.AssistantStaffIds))
            set.Add(id);

        return set;
    }

    private static string? CheckReservationConflict(
        TimeSpan reqStartTime,
        TimeSpan reqEndTime,
        long? tableId,
        string? resourceType,
        string? resourceIdentifier,
        List<ReservationServiceItemDto>? services,
        HashSet<long> requestedStaff,
        List<PosReservation> activeReservations,
        long? excludeReservationId = null)
    {
        foreach (var r in activeReservations)
        {
            if (excludeReservationId.HasValue && r.Id == excludeReservationId.Value)
                continue;

            // 1. Table conflict
            if (tableId.HasValue && r.TableId.HasValue && tableId.Value == r.TableId.Value)
            {
                if (reqStartTime < r.EndTime && reqEndTime > r.StartTime)
                {
                    return $"الوقت المحدد يتعارض مع حجز قائم للطاولة (رقم الحجز: {r.ReservationNumber} للعميل: {r.CustomerName} من {r.StartTime:hh\\:mm} إلى {r.EndTime:hh\\:mm})";
                }
            }

            // 2. Resource Identifier conflict
            if (!string.IsNullOrWhiteSpace(resourceIdentifier) && !string.IsNullOrWhiteSpace(r.ResourceIdentifier) &&
                string.Equals(resourceIdentifier.Trim(), r.ResourceIdentifier.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(resourceType?.Trim(), r.ResourceType?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (reqStartTime < r.EndTime && reqEndTime > r.StartTime)
                {
                    return $"الوقت المحدد يتعارض مع حجز قائم للمورد {resourceIdentifier} (رقم الحجز: {r.ReservationNumber} للعميل: {r.CustomerName} من {r.StartTime:hh\\:mm} إلى {r.EndTime:hh\\:mm})";
                }
            }

            // 3. Staff Conflicts
            if (services != null && services.Count > 0)
            {
                foreach (var s in services)
                {
                    var sStaff = GetStaffIdsForService(s);
                    if (sStaff.Count == 0) continue;
                    var sStart = s.StartTime ?? reqStartTime;
                    var sEnd = s.EndTime ?? reqEndTime;

                    if (r.Services != null && r.Services.Count > 0)
                    {
                        foreach (var rs in r.Services)
                        {
                            if (sStart < rs.EndTime && sEnd > rs.StartTime)
                            {
                                var rsStaff = GetStaffIdsForService(rs);
                                var overlap = sStaff.Intersect(rsStaff).FirstOrDefault();
                                if (overlap > 0)
                                {
                                    return $"الموظف (كود: {overlap}) مشغول في خدمة \"{rs.ServiceName}\" من {rs.StartTime:hh\\:mm} إلى {rs.EndTime:hh\\:mm} في حجز ({r.ReservationNumber} - {r.CustomerName}) ولا يمكن حجز خدمة \"{s.ServiceName}\" له في نفس التوقيت ({sStart:hh\\:mm} - {sEnd:hh\\:mm})";
                                }
                            }
                        }
                    }
                    else
                    {
                        if (sStart < r.EndTime && sEnd > r.StartTime)
                        {
                            var rStaff = GetAllStaffIdsInReservation(r);
                            var overlap = sStaff.Intersect(rStaff).FirstOrDefault();
                            if (overlap > 0)
                            {
                                return $"الموظف (كود: {overlap}) لديه حجز قائم ({r.ReservationNumber} - {r.CustomerName}) من {r.StartTime:hh\\:mm} إلى {r.EndTime:hh\\:mm}";
                            }
                        }
                    }
                }
            }
            else if (requestedStaff.Count > 0)
            {
                if (reqStartTime < r.EndTime && reqEndTime > r.StartTime)
                {
                    if (r.Services != null && r.Services.Count > 0)
                    {
                        foreach (var rs in r.Services)
                        {
                            if (reqStartTime < rs.EndTime && reqEndTime > rs.StartTime)
                            {
                                var rsStaff = GetStaffIdsForService(rs);
                                var overlap = requestedStaff.Intersect(rsStaff).FirstOrDefault();
                                if (overlap > 0)
                                {
                                    return $"الموظف (كود: {overlap}) مشغول في خدمة \"{rs.ServiceName}\" من {rs.StartTime:hh\\:mm} إلى {rs.EndTime:hh\\:mm} في حجز ({r.ReservationNumber} - {r.CustomerName})";
                                }
                            }
                        }
                    }
                    else
                    {
                        var rStaff = GetAllStaffIdsInReservation(r);
                        var overlap = requestedStaff.Intersect(rStaff).FirstOrDefault();
                        if (overlap > 0)
                        {
                            return $"الموظف (كود: {overlap}) لديه حجز قائم ({r.ReservationNumber} - {r.CustomerName}) من {r.StartTime:hh\\:mm} إلى {r.EndTime:hh\\:mm}";
                        }
                    }
                }
            }
        }

        return null;
    }

    private static List<long> ParseStaffIds(string? commaSeparated)
    {
        if (string.IsNullOrWhiteSpace(commaSeparated)) return new List<long>();
        return commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static string? FormatStaffIds(List<long>? staffIds)
    {
        if (staffIds == null || staffIds.Count == 0) return null;
        var valid = staffIds.Where(id => id > 0).Distinct().ToList();
        return valid.Count > 0 ? string.Join(",", valid) : null;
    }

    private static HashSet<long> GetAllStaffIdsInReservation(PosReservation res)
    {
        var set = new HashSet<long>();
        if (res.StaffEmployeeId.HasValue && res.StaffEmployeeId.Value > 0)
            set.Add(res.StaffEmployeeId.Value);

        foreach (var id in ParseStaffIds(res.AdditionalStaffIds))
            set.Add(id);

        if (res.Services != null)
        {
            foreach (var s in res.Services)
            {
                foreach (var id in ParseStaffIds(s.PrimaryStaffIds))
                    set.Add(id);

                foreach (var id in ParseStaffIds(s.AssistantStaffIds))
                    set.Add(id);
            }
        }

        return set;
    }
}
