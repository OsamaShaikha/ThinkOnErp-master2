using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosGiftCardService
{
    Task<ApiResponse<GiftCardSummaryDto>> CreateGiftCardAsync(CreateGiftCardDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<GiftCardSummaryDto>> GetCardBalanceAsync(long branchId, string cardCode, CancellationToken ct = default);
    Task<ApiResponse<GiftCardDetailsDto>> GetGiftCardByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<GiftCardSummaryDto>>> GetGiftCardsPagedAsync(
        long branchId,
        string? cardCode = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<ApiResponse<GiftCardSummaryDto>> UpdateGiftCardAsync(long id, UpdateGiftCardDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteGiftCardAsync(long id, string username, CancellationToken ct = default);
    Task<ApiResponse<GiftCardSummaryDto>> RedeemCardAsync(RedeemGiftCardDto dto, string username, CancellationToken ct = default);
}

public class PosGiftCardService : IPosGiftCardService
{
    private readonly IPosGiftCardRepository _giftCardRepository;
    private readonly IPosAuditService _auditService;

    public PosGiftCardService(
        IPosGiftCardRepository giftCardRepository,
        IPosAuditService auditService)
    {
        _giftCardRepository = giftCardRepository;
        _auditService = auditService;
    }

    public async Task<ApiResponse<GiftCardSummaryDto>> CreateGiftCardAsync(CreateGiftCardDto dto, string username, CancellationToken ct = default)
    {
        var existing = await _giftCardRepository.GetByCodeAsync(dto.BranchId, dto.CardCode, ct);
        if (existing != null)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Card code already exists", null, 400);

        var card = new PosGiftCard
        {
            BranchId = dto.BranchId,
            CardCode = dto.CardCode.Trim().ToUpper(),
            PinHash = !string.IsNullOrWhiteSpace(dto.Pin) ? dto.Pin : null,
            InitialBalance = dto.InitialBalance,
            CurrentBalance = dto.InitialBalance,
            IssueDate = DateTime.UtcNow,
            ExpiryDate = dto.ExpiryDate,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _giftCardRepository.AddGiftCardAsync(card, ct);

        if (dto.InitialBalance > 0)
        {
            await _giftCardRepository.AddTransactionAsync(new PosGiftCardTransaction
            {
                GiftCard = card,
                TransactionType = "InitialLoad",
                Amount = dto.InitialBalance,
                BalanceBefore = 0,
                BalanceAfter = dto.InitialBalance,
                TransactionDate = DateTime.UtcNow,
                CreationUser = username
            }, ct);
        }

        await _giftCardRepository.SaveChangesAsync(ct);

        if (dto.InitialBalance > 0)
        {
            await _auditService.LogGiftCardTransactionAsync(
                card.BranchId,
                card.Id,
                card.CardCode,
                "InitialLoad",
                dto.InitialBalance,
                card.CurrentBalance,
                username,
                ct);
        }

        return ApiResponse<GiftCardSummaryDto>.CreateSuccess(MapToSummary(card), "Gift card created successfully");
    }

    public async Task<ApiResponse<GiftCardSummaryDto>> GetCardBalanceAsync(long branchId, string cardCode, CancellationToken ct = default)
    {
        var card = await _giftCardRepository.GetByCodeAsync(branchId, cardCode, ct);
        if (card == null)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Gift card not found", null, 404);

        if (!card.IsActive)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Gift card is inactive or blocked", null, 400);

        if (card.ExpiryDate.HasValue && card.ExpiryDate.Value < DateTime.UtcNow)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Gift card has expired", null, 400);

        return ApiResponse<GiftCardSummaryDto>.CreateSuccess(MapToSummary(card));
    }

    public async Task<ApiResponse<GiftCardDetailsDto>> GetGiftCardByIdAsync(long id, CancellationToken ct = default)
    {
        var card = await _giftCardRepository.GetByIdAsync(id, ct);
        if (card == null)
            return ApiResponse<GiftCardDetailsDto>.CreateFailure("Gift card not found", null, 404);

        var dto = new GiftCardDetailsDto
        {
            Id = card.Id,
            BranchId = card.BranchId,
            CardCode = card.CardCode,
            InitialBalance = card.InitialBalance,
            CurrentBalance = card.CurrentBalance,
            IssueDate = card.IssueDate,
            ExpiryDate = card.ExpiryDate,
            IsActive = card.IsActive,
            Transactions = card.Transactions.Select(t => new GiftCardTransactionDto
            {
                Id = t.Id,
                GiftCardId = t.GiftCardId,
                OrderId = t.OrderId,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                BalanceBefore = t.BalanceBefore,
                BalanceAfter = t.BalanceAfter,
                TransactionDate = t.TransactionDate,
                CreationUser = t.CreationUser
            }).OrderByDescending(t => t.TransactionDate).ToList()
        };

        return ApiResponse<GiftCardDetailsDto>.CreateSuccess(dto);
    }

    public async Task<ApiResponse<PagedResultDto<GiftCardSummaryDto>>> GetGiftCardsPagedAsync(
        long branchId,
        string? cardCode = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _giftCardRepository.GetGiftCardsPagedAsync(
            branchId, cardCode, isActive, pageIndex, pageSize, ct);

        var dtos = items.Select(MapToSummary).ToList();
        var paged = new PagedResultDto<GiftCardSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<GiftCardSummaryDto>>.CreateSuccess(paged);
    }

    public async Task<ApiResponse<GiftCardSummaryDto>> UpdateGiftCardAsync(long id, UpdateGiftCardDto dto, string username, CancellationToken ct = default)
    {
        var card = await _giftCardRepository.GetByIdAsync(id, ct);
        if (card == null)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Gift card not found", null, 404);

        card.ExpiryDate = dto.ExpiryDate;
        card.IsActive = dto.IsActive;

        await _giftCardRepository.UpdateGiftCardAsync(card, ct);
        await _giftCardRepository.SaveChangesAsync(ct);

        return ApiResponse<GiftCardSummaryDto>.CreateSuccess(MapToSummary(card), "Gift card updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteGiftCardAsync(long id, string username, CancellationToken ct = default)
    {
        var card = await _giftCardRepository.GetByIdAsync(id, ct);
        if (card == null)
            return ApiResponse<bool>.CreateFailure("Gift card not found", null, 404);

        card.IsActive = false;
        await _giftCardRepository.UpdateGiftCardAsync(card, ct);
        await _giftCardRepository.SaveChangesAsync(ct);

        await _auditService.LogGiftCardTransactionAsync(
            card.BranchId,
            card.Id,
            card.CardCode,
            "Deactivated",
            0,
            card.CurrentBalance,
            username,
            ct);

        return ApiResponse<bool>.CreateSuccess(true, "Gift card deactivated successfully");
    }

    public async Task<ApiResponse<GiftCardSummaryDto>> RedeemCardAsync(RedeemGiftCardDto dto, string username, CancellationToken ct = default)
    {
        var card = await _giftCardRepository.GetByCodeAsync(dto.BranchId, dto.CardCode, ct);
        if (card == null)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Gift card not found", null, 404);

        if (!card.IsActive)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Gift card is inactive", null, 400);

        if (card.ExpiryDate.HasValue && card.ExpiryDate.Value < DateTime.UtcNow)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure("Gift card has expired", null, 400);

        if (card.CurrentBalance < dto.Amount)
            return ApiResponse<GiftCardSummaryDto>.CreateFailure($"Insufficient card balance. Available: {card.CurrentBalance:F2}", null, 400);

        var balanceBefore = card.CurrentBalance;
        card.CurrentBalance -= dto.Amount;

        var tx = new PosGiftCardTransaction
        {
            GiftCardId = card.Id,
            OrderId = dto.OrderId,
            TransactionType = "Redeem",
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = card.CurrentBalance,
            TransactionDate = DateTime.UtcNow,
            CreationUser = username
        };

        await _giftCardRepository.AddTransactionAsync(tx, ct);
        await _giftCardRepository.SaveChangesAsync(ct);

        await _auditService.LogGiftCardTransactionAsync(
            card.BranchId,
            card.Id,
            card.CardCode,
            "Redeem",
            dto.Amount,
            card.CurrentBalance,
            username,
            ct);

        return ApiResponse<GiftCardSummaryDto>.CreateSuccess(MapToSummary(card), "Gift card redeemed successfully");
    }

    private static GiftCardSummaryDto MapToSummary(PosGiftCard card)
    {
        return new GiftCardSummaryDto
        {
            Id = card.Id,
            BranchId = card.BranchId,
            CardCode = card.CardCode,
            InitialBalance = card.InitialBalance,
            CurrentBalance = card.CurrentBalance,
            IssueDate = card.IssueDate,
            ExpiryDate = card.ExpiryDate,
            IsActive = card.IsActive
        };
    }
}
