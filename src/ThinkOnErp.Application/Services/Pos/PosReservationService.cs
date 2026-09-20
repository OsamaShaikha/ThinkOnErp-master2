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
    Task<ApiResponse<ReservationSummaryDto>> UpdateReservationAsync(long id, UpdateReservationDto dto, string username, CancellationToken ct = default);
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

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var resNumber = $"RES-{dto.BranchId}-{today}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

        var reservation = new PosReservation
        {
            BranchId = dto.BranchId,
            ReservationNumber = resNumber,
            CustomerId = dto.CustomerId,
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
            Status = PosReservationStatus.Booked,
            DepositAmount = dto.DepositAmount,
            IsDepositPaid = dto.IsDepositPaid,
            SpecialRequests = dto.SpecialRequests,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
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
        reservation.Status = dto.Status;
        reservation.DepositAmount = dto.DepositAmount;
        reservation.IsDepositPaid = dto.IsDepositPaid;
        reservation.SpecialRequests = dto.SpecialRequests;

        await _reservationRepository.UpdateReservationAsync(reservation, ct);
        await _reservationRepository.SaveChangesAsync(ct);

        return ApiResponse<ReservationSummaryDto>.CreateSuccess(MapToSummary(reservation), "Reservation updated successfully");
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

    private static ReservationSummaryDto MapToSummary(PosReservation r)
    {
        return new ReservationSummaryDto
        {
            Id = r.Id,
            ReservationNumber = r.ReservationNumber,
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
            Status = r.Status,
            DepositAmount = r.DepositAmount,
            IsDepositPaid = r.IsDepositPaid,
            SpecialRequests = r.SpecialRequests
        };
    }
}
