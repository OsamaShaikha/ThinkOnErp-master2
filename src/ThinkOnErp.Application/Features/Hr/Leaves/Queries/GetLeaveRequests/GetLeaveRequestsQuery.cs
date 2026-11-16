using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveRequests;

public sealed record GetLeaveRequestsQuery(
    string? EmployeeCode = null,
    string? LeaveTypeCode = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageIndex = 1,
    int PageSize = 20) : IRequest<ApiResponse<PagedResultDto<LeaveRequestDto>>>;

public sealed class GetLeaveRequestsQueryHandler : IRequestHandler<GetLeaveRequestsQuery, ApiResponse<PagedResultDto<LeaveRequestDto>>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetLeaveRequestsQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<PagedResultDto<LeaveRequestDto>>> Handle(GetLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _leaveRepo.GetLeaveRequestsPagedAsync(
            request.EmployeeCode,
            request.LeaveTypeCode,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(r => LeaveCommon.MapLeaveRequest(r)).ToList();
        var paged = new PagedResultDto<LeaveRequestDto>(dtos, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResultDto<LeaveRequestDto>>.CreateSuccess(paged, "Leave requests retrieved successfully.");
    }
}
