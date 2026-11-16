using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetUnpaidLeaveDays;

public sealed record GetUnpaidLeaveDaysQuery(
    string EmployeeCode,
    DateTime PeriodStart,
    DateTime PeriodEnd) : IRequest<decimal>;

public sealed class GetUnpaidLeaveDaysQueryHandler : IRequestHandler<GetUnpaidLeaveDaysQuery, decimal>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetUnpaidLeaveDaysQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<decimal> Handle(GetUnpaidLeaveDaysQuery request, CancellationToken cancellationToken)
    {
        var unpaidRequests = await _leaveRepo.GetApprovedUnpaidLeavesInPeriodAsync(
            request.EmployeeCode, request.PeriodStart, request.PeriodEnd, cancellationToken);
        decimal totalUnpaidDays = 0m;

        foreach (var req in unpaidRequests)
        {
            var effectiveStart = req.StartDate > request.PeriodStart ? req.StartDate.Date : request.PeriodStart.Date;
            var effectiveEnd = req.EndDate < request.PeriodEnd ? req.EndDate.Date : request.PeriodEnd.Date;

            if (effectiveEnd >= effectiveStart)
            {
                var days = (effectiveEnd - effectiveStart).Days + 1;
                totalUnpaidDays += days;
            }
        }

        return totalUnpaidDays;
    }
}
