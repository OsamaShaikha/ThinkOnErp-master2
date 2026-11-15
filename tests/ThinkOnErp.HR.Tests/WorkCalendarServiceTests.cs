using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class WorkCalendarServiceTests
{
    private readonly Mock<IWorkCalendarRepository> _repoMock = new();
    private readonly WorkCalendarService _service;

    public WorkCalendarServiceTests()
    {
        _service = new WorkCalendarService(_repoMock.Object);
    }

    [Fact]
    public async Task CreateWorkCalendarAsync_WithDays_SavesCustomWorkingDays()
    {
        WorkCalendar? saved = null;
        _repoMock.Setup(r => r.AddCalendarAsync(It.IsAny<WorkCalendar>(), It.IsAny<CancellationToken>()))
            .Callback<WorkCalendar, CancellationToken>((cal, _) => saved = cal)
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dto = new CreateWorkCalendarDto(
            CompanyId: 1,
            Code: "CAL-2026",
            NameAr: "تقويم 2026",
            NameEn: "Calendar 2026",
            EffectiveFrom: new DateTime(2026, 1, 1),
            EffectiveTo: new DateTime(2026, 12, 31),
            IsDefault: true,
            Days: new List<WorkCalendarDayDto>
            {
                new(DayOfWeek: 0, IsWorkingDay: true, StandardWorkingHours: 8m, DefaultShiftCode: "MORNING"),
                new(DayOfWeek: 5, IsWorkingDay: false, StandardWorkingHours: 0m, DefaultShiftCode: null),
                new(DayOfWeek: 6, IsWorkingDay: false, StandardWorkingHours: 0m, DefaultShiftCode: null)
            }
        );

        var result = await _service.CreateWorkCalendarAsync(dto, "ADMIN");

        Assert.NotNull(saved);
        Assert.Equal("CAL-2026", result.Code);
        Assert.True(result.IsActive);
        Assert.Equal(3, result.Days.Count);
        _repoMock.Verify(r => r.AddCalendarAsync(It.IsAny<WorkCalendar>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateWorkCalendarAsync_WithoutDays_GeneratesSundayToThursdayDefaults()
    {
        WorkCalendar? saved = null;
        _repoMock.Setup(r => r.AddCalendarAsync(It.IsAny<WorkCalendar>(), It.IsAny<CancellationToken>()))
            .Callback<WorkCalendar, CancellationToken>((cal, _) => saved = cal)
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dto = new CreateWorkCalendarDto(
            CompanyId: 1,
            Code: "CAL-DEFAULT",
            NameAr: "افتراضي",
            NameEn: "Default",
            EffectiveFrom: new DateTime(2026, 1, 1),
            EffectiveTo: null,
            IsDefault: true,
            Days: null
        );

        var result = await _service.CreateWorkCalendarAsync(dto, "ADMIN");

        Assert.NotNull(saved);
        Assert.Equal(7, result.Days.Count);
        // Days 0-4 are working (Sunday to Thursday)
        for (int i = 0; i <= 4; i++)
        {
            var day = result.Days.Find(d => d.DayOfWeek == i);
            Assert.NotNull(day);
            Assert.True(day.IsWorkingDay);
            Assert.Equal(8.0m, day.StandardWorkingHours);
        }
        // Days 5-6 are off (Friday and Saturday)
        for (int i = 5; i <= 6; i++)
        {
            var day = result.Days.Find(d => d.DayOfWeek == i);
            Assert.NotNull(day);
            Assert.False(day.IsWorkingDay);
            Assert.Equal(0.0m, day.StandardWorkingHours);
        }
    }

    [Fact]
    public async Task CreateCalendarWithDaysAsync_AppliesWorkingDayMaskAndStandardHours()
    {
        WorkCalendar? saved = null;
        _repoMock.Setup(r => r.AddCalendarAsync(It.IsAny<WorkCalendar>(), It.IsAny<CancellationToken>()))
            .Callback<WorkCalendar, CancellationToken>((cal, _) => saved = cal)
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Monday (1) to Friday (5) working (Western calendar)
        var workingDays = new List<int> { 1, 2, 3, 4, 5 };
        var result = await _service.CreateCalendarWithDaysAsync(
            companyId: 2,
            code: "WESTERN",
            nameAr: "غربي",
            nameEn: "Western",
            fromDate: new DateTime(2026, 1, 1),
            workingDaysOfWeek: workingDays,
            standardHours: 7.5m
        );

        Assert.NotNull(saved);
        Assert.Equal(7, result.Days.Count);
        var monday = result.Days.Find(d => d.DayOfWeek == 1);
        Assert.True(monday!.IsWorkingDay);
        Assert.Equal(7.5m, monday.StandardWorkingHours);

        var sunday = result.Days.Find(d => d.DayOfWeek == 0);
        Assert.False(sunday!.IsWorkingDay);
        Assert.Equal(0m, sunday.StandardWorkingHours);
    }

    [Fact]
    public async Task IsWorkingDayAsync_WhenPublicHoliday_ReturnsFalse()
    {
        var testDate = new DateTime(2026, 5, 25); // Independence day
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, testDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicHoliday { Id = 1, NameEn = "Independence Day", HolidayDate = testDate });

        var isWorking = await _service.IsWorkingDayAsync(1, testDate);

        Assert.False(isWorking);
        _repoMock.Verify(r => r.GetActiveCalendarAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsWorkingDayAsync_WhenActiveCalendarExists_RespectsCalendarConfig()
    {
        var testDate = new DateTime(2026, 5, 24); // Sunday (DayOfWeek = 0)
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, testDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);

        var calendar = new WorkCalendar { Id = 10, CompanyId = 1, IsActive = true };
        calendar.Days.Add(new WorkCalendarDay { DayOfWeek = 0, IsWorkingDay = false }); // Sunday marked OFF
        calendar.Days.Add(new WorkCalendarDay { DayOfWeek = 1, IsWorkingDay = true });

        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, testDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        var isWorkingSunday = await _service.IsWorkingDayAsync(1, testDate);
        Assert.False(isWorkingSunday);

        var mondayDate = new DateTime(2026, 5, 25);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, mondayDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        var isWorkingMonday = await _service.IsWorkingDayAsync(1, mondayDate);
        Assert.True(isWorkingMonday);
    }

    [Fact]
    public async Task IsWorkingDayAsync_WhenNoCalendarExists_DefaultsToFridaySaturdayOff()
    {
        _repoMock.Setup(r => r.GetHolidayByDateAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null);

        // 2026-11-06 is Friday (5)
        var friday = new DateTime(2026, 11, 6);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);
        Assert.False(await _service.IsWorkingDayAsync(1, friday));

        // 2026-11-07 is Saturday (6)
        var saturday = new DateTime(2026, 11, 7);
        Assert.Equal(DayOfWeek.Saturday, saturday.DayOfWeek);
        Assert.False(await _service.IsWorkingDayAsync(1, saturday));

        // 2026-11-08 is Sunday (0)
        var sunday = new DateTime(2026, 11, 8);
        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
        Assert.True(await _service.IsWorkingDayAsync(1, sunday));

        // 2026-11-09 is Monday (1)
        var monday = new DateTime(2026, 11, 9);
        Assert.True(await _service.IsWorkingDayAsync(1, monday));
    }

    [Fact]
    public async Task CountWorkingDaysAsync_CalculatesCorrectDaysAcrossDateSpan()
    {
        // Span: 2026-11-01 (Sunday) to 2026-11-07 (Saturday) = 7 days
        // Days: Sun(Work), Mon(Work), Tue(Work), Wed(Holiday), Thu(Work), Fri(Off), Sat(Off)
        // Expected working days: 4 (Sun, Mon, Tue, Thu)
        var holidayDate = new DateTime(2026, 11, 4); // Wednesday
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, DateTime d, CancellationToken _) =>
                d.Date == holidayDate ? new PublicHoliday { Id = 1, HolidayDate = holidayDate } : null);

        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null); // Use default Friday/Saturday off

        var startDate = new DateTime(2026, 11, 1);
        var endDate = new DateTime(2026, 11, 7);

        var count = await _service.CountWorkingDaysAsync(1, startDate, endDate);

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task CreateHolidayAsync_NormalizesDateAndPersists()
    {
        PublicHoliday? saved = null;
        _repoMock.Setup(r => r.AddHolidayAsync(It.IsAny<PublicHoliday>(), It.IsAny<CancellationToken>()))
            .Callback<PublicHoliday, CancellationToken>((h, _) => saved = h)
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dto = new CreatePublicHolidayDto(
            CompanyId: 1,
            BranchId: 7,
            NameAr: "عيد الفطر",
            NameEn: "Eid al-Fitr",
            HolidayDate: new DateTime(2026, 3, 20, 15, 30, 0), // includes time component
            IsPaid: true,
            IsRecurring: false,
            Description: "عطلة رسمية"
        );

        var result = await _service.CreateHolidayAsync(dto, "HR_OFFICER");

        Assert.NotNull(saved);
        Assert.Equal(new DateTime(2026, 3, 20), result.HolidayDate); // Date only normalized
        Assert.Equal("HR_OFFICER", result.CreationUser);
        Assert.Equal("Eid al-Fitr", result.NameEn);
        Assert.True(result.IsPaid);
        _repoMock.Verify(r => r.AddHolidayAsync(It.IsAny<PublicHoliday>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsWorkingDayAsync_WhenPublicHolidayFallsOnWeekend_ReturnsFalse_AndAvoidsCalendarLookup()
    {
        var fridayHoliday = new DateTime(2026, 11, 6); // Friday
        Assert.Equal(DayOfWeek.Friday, fridayHoliday.DayOfWeek);

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, fridayHoliday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicHoliday { Id = 5, HolidayDate = fridayHoliday, NameEn = "National Day" });

        var isWorking = await _service.IsWorkingDayAsync(1, fridayHoliday);

        Assert.False(isWorking);
        _repoMock.Verify(r => r.GetActiveCalendarAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsWorkingDayAsync_WhenCalendarHasDayEntryMarkedNotWorking_ReturnsFalse()
    {
        var wednesday = new DateTime(2026, 11, 4); // DayOfWeek = 3 (Wednesday)
        Assert.Equal(DayOfWeek.Wednesday, wednesday.DayOfWeek);

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, wednesday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);

        var calendar = new WorkCalendar { Id = 15, CompanyId = 1, IsActive = true };
        calendar.Days.Add(new WorkCalendarDay { DayOfWeek = 3, IsWorkingDay = false }); // Wednesday OFF

        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, wednesday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        var isWorking = await _service.IsWorkingDayAsync(1, wednesday);
        Assert.False(isWorking);
    }

    [Fact]
    public async Task IsWorkingDayAsync_WhenCalendarHasNoEntryForDayOfWeek_DefaultsToTrue()
    {
        var tuesday = new DateTime(2026, 11, 3); // DayOfWeek = 2 (Tuesday)
        Assert.Equal(DayOfWeek.Tuesday, tuesday.DayOfWeek);

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, tuesday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);

        var calendar = new WorkCalendar { Id = 16, CompanyId = 1, IsActive = true };
        // Empty days collection: dayConfig is null -> dayConfig?.IsWorkingDay ?? true => true

        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, tuesday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        var isWorking = await _service.IsWorkingDayAsync(1, tuesday);
        Assert.True(isWorking);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_WhenRangeCrossesMonthAndLeapYear_AccuratelyCounts()
    {
        // 2024 is a leap year. Feb 27 to Mar 2 = 5 days:
        // 2024-02-27 (Tue: Work), 2024-02-28 (Wed: Work), 2024-02-29 (Thu: Work),
        // 2024-03-01 (Fri: Off), 2024-03-02 (Sat: Off)
        var fromDate = new DateTime(2024, 2, 27);
        var toDate = new DateTime(2024, 3, 2);

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null); // Default Friday/Saturday off

        var count = await _service.CountWorkingDaysAsync(1, fromDate, toDate);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_WhenStartDateAfterEndDate_ReturnsZero()
    {
        var fromDate = new DateTime(2026, 11, 10);
        var toDate = new DateTime(2026, 11, 5);

        var count = await _service.CountWorkingDaysAsync(1, fromDate, toDate);

        Assert.Equal(0, count);
        _repoMock.Verify(r => r.GetHolidayByDateAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_WithActiveCalendarCustomWeekend_ThursdayFridayOff()
    {
        // Custom Gulf calendar: Thursday (4) and Friday (5) off, Sat(6) through Wed(3) work
        var calendar = new WorkCalendar { Id = 20, CompanyId = 1, IsActive = true };
        for (int i = 0; i < 7; i++)
        {
            bool isOff = (i == 4 || i == 5); // Thu, Fri off
            calendar.Days.Add(new WorkCalendarDay { DayOfWeek = i, IsWorkingDay = !isOff });
        }

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        // Span: 2026-11-01 (Sun) to 2026-11-07 (Sat) = 7 days
        // Days: Sun(Work), Mon(Work), Tue(Work), Wed(Work), Thu(OFF), Fri(OFF), Sat(Work) = 5 working days
        var count = await _service.CountWorkingDaysAsync(1, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7));

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_WithWesternActiveCalendar_SaturdaySundayOff_WithHolidayOnTuesday()
    {
        // Western calendar: Saturday (6) and Sunday (0) off, Mon(1) to Fri(5) work
        var calendar = new WorkCalendar { Id = 21, CompanyId = 1, IsActive = true };
        for (int i = 0; i < 7; i++)
        {
            bool isOff = (i == 0 || i == 6); // Sun, Sat off
            calendar.Days.Add(new WorkCalendarDay { DayOfWeek = i, IsWorkingDay = !isOff });
        }

        var tuesdayHoliday = new DateTime(2026, 11, 3);
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, DateTime d, CancellationToken _) =>
                d.Date == tuesdayHoliday ? new PublicHoliday { Id = 99, HolidayDate = tuesdayHoliday } : null);

        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        // Span: 2026-11-01 (Sun:Off) to 2026-11-07 (Sat:Off) = 7 days
        // Days: Sun(Off), Mon(Work), Tue(Holiday), Wed(Work), Thu(Work), Fri(Work), Sat(Off)
        // Working days: Mon, Wed, Thu, Fri = 4
        var count = await _service.CountWorkingDaysAsync(1, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7));

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_WhenEntireSpanIsHolidays_ReturnsZero()
    {
        var start = new DateTime(2026, 11, 1);
        var end = new DateTime(2026, 11, 3);

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicHoliday { Id = 1, HolidayDate = DateTime.Today });

        var count = await _service.CountWorkingDaysAsync(1, start, end);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetHolidaysAsync_DefaultsToCurrentUtcYear_WhenYearIsNull()
    {
        var expectedHolidays = new List<PublicHoliday>
        {
            new() { Id = 1, NameEn = "New Year", HolidayDate = new DateTime(DateTime.UtcNow.Year, 1, 1) }
        };

        _repoMock.Setup(r => r.GetHolidaysAsync(1, DateTime.UtcNow.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedHolidays);

        var result = await _service.GetHolidaysAsync(1, null);

        Assert.Single(result);
        Assert.Equal("New Year", result[0].NameEn);
        _repoMock.Verify(r => r.GetHolidaysAsync(1, DateTime.UtcNow.Year, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHolidaysAsync_PassesExplicitYear_WhenSpecified()
    {
        var expectedHolidays = new List<PublicHoliday>
        {
            new() { Id = 2, NameEn = "National Day", HolidayDate = new DateTime(2028, 5, 25) }
        };

        _repoMock.Setup(r => r.GetHolidaysAsync(1, 2028, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedHolidays);

        var result = await _service.GetHolidaysAsync(1, 2028);

        Assert.Single(result);
        Assert.Equal("National Day", result[0].NameEn);
        _repoMock.Verify(r => r.GetHolidaysAsync(1, 2028, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCalendarsAsync_And_GetCalendarByIdAsync_DelegateToRepository()
    {
        var calendars = new List<WorkCalendar>
        {
            new() { Id = 10, Code = "CAL-A", CompanyId = 1 },
            new() { Id = 20, Code = "CAL-B", CompanyId = 1 }
        };

        _repoMock.Setup(r => r.GetCalendarsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(calendars);
        _repoMock.Setup(r => r.GetCalendarByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(calendars[0]);

        var list = await _service.GetCalendarsAsync(1);
        Assert.Equal(2, list.Count);

        var single = await _service.GetCalendarByIdAsync(10);
        Assert.NotNull(single);
        Assert.Equal("CAL-A", single!.Code);

        _repoMock.Verify(r => r.GetCalendarsAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.GetCalendarByIdAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateHolidayAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.AddHolidayAsync(It.IsAny<PublicHoliday>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var dto = new CreatePublicHolidayDto(
            CompanyId: 1,
            BranchId: null,
            NameAr: "عطلة",
            NameEn: "Holiday",
            HolidayDate: new DateTime(2026, 5, 1),
            IsPaid: true,
            IsRecurring: false,
            Description: null
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CreateHolidayAsync(dto, "ADMIN", cts.Token));
    }

    [Fact]
    public async Task CreateCalendarWithDaysAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.AddCalendarAsync(It.IsAny<WorkCalendar>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CreateCalendarWithDaysAsync(1, "CAL", "عربي", "English", new DateTime(2026, 1, 1), new List<int> { 0, 1 }, 8m, cts.Token));
    }

    [Fact]
    public async Task CreateWorkCalendarAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.AddCalendarAsync(It.IsAny<WorkCalendar>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var dto = new CreateWorkCalendarDto(
            CompanyId: 1,
            Code: "CAL-01",
            NameAr: "تقويم",
            NameEn: "Calendar",
            EffectiveFrom: new DateTime(2026, 1, 1),
            EffectiveTo: null,
            IsDefault: true,
            Days: null
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CreateWorkCalendarAsync(dto, "ADMIN", cts.Token));
    }

    [Fact]
    public async Task CountWorkingDaysAsync_SingleDay_OnWorkingDay_ReturnsOne()
    {
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null); // Fallback: Friday and Saturday off

        var sunday = new DateTime(2026, 6, 7); // Sunday is working
        var count = await _service.CountWorkingDaysAsync(1, sunday, sunday);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_SingleDay_OnWeekend_ReturnsZero()
    {
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null);

        var friday = new DateTime(2026, 6, 12); // Friday is weekend
        var count = await _service.CountWorkingDaysAsync(1, friday, friday);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_SingleDay_OnHoliday_ReturnsZero()
    {
        var testDate = new DateTime(2026, 6, 8); // Monday (normally working)
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, testDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicHoliday { Id = 10, HolidayDate = testDate, NameEn = "Special Holiday" });

        var count = await _service.CountWorkingDaysAsync(1, testDate, testDate);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CountWorkingDaysAsync(1, new DateTime(2026, 6, 7), new DateTime(2026, 6, 8), cts.Token));
    }

    [Fact]
    public async Task GetHolidaysAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.GetHolidaysAsync(1, 2026, cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetHolidaysAsync(1, 2026, cts.Token));
    }

    [Fact]
    public async Task GetCalendarsAsync_WhenEmpty_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetCalendarsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkCalendar>());

        var result = await _service.GetCalendarsAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCalendarByIdAsync_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetCalendarByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null);

        var result = await _service.GetCalendarByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_WhenFromDateGreaterThanToDate_ReturnsZero()
    {
        var count = await _service.CountWorkingDaysAsync(1, new DateTime(2026, 6, 15), new DateTime(2026, 6, 10));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_LeapYear_February2024_Counts21WorkingDaysWithStandardWeekends()
    {
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null); // Standard Friday (5) and Saturday (6) weekend

        var feb1 = new DateTime(2024, 2, 1);
        var feb29 = new DateTime(2024, 2, 29);

        var count = await _service.CountWorkingDaysAsync(1, feb1, feb29);
        Assert.Equal(21, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_NonLeapYear_February2025_Counts20WorkingDaysWithStandardWeekends()
    {
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null);

        var feb1 = new DateTime(2025, 2, 1);
        var feb28 = new DateTime(2025, 2, 28);

        var count = await _service.CountWorkingDaysAsync(1, feb1, feb28);
        Assert.Equal(20, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_CrossingYearBoundary_ComputesCorrectTotalWorkingDays()
    {
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null);

        // Sun Dec 28, 2025 to Sun Jan 4, 2026:
        // Sun Dec 28, Mon Dec 29, Tue Dec 30, Wed Dec 31, Thu Jan 1 (5 working days)
        // Fri Jan 2, Sat Jan 3 (2 weekend days)
        // Sun Jan 4 (1 working day)
        // Total = 6 working days
        var from = new DateTime(2025, 12, 28);
        var to = new DateTime(2026, 1, 4);

        var count = await _service.CountWorkingDaysAsync(1, from, to);
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_CustomSixDayWorkWeek_CountsSaturdayAsWorkingDay()
    {
        var sixDayCalendar = new WorkCalendar
        {
            Id = 5,
            CompanyId = 1,
            Code = "SIX-DAY",
            Days = new List<WorkCalendarDay>
            {
                new() { DayOfWeek = 0, IsWorkingDay = true },  // Sunday
                new() { DayOfWeek = 1, IsWorkingDay = true },  // Monday
                new() { DayOfWeek = 2, IsWorkingDay = true },  // Tuesday
                new() { DayOfWeek = 3, IsWorkingDay = true },  // Wednesday
                new() { DayOfWeek = 4, IsWorkingDay = true },  // Thursday
                new() { DayOfWeek = 5, IsWorkingDay = false }, // Friday (only weekend)
                new() { DayOfWeek = 6, IsWorkingDay = true },  // Saturday (working day)
            }
        };

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sixDayCalendar);

        var sun = new DateTime(2026, 6, 7);
        var sat = new DateTime(2026, 6, 13);

        var count = await _service.CountWorkingDaysAsync(1, sun, sat);
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_CustomFourDayWorkWeek_TreatsThursdayFridaySaturdayAsOff()
    {
        var fourDayCalendar = new WorkCalendar
        {
            Id = 6,
            CompanyId = 1,
            Code = "FOUR-DAY",
            Days = new List<WorkCalendarDay>
            {
                new() { DayOfWeek = 0, IsWorkingDay = true },  // Sunday
                new() { DayOfWeek = 1, IsWorkingDay = true },  // Monday
                new() { DayOfWeek = 2, IsWorkingDay = true },  // Tuesday
                new() { DayOfWeek = 3, IsWorkingDay = true },  // Wednesday
                new() { DayOfWeek = 4, IsWorkingDay = false }, // Thursday off
                new() { DayOfWeek = 5, IsWorkingDay = false }, // Friday off
                new() { DayOfWeek = 6, IsWorkingDay = false }, // Saturday off
            }
        };

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fourDayCalendar);

        var sun = new DateTime(2026, 6, 7);
        var sat = new DateTime(2026, 6, 13);

        var count = await _service.CountWorkingDaysAsync(1, sun, sat);
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task CountWorkingDaysAsync_OverlappingHolidaysOnWeekend_DoesNotDoubleDeduct()
    {
        var fridayWeekend = new DateTime(2026, 6, 12); // Friday
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, fridayWeekend, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicHoliday { Id = 20, HolidayDate = fridayWeekend, NameEn = "Friday Holiday" });
        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsNotIn(fridayWeekend), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCalendar?)null);

        var sun = new DateTime(2026, 6, 7);
        var sat = new DateTime(2026, 6, 13);

        var count = await _service.CountWorkingDaysAsync(1, sun, sat);
        // Friday is both holiday and weekend, but deducted once: working days = 5 (Sun to Thu)
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task IsWorkingDayAsync_WhenCalendarHasNoConfigForDayOfWeek_DefaultsToTrue()
    {
        var incompleteCalendar = new WorkCalendar
        {
            Id = 7,
            CompanyId = 1,
            Code = "INCOMPLETE",
            Days = new List<WorkCalendarDay>
            {
                new() { DayOfWeek = 0, IsWorkingDay = false } // Only Sunday defined
            }
        };

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicHoliday?)null);
        _repoMock.Setup(r => r.GetActiveCalendarAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteCalendar);

        // Monday (1) has no dayConfig in Days: defaults to true
        var monday = new DateTime(2026, 6, 8);
        var isWorking = await _service.IsWorkingDayAsync(1, monday);
        Assert.True(isWorking);

        // Sunday (0) has dayConfig IsWorkingDay = false: returns false
        var sunday = new DateTime(2026, 6, 7);
        var isSundayWorking = await _service.IsWorkingDayAsync(1, sunday);
        Assert.False(isSundayWorking);
    }

    [Fact]
    public async Task CreateHolidayAsync_SavesWithDateOnlyAndAuditFields()
    {
        PublicHoliday? saved = null;
        _repoMock.Setup(r => r.AddHolidayAsync(It.IsAny<PublicHoliday>(), It.IsAny<CancellationToken>()))
            .Callback<PublicHoliday, CancellationToken>((h, _) => saved = h)
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dto = new CreatePublicHolidayDto(
            CompanyId: 1,
            BranchId: 2,
            NameAr: "يوم الاستقلال",
            NameEn: "Independence Day",
            HolidayDate: new DateTime(2026, 5, 25, 15, 30, 45),
            IsPaid: true,
            IsRecurring: true,
            Description: "National holiday"
        );

        var result = await _service.CreateHolidayAsync(dto, "HR_ADMIN");

        Assert.NotNull(saved);
        Assert.Equal(new DateTime(2026, 5, 25), saved.HolidayDate);
        Assert.Equal(TimeSpan.Zero, saved.HolidayDate.TimeOfDay);
        Assert.Equal("HR_ADMIN", saved.CreationUser);
        Assert.Equal("Independence Day", result.NameEn);
        _repoMock.Verify(r => r.AddHolidayAsync(It.IsAny<PublicHoliday>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsWorkingDayAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.GetHolidayByDateAsync(1, It.IsAny<DateTime>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.IsWorkingDayAsync(1, new DateTime(2026, 6, 8), cts.Token));
    }

    [Fact]
    public async Task GetHolidaysAsync_WhenYearNull_UsesCurrentYear()
    {
        _repoMock.Setup(r => r.GetHolidaysAsync(1, DateTime.UtcNow.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PublicHoliday>());

        var result = await _service.GetHolidaysAsync(1, null);

        Assert.NotNull(result);
        _repoMock.Verify(r => r.GetHolidaysAsync(1, DateTime.UtcNow.Year, It.IsAny<CancellationToken>()), Times.Once);
    }
}
