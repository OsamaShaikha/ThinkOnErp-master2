using ThinkOnErp.Application.DTOs.Accounting.OpeningBalances;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IOpeningBalanceService
{
    /// <summary>
    /// Creates a new Opening Balance header in Draft status.
    /// Only one OB is allowed per Branch + FiscalYear.
    /// </summary>
    Task<OpeningBalanceHeaderDto> CreateAsync(
        CreateOpeningBalanceDto dto,
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>Returns an Opening Balance by its ID.</summary>
    Task<OpeningBalanceHeaderDto?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the Opening Balance for a specific branch and fiscal year.</summary>
    Task<OpeningBalanceHeaderDto?> GetByFiscalYearAsync(
        long branchId,
        long fiscalYearId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all Opening Balances for a branch (summary list, no details).</summary>
    Task<IReadOnlyList<OpeningBalanceHeaderDto>> GetAllByBranchAsync(
        long branchId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new account line to a Draft Opening Balance.</summary>
    Task<OpeningBalanceHeaderDto> AddLineAsync(
        long headerId,
        OpeningBalanceLineDto line,
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing line in a Draft Opening Balance.</summary>
    Task<OpeningBalanceHeaderDto> UpdateLineAsync(
        long headerId,
        long lineId,
        OpeningBalanceLineDto line,
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a line from a Draft Opening Balance.</summary>
    Task<OpeningBalanceHeaderDto> DeleteLineAsync(
        long headerId,
        long lineId,
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the Opening Balance — locks it and auto-generates an OB journal voucher
    /// (VoucherType=302/OPENING, Status=Posted) in GL_VOUCHER_HEADER/DETAIL.
    /// </summary>
    Task<OpeningBalanceHeaderDto> ConfirmAsync(
        long headerId,
        string username,
        CancellationToken cancellationToken = default);
}
