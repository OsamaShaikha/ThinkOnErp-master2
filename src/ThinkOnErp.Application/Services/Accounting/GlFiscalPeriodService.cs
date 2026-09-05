using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.FiscalPeriods;
using ThinkOnErp.Application.Mappings.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class GlFiscalPeriodService : IGlFiscalPeriodService
{
    private readonly IGlFiscalPeriodRepository _periodRepository;
    private readonly IFiscalYearRepository _fiscalYearRepository;
    private readonly ILogger<GlFiscalPeriodService> _logger;

    private static readonly string[] ArabicMonthNames =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    private static readonly string[] EnglishMonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    public GlFiscalPeriodService(
        IGlFiscalPeriodRepository periodRepository,
        IFiscalYearRepository fiscalYearRepository,
        ILogger<GlFiscalPeriodService> logger)
    {
        _periodRepository = periodRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GlFiscalPeriodDto>> GetPeriodsByFiscalYearIdAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        var periods = await _periodRepository.GetByFiscalYearIdAsync(fiscalYearId, cancellationToken);
        return periods.Select(GlFiscalPeriodMapper.ToDto).ToList();
    }

    public async Task<GlFiscalPeriodDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(id, cancellationToken);
        return period == null ? null : GlFiscalPeriodMapper.ToDto(period);
    }

    public async Task<IReadOnlyList<GlFiscalPeriodDto>> GeneratePeriodsAsync(
        long fiscalYearId,
        bool includeAdjustmentPeriod,
        string username,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _fiscalYearRepository.GetByIdAsync(fiscalYearId);
        if (fiscalYear == null)
        {
            throw new AccountingNotFoundException($"السنة المالية رقم ({fiscalYearId}) غير موجودة.", "FISCAL_YEAR_NOT_FOUND");
        }

        if (fiscalYear.IsClosed)
        {
            throw new AccountingException("لا يمكن توليد فترات مالية لسنة مالية مغلقة.", "FISCAL_YEAR_ALREADY_CLOSED");
        }

        var existingPeriods = await _periodRepository.GetByFiscalYearIdAsync(fiscalYearId, cancellationToken);
        if (existingPeriods.Count > 0)
        {
            // Remove existing periods if none of them are closed
            var anyClosed = existingPeriods.Any(p => p.Status != "OPEN");
            if (anyClosed)
            {
                throw new AccountingException("لا يمكن إعادة توليد الفترات المالية لوجود فترات تم إقفالها مسبقاً في هذه السنة.", "FISCAL_PERIODS_ALREADY_LOCKED");
            }

            await _periodRepository.DeleteRangeAsync(existingPeriods, cancellationToken);
        }

        var generated = new List<GlFiscalPeriod>();
        var currentStart = fiscalYear.StartDate.Date;
        int periodNumber = 1;

        while (currentStart <= fiscalYear.EndDate.Date && periodNumber <= 12)
        {
            // End date of the current month or the fiscal year end date, whichever is earlier
            var lastDayOfMonth = new DateTime(currentStart.Year, currentStart.Month, DateTime.DaysInMonth(currentStart.Year, currentStart.Month));
            var currentEnd = lastDayOfMonth > fiscalYear.EndDate.Date ? fiscalYear.EndDate.Date : lastDayOfMonth;

            var monthIndex = currentStart.Month - 1;
            var arName = monthIndex >= 0 && monthIndex < 12 ? $"{ArabicMonthNames[monthIndex]} {currentStart.Year}" : $"فترة {periodNumber}";
            var enName = monthIndex >= 0 && monthIndex < 12 ? $"{EnglishMonthNames[monthIndex]} {currentStart.Year}" : $"Period {periodNumber}";

            generated.Add(new GlFiscalPeriod
            {
                FiscalYearId = fiscalYearId,
                PeriodNumber = periodNumber++,
                PeriodNameLocal = arName,
                PeriodNameEn = enName,
                StartDate = currentStart,
                EndDate = currentEnd,
                Status = "OPEN",
                IsAdjustment = false,
                CreationUser = username,
                CreationDate = DateTime.UtcNow
            });

            currentStart = currentEnd.AddDays(1);
        }

        // Add 13th adjustment period if requested
        if (includeAdjustmentPeriod)
        {
            generated.Add(new GlFiscalPeriod
            {
                FiscalYearId = fiscalYearId,
                PeriodNumber = 13,
                PeriodNameLocal = $"تسويات نهاية السنة {fiscalYear.EndDate.Year}",
                PeriodNameEn = $"Year-End Adjustments {fiscalYear.EndDate.Year}",
                StartDate = fiscalYear.EndDate.Date,
                EndDate = fiscalYear.EndDate.Date,
                Status = "OPEN",
                IsAdjustment = true,
                CreationUser = username,
                CreationDate = DateTime.UtcNow
            });
        }

        await _periodRepository.AddRangeAsync(generated, cancellationToken);
        await _periodRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated {Count} fiscal periods for fiscal year {FiscalYearId} by user {User}", generated.Count, fiscalYearId, username);

        return generated.Select(GlFiscalPeriodMapper.ToDto).ToList();
    }

    public async Task<GlFiscalPeriodDto> SoftClosePeriodAsync(long id, string username, string? reason, CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(id, cancellationToken);
        if (period == null)
        {
            throw new AccountingNotFoundException($"الفترة المالية رقم ({id}) غير موجودة.", "FISCAL_PERIOD_NOT_FOUND");
        }

        if (period.Status == "HARD_CLOSE")
        {
            throw new AccountingException("لا يمكن عمل إقفال مرن لفترة مقفلة نهائياً (HARD_CLOSE). يجب إعادة فتحها أولاً.", "FISCAL_PERIOD_ALREADY_HARD_CLOSED");
        }

        period.Status = "SOFT_CLOSE";
        period.CloseReason = reason;
        period.ClosedBy = username;
        period.ClosedDate = DateTime.UtcNow;
        period.UpdateUser = username;
        period.UpdateDate = DateTime.UtcNow;

        await _periodRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Fiscal period {PeriodId} ({Name}) soft-closed by {User}", id, period.PeriodNameLocal, username);

        return GlFiscalPeriodMapper.ToDto(period);
    }

    public async Task<GlFiscalPeriodDto> HardClosePeriodAsync(long id, string username, string? reason, CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(id, cancellationToken);
        if (period == null)
        {
            throw new AccountingNotFoundException($"الفترة المالية رقم ({id}) غير موجودة.", "FISCAL_PERIOD_NOT_FOUND");
        }

        period.Status = "HARD_CLOSE";
        period.CloseReason = reason;
        period.ClosedBy = username;
        period.ClosedDate = DateTime.UtcNow;
        period.UpdateUser = username;
        period.UpdateDate = DateTime.UtcNow;

        await _periodRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Fiscal period {PeriodId} ({Name}) hard-closed by {User}", id, period.PeriodNameLocal, username);

        return GlFiscalPeriodMapper.ToDto(period);
    }

    public async Task<GlFiscalPeriodDto> ReopenPeriodAsync(long id, string username, string? reason, CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(id, cancellationToken);
        if (period == null)
        {
            throw new AccountingNotFoundException($"الفترة المالية رقم ({id}) غير موجودة.", "FISCAL_PERIOD_NOT_FOUND");
        }

        if (period.FiscalYear != null && period.FiscalYear.IsClosed)
        {
            throw new AccountingException("لا يمكن إعادة فتح فترة تتبع لسنة مالية مقفلة بالكامل.", "FISCAL_YEAR_CLOSED");
        }

        period.Status = "OPEN";
        period.CloseReason = string.IsNullOrWhiteSpace(reason) ? "تمت إعادة فتح الفترة" : $"إعادة فتح: {reason}";
        period.ClosedBy = null;
        period.ClosedDate = null;
        period.UpdateUser = username;
        period.UpdateDate = DateTime.UtcNow;

        await _periodRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Fiscal period {PeriodId} ({Name}) reopened by {User}. Reason: {Reason}", id, period.PeriodNameLocal, username, reason);

        return GlFiscalPeriodMapper.ToDto(period);
    }

    public async Task ValidatePostingAllowedAsync(long fiscalYearId, DateTime entryDate, CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _fiscalYearRepository.GetByIdAsync(fiscalYearId);
        if (fiscalYear == null)
        {
            throw new AccountingNotFoundException($"السنة المالية رقم ({fiscalYearId}) غير موجودة.", "FISCAL_YEAR_NOT_FOUND");
        }

        if (fiscalYear.IsClosed)
        {
            throw new AccountingException($"لا يمكن الترحيل في سنة مالية مقفلة ({fiscalYear.FiscalYearNameLocal ?? fiscalYear.FiscalYearCode}).", "GL_FISCAL_YEAR_CLOSED");
        }

        var period = await _periodRepository.GetPeriodByDateAsync(fiscalYearId, entryDate, cancellationToken);
        if (period != null)
        {
            if (period.Status == "HARD_CLOSE")
            {
                throw new AccountingException(
                    $"لا يمكن الترحيل بتاريخ ({entryDate:yyyy-MM-dd}) لأن الفترة المالية ({period.PeriodNameLocal}) مقفلة نهائياً (HARD_CLOSE).",
                    "GL_FISCAL_PERIOD_HARD_CLOSED");
            }
            if (period.Status == "SOFT_CLOSE")
            {
                throw new AccountingException(
                    $"الفترة المالية ({period.PeriodNameLocal}) مقفلة جزئياً (SOFT_CLOSE). يتطلب الترحيل فيها صلاحيات مشرف مالي وتبرير محاسبي.",
                    "GL_FISCAL_PERIOD_SOFT_CLOSED");
            }
        }
    }
}
