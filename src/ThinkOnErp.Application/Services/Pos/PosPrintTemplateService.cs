using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosPrintTemplateService
{
    // Templates
    Task<ApiResponse<IReadOnlyList<PrintTemplateDto>>> GetTemplatesByBranchAsync(long branchId, string? templateType = null, CancellationToken ct = default);
    Task<ApiResponse<PrintTemplateDto>> GetTemplateByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PrintTemplateDto>> CreateTemplateAsync(CreatePrintTemplateDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PrintTemplateDto>> UpdateTemplateAsync(long id, UpdatePrintTemplateDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteTemplateAsync(long id, CancellationToken ct = default);

    // Routing
    Task<ApiResponse<IReadOnlyList<PrinterRoutingDto>>> GetRoutingsByBranchAsync(long branchId, CancellationToken ct = default);
    Task<ApiResponse<PrinterRoutingDto>> GetRoutingByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PrinterRoutingDto>> CreateRoutingAsync(CreatePrinterRoutingDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PrinterRoutingDto>> UpdateRoutingAsync(long id, UpdatePrinterRoutingDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteRoutingAsync(long id, CancellationToken ct = default);
}

public class PosPrintTemplateService : IPosPrintTemplateService
{
    private readonly IPosPrintTemplateRepository _repository;

    public PosPrintTemplateService(IPosPrintTemplateRepository repository)
    {
        _repository = repository;
    }

    // Templates
    public async Task<ApiResponse<IReadOnlyList<PrintTemplateDto>>> GetTemplatesByBranchAsync(long branchId, string? templateType = null, CancellationToken ct = default)
    {
        var templates = await _repository.GetTemplatesByBranchAsync(branchId, templateType, ct);
        var dtos = templates.Select(t => new PrintTemplateDto
        {
            Id = t.Id,
            BranchId = t.BranchId,
            TemplateCode = t.TemplateCode,
            TemplateName = t.TemplateName,
            TemplateType = t.TemplateType,
            RawEscPosPattern = t.RawEscPosPattern,
            IsDefault = t.IsDefault,
            IsActive = t.IsActive
        }).ToList();

        return ApiResponse<IReadOnlyList<PrintTemplateDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<PrintTemplateDto>> GetTemplateByIdAsync(long id, CancellationToken ct = default)
    {
        var t = await _repository.GetTemplateByIdAsync(id, ct);
        if (t == null)
            return ApiResponse<PrintTemplateDto>.CreateFailure("Print template not found", null, 404);

        return ApiResponse<PrintTemplateDto>.CreateSuccess(new PrintTemplateDto
        {
            Id = t.Id,
            BranchId = t.BranchId,
            TemplateCode = t.TemplateCode,
            TemplateName = t.TemplateName,
            TemplateType = t.TemplateType,
            RawEscPosPattern = t.RawEscPosPattern,
            IsDefault = t.IsDefault,
            IsActive = t.IsActive
        });
    }

    public async Task<ApiResponse<PrintTemplateDto>> CreateTemplateAsync(CreatePrintTemplateDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<PrintTemplateDto>.CreateFailure("Request body cannot be null", null, 400);

        if (string.IsNullOrWhiteSpace(dto.TemplateCode) || string.IsNullOrWhiteSpace(dto.TemplateName))
            return ApiResponse<PrintTemplateDto>.CreateFailure("Template code and template name are required", null, 400);

        if (dto.BranchId <= 0)
            return ApiResponse<PrintTemplateDto>.CreateFailure("Valid BranchId is required", null, 400);

        var template = new PosPrintTemplate
        {
            BranchId = dto.BranchId,
            TemplateCode = dto.TemplateCode.Trim().ToUpper(),
            TemplateName = dto.TemplateName.Trim(),
            TemplateType = dto.TemplateType?.Trim() ?? "Receipt",
            RawEscPosPattern = dto.RawEscPosPattern ?? string.Empty,
            IsDefault = dto.IsDefault,
            IsActive = true
        };

        await _repository.AddTemplateAsync(template, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<PrintTemplateDto>.CreateSuccess(new PrintTemplateDto
        {
            Id = template.Id,
            BranchId = template.BranchId,
            TemplateCode = template.TemplateCode,
            TemplateName = template.TemplateName,
            TemplateType = template.TemplateType,
            RawEscPosPattern = template.RawEscPosPattern,
            IsDefault = template.IsDefault,
            IsActive = template.IsActive
        }, "Print template created successfully");
    }

    public async Task<ApiResponse<PrintTemplateDto>> UpdateTemplateAsync(long id, UpdatePrintTemplateDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<PrintTemplateDto>.CreateFailure("Request body cannot be null", null, 400);

        var template = await _repository.GetTemplateByIdAsync(id, ct);
        if (template == null)
            return ApiResponse<PrintTemplateDto>.CreateFailure("Print template not found", null, 404);

        if (!string.IsNullOrWhiteSpace(dto.TemplateName))
            template.TemplateName = dto.TemplateName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.TemplateType))
            template.TemplateType = dto.TemplateType.Trim();
        if (dto.RawEscPosPattern != null)
            template.RawEscPosPattern = dto.RawEscPosPattern;
        template.IsDefault = dto.IsDefault;
        template.IsActive = dto.IsActive;

        await _repository.UpdateTemplateAsync(template, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<PrintTemplateDto>.CreateSuccess(new PrintTemplateDto
        {
            Id = template.Id,
            BranchId = template.BranchId,
            TemplateCode = template.TemplateCode,
            TemplateName = template.TemplateName,
            TemplateType = template.TemplateType,
            RawEscPosPattern = template.RawEscPosPattern,
            IsDefault = template.IsDefault,
            IsActive = template.IsActive
        }, "Print template updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteTemplateAsync(long id, CancellationToken ct = default)
    {
        var template = await _repository.GetTemplateByIdAsync(id, ct);
        if (template == null)
            return ApiResponse<bool>.CreateFailure("Print template not found", null, 404);

        await _repository.DeleteTemplateAsync(template, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Print template deleted successfully");
    }

    // Routing
    public async Task<ApiResponse<IReadOnlyList<PrinterRoutingDto>>> GetRoutingsByBranchAsync(long branchId, CancellationToken ct = default)
    {
        var routings = await _repository.GetRoutingsByBranchAsync(branchId, ct);
        var dtos = routings.Select(r => new PrinterRoutingDto
        {
            Id = r.Id,
            BranchId = r.BranchId,
            StationName = r.StationName,
            PrinterNameOrIp = r.PrinterNameOrIp,
            ItemCategoryId = r.ItemCategoryId,
            ItemGroupId = r.ItemCategoryId,
            ItemCategoryName = r.ItemCategory?.CategoryNameLocal,
            ItemGroupName = r.ItemCategory?.CategoryNameLocal,
            Copies = r.Copies,
            IsActive = r.IsActive
        }).ToList();

        return ApiResponse<IReadOnlyList<PrinterRoutingDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<PrinterRoutingDto>> GetRoutingByIdAsync(long id, CancellationToken ct = default)
    {
        var r = await _repository.GetRoutingByIdAsync(id, ct);
        if (r == null)
            return ApiResponse<PrinterRoutingDto>.CreateFailure("Printer routing not found", null, 404);

        return ApiResponse<PrinterRoutingDto>.CreateSuccess(new PrinterRoutingDto
        {
            Id = r.Id,
            BranchId = r.BranchId,
            StationName = r.StationName,
            PrinterNameOrIp = r.PrinterNameOrIp,
            ItemCategoryId = r.ItemCategoryId,
            ItemGroupId = r.ItemCategoryId,
            ItemCategoryName = r.ItemCategory?.CategoryNameLocal,
            ItemGroupName = r.ItemCategory?.CategoryNameLocal,
            Copies = r.Copies,
            IsActive = r.IsActive
        });
    }

    public async Task<ApiResponse<PrinterRoutingDto>> CreateRoutingAsync(CreatePrinterRoutingDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<PrinterRoutingDto>.CreateFailure("Request body cannot be null", null, 400);

        if (string.IsNullOrWhiteSpace(dto.StationName) || string.IsNullOrWhiteSpace(dto.PrinterNameOrIp))
            return ApiResponse<PrinterRoutingDto>.CreateFailure("Station name and printer name or IP are required", null, 400);

        if (dto.BranchId <= 0)
            return ApiResponse<PrinterRoutingDto>.CreateFailure("Valid BranchId is required", null, 400);

        var routing = new PosPrinterRouting
        {
            BranchId = dto.BranchId,
            StationName = dto.StationName.Trim(),
            PrinterNameOrIp = dto.PrinterNameOrIp.Trim(),
            ItemCategoryId = dto.ItemCategoryId ?? dto.ItemGroupId,
            Copies = dto.Copies > 0 ? dto.Copies : 1,
            IsActive = true
        };

        await _repository.AddRoutingAsync(routing, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<PrinterRoutingDto>.CreateSuccess(new PrinterRoutingDto
        {
            Id = routing.Id,
            BranchId = routing.BranchId,
            StationName = routing.StationName,
            PrinterNameOrIp = routing.PrinterNameOrIp,
            ItemCategoryId = routing.ItemCategoryId,
            ItemGroupId = routing.ItemCategoryId,
            Copies = routing.Copies,
            IsActive = routing.IsActive
        }, "Printer routing created successfully");
    }

    public async Task<ApiResponse<PrinterRoutingDto>> UpdateRoutingAsync(long id, UpdatePrinterRoutingDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<PrinterRoutingDto>.CreateFailure("Request body cannot be null", null, 400);

        var routing = await _repository.GetRoutingByIdAsync(id, ct);
        if (routing == null)
            return ApiResponse<PrinterRoutingDto>.CreateFailure("Printer routing not found", null, 404);

        if (!string.IsNullOrWhiteSpace(dto.StationName))
            routing.StationName = dto.StationName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PrinterNameOrIp))
            routing.PrinterNameOrIp = dto.PrinterNameOrIp.Trim();
        routing.ItemCategoryId = dto.ItemCategoryId ?? dto.ItemGroupId;
        routing.Copies = dto.Copies > 0 ? dto.Copies : 1;
        routing.IsActive = dto.IsActive;

        await _repository.UpdateRoutingAsync(routing, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<PrinterRoutingDto>.CreateSuccess(new PrinterRoutingDto
        {
            Id = routing.Id,
            BranchId = routing.BranchId,
            StationName = routing.StationName,
            PrinterNameOrIp = routing.PrinterNameOrIp,
            ItemCategoryId = routing.ItemCategoryId,
            ItemGroupId = routing.ItemCategoryId,
            Copies = routing.Copies,
            IsActive = routing.IsActive
        }, "Printer routing updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteRoutingAsync(long id, CancellationToken ct = default)
    {
        var routing = await _repository.GetRoutingByIdAsync(id, ct);
        if (routing == null)
            return ApiResponse<bool>.CreateFailure("Printer routing not found", null, 404);

        await _repository.DeleteRoutingAsync(routing, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Printer routing deleted successfully");
    }
}
