using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosTableRepository
{
    // Floors
    Task<IReadOnlyList<PosFloor>> GetFloorsWithTablesAsync(long branchId, CancellationToken ct = default);
    Task<IReadOnlyList<PosFloor>> GetFloorsByBranchAsync(long branchId, CancellationToken ct = default);
    Task<PosFloor?> GetFloorByIdAsync(long floorId, CancellationToken ct = default);
    Task AddFloorAsync(PosFloor floor, CancellationToken ct = default);
    Task UpdateFloorAsync(PosFloor floor, CancellationToken ct = default);
    Task DeleteFloorAsync(PosFloor floor, CancellationToken ct = default);

    // Tables
    Task<IReadOnlyList<PosTable>> GetTablesByFloorAsync(long floorId, CancellationToken ct = default);
    Task<PosTable?> GetTableByIdAsync(long tableId, CancellationToken ct = default);
    Task AddTableAsync(PosTable table, CancellationToken ct = default);
    Task UpdateTableAsync(PosTable table, CancellationToken ct = default);
    Task DeleteTableAsync(PosTable table, CancellationToken ct = default);
    Task UpdateTableStatusAsync(long tableId, PosTableStatus status, long? activeOrderId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IPosReservationRepository
{
    Task<PosReservation?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<PosReservation>> GetReservationsForDayAsync(long branchId, DateTime date, CancellationToken ct = default);
    Task<(IReadOnlyList<PosReservation> Items, long TotalCount)> GetReservationsPagedAsync(
        long branchId,
        long? customerId = null,
        long? tableId = null,
        PosReservationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task AddReservationAsync(PosReservation reservation, CancellationToken ct = default);
    Task UpdateReservationAsync(PosReservation reservation, CancellationToken ct = default);
    Task DeleteReservationAsync(PosReservation reservation, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
