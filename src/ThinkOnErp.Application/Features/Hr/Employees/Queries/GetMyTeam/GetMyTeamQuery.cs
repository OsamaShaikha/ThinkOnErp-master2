using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.GetMyTeam;

/// <summary>Direct reports of the employee linked to <paramref name="UserId"/>.</summary>
public sealed record GetMyTeamQuery(long UserId, bool ActiveOnly, string? Search, int PageIndex, int PageSize)
    : IRequest<ApiResponse<PagedResultDto<EmployeeTeamMemberDto>>>;

public sealed class GetMyTeamQueryHandler : IRequestHandler<GetMyTeamQuery, ApiResponse<PagedResultDto<EmployeeTeamMemberDto>>>
{
    private const int MaxPageSize = 200;
    private const int MaxSearchLength = 200;

    private readonly IEmployeeRepository _repository;

    public GetMyTeamQueryHandler(IEmployeeRepository repository) => _repository = repository;

    public async Task<ApiResponse<PagedResultDto<EmployeeTeamMemberDto>>> Handle(GetMyTeamQuery request, CancellationToken ct)
    {
        if (request.UserId <= 0 || request.PageIndex < 1 || request.PageSize is < 1 or > MaxPageSize ||
            (long)(request.PageIndex - 1) * request.PageSize > int.MaxValue)
            throw new ArgumentException($"Invalid team identity or page; pageSize must be between 1 and {MaxPageSize}.");

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        if (search?.Length > MaxSearchLength)
            throw new ArgumentException($"Team search must not exceed {MaxSearchLength} characters.");

        var (employees, total) = await _repository.GetDirectReportsAsync(request.UserId, request.ActiveOnly, search, request.PageIndex, request.PageSize, ct);

        var items = employees.Select(EmployeeMappings.ToTeamMember).ToList();
        return ApiResponse<PagedResultDto<EmployeeTeamMemberDto>>.CreateSuccess(
            new PagedResultDto<EmployeeTeamMemberDto>(items, total, request.PageIndex, request.PageSize));
    }
}
