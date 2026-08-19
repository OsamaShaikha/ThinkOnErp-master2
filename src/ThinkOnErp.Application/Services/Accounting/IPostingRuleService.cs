using ThinkOnErp.Application.DTOs.Accounting.PostingRules;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IPostingRuleService
{
    Task<IReadOnlyList<PostingRuleDto>> GetRulesAsync(string? module, long? branchId, CancellationToken cancellationToken = default);
    Task<PostingRuleDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PostingRuleDto> CreateRuleAsync(CreatePostingRuleDto dto, string username, CancellationToken cancellationToken = default);
    Task<PostingRuleDto> UpdateRuleAsync(long id, UpdatePostingRuleDto dto, string username, CancellationToken cancellationToken = default);
    Task<bool> DeleteRuleAsync(long id, CancellationToken cancellationToken = default);
    Task SeedDefaultPostingRulesAsync(long? branchId, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Core engine method: Resolves rule and executes automatic GL voucher posting.
    /// </summary>
    Task<GlVoucherHeaderDto> PostAutomaticEventAsync(AutomaticPostingEventRequest request, string username, CancellationToken cancellationToken = default);
}
