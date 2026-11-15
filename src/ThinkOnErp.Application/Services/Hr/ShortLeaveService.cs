using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ApproveShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CancelShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RejectShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.SaveShortLeavePolicy;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.SubmitShortLeave;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveByEmployee;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveById;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeavePolicy;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetShortLeaveRequests;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Leaves.")]
public interface IShortLeaveService
{
    Task<ShortLeaveDto> SubmitRequestAsync(CreateShortLeaveDto dto, string user, CancellationToken ct = default);
    Task<ShortLeaveDto> ApproveRequestAsync(long requestId, string user, CancellationToken ct = default);
    Task<ShortLeaveDto> RejectRequestAsync(long requestId, string reason, string user, CancellationToken ct = default);
    Task<ShortLeaveDto> CancelRequestAsync(long requestId, string reason, string user, CancellationToken ct = default);
    Task<ShortLeaveDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ShortLeaveDto>> GetByEmployeeAsync(string employeeCode, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);
    Task<IReadOnlyList<ShortLeaveDto>> GetRequestsAsync(long companyId, long? branchId = null, string? status = null, DateTime? date = null, CancellationToken ct = default);
    Task<ShortLeavePolicyDto> GetPolicyAsync(long companyId, CancellationToken ct = default);
    Task<ShortLeavePolicyDto> SavePolicyAsync(ShortLeavePolicyDto dto, string user, CancellationToken ct = default);
    (int ExcusedLateMinutes, int ExcusedEarlyMinutes, int ExcusedMiddayMinutes) CalculateExcusedMinutes(IEnumerable<ShortLeaveRequest> approvedRequests);
}

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Leaves. Kept for backwards compatibility.")]
public sealed class ShortLeaveService : IShortLeaveService
{
    private readonly IShortLeaveRepository _repo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IPolicyRepository? _policyRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public ShortLeaveService(
        IShortLeaveRepository repo,
        IEmployeeRepository employeeRepo,
        IPolicyRepository? policyRepository = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _repo = repo;
        _employeeRepo = employeeRepo;
        _policyRepository = policyRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<ShortLeaveDto> SubmitRequestAsync(CreateShortLeaveDto dto, string user, CancellationToken ct = default)
        => new SubmitShortLeaveCommandHandler(_repo, _employeeRepo)
            .Handle(new SubmitShortLeaveCommand(dto, user), ct);

    public Task<ShortLeaveDto> ApproveRequestAsync(long requestId, string user, CancellationToken ct = default)
        => new ApproveShortLeaveCommandHandler(_repo, _employeeRepo, _policyRepository, _unitOfWork)
            .Handle(new ApproveShortLeaveCommand(requestId, user), ct);

    public Task<ShortLeaveDto> RejectRequestAsync(long requestId, string reason, string user, CancellationToken ct = default)
        => new RejectShortLeaveCommandHandler(_repo)
            .Handle(new RejectShortLeaveCommand(requestId, reason, user), ct);

    public Task<ShortLeaveDto> CancelRequestAsync(long requestId, string reason, string user, CancellationToken ct = default)
        => new CancelShortLeaveCommandHandler(_repo)
            .Handle(new CancelShortLeaveCommand(requestId, reason, user), ct);

    public Task<ShortLeaveDto?> GetByIdAsync(long id, CancellationToken ct = default)
        => new GetShortLeaveByIdQueryHandler(_repo)
            .Handle(new GetShortLeaveByIdQuery(id), ct);

    public Task<IReadOnlyList<ShortLeaveDto>> GetByEmployeeAsync(string employeeCode, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
        => new GetShortLeaveByEmployeeQueryHandler(_repo)
            .Handle(new GetShortLeaveByEmployeeQuery(employeeCode, fromDate, toDate), ct);

    public Task<IReadOnlyList<ShortLeaveDto>> GetRequestsAsync(long companyId, long? branchId = null, string? status = null, DateTime? date = null, CancellationToken ct = default)
        => new GetShortLeaveRequestsQueryHandler(_repo)
            .Handle(new GetShortLeaveRequestsQuery(companyId, branchId, status, date), ct);

    public Task<ShortLeavePolicyDto> GetPolicyAsync(long companyId, CancellationToken ct = default)
        => new GetShortLeavePolicyQueryHandler(_repo)
            .Handle(new GetShortLeavePolicyQuery(companyId), ct);

    public Task<ShortLeavePolicyDto> SavePolicyAsync(ShortLeavePolicyDto dto, string user, CancellationToken ct = default)
        => new SaveShortLeavePolicyCommandHandler(_repo)
            .Handle(new SaveShortLeavePolicyCommand(dto, user), ct);

    public (int ExcusedLateMinutes, int ExcusedEarlyMinutes, int ExcusedMiddayMinutes) CalculateExcusedMinutes(IEnumerable<ShortLeaveRequest> approvedRequests)
        => ShortLeaveCommon.CalculateExcusedMinutes(approvedRequests);
}
