using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.PostingRules;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/posting-rules")]
[TenantScoped]
[Authorize]
public class PostingRulesController : ControllerBase
{
    private readonly IPostingRuleService _postingRuleService;
    private readonly ILogger<PostingRulesController> _logger;

    public PostingRulesController(IPostingRuleService postingRuleService, ILogger<PostingRulesController> logger)
    {
        _postingRuleService = postingRuleService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PostingRuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PostingRuleDto>>>> GetRules(
        [FromQuery] string? module,
        [FromQuery] long? branchId,
        CancellationToken cancellationToken)
    {
        var rules = await _postingRuleService.GetRulesAsync(module, branchId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PostingRuleDto>>.CreateSuccess(
            rules,
            "تم استرجاع قائمة قواعد الترحيل التلقائي بنجاح",
            200));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PostingRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PostingRuleDto>>> GetRuleById(
        long id,
        CancellationToken cancellationToken)
    {
        var rule = await _postingRuleService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PostingRuleDto>.CreateSuccess(
            rule,
            "تم استرجاع بيانات قاعدة الترحيل بنجاح",
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PostingRuleDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<PostingRuleDto>>> CreateRule(
        [FromBody] CreatePostingRuleDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var rule = await _postingRuleService.CreateRuleAsync(dto, username, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PostingRuleDto>.CreateSuccess(
            rule,
            "تم إنشاء قاعدة الترحيل التلقائي بنجاح",
            201));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<PostingRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PostingRuleDto>>> UpdateRule(
        long id,
        [FromBody] UpdatePostingRuleDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var rule = await _postingRuleService.UpdateRuleAsync(id, dto, username, cancellationToken);
        return Ok(ApiResponse<PostingRuleDto>.CreateSuccess(
            rule,
            "تم تحديث قاعدة الترحيل التلقائي بنجاح",
            200));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteRule(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _postingRuleService.DeleteRuleAsync(id, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "تم حذف قاعدة الترحيل التلقائي بنجاح",
            200));
    }

    [HttpPost("seed-defaults")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> SeedDefaultRules(
        [FromQuery] long? branchId,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        await _postingRuleService.SeedDefaultPostingRulesAsync(branchId, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            true,
            "تم زرع قواعد الترحيل القياسية الافتراضية بنجاح",
            200));
    }

    [HttpPost("post-event")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlVoucherHeaderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GlVoucherHeaderDto>>> PostAutomaticEvent(
        [FromBody] AutomaticPostingEventRequest request,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var voucher = await _postingRuleService.PostAutomaticEventAsync(request, username, cancellationToken);
        return Ok(ApiResponse<GlVoucherHeaderDto>.CreateSuccess(
            voucher,
            "تم ترحيل الحركة تلقائياً وإنشاء القيد المحاسبي بنجاح",
            200));
    }
}
