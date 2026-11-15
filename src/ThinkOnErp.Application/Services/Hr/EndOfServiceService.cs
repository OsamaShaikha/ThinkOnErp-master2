using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Employees.Common;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.ApproveSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CalculateSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CancelSettlement;
using ThinkOnErp.Application.Features.Hr.EndOfService.Common;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetEndOfServiceHandoffStatus;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementByEmployeeCode;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlementById;
using ThinkOnErp.Application.Features.Hr.EndOfService.Queries.GetSettlements;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.EndOfService.")]
public interface IEndOfServiceService
{
    Task<EndOfServiceSettlementDto> CalculateSettlementAsync(CreateEndOfServiceRequest request, string user, long companyId, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto> ApproveSettlementAsync(long settlementId, string user, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto> CancelSettlementAsync(long settlementId, string reason, string user, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto?> GetByIdAsync(long settlementId, CancellationToken ct = default);
    Task<EndOfServiceSettlementDto?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken ct = default);
    Task<IReadOnlyList<EndOfServiceSettlementDto>> GetListAsync(long companyId, long? branchId = null, string? status = null, CancellationToken ct = default);
    Task<EndOfServiceHandoffStatusDto?> GetHandoffStatusAsync(long settlementId, CancellationToken ct = default);
}

/// <summary>
/// Transitional facade. All logic lives in the Features/Hr/EndOfService handlers; this class only
/// constructs them, so existing unit tests keep exercising the real rules. It is no longer registered in DI.
/// Delete it once those callers use the handlers.
/// </summary>
[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.EndOfService. Kept only for existing tests.")]
public sealed class EndOfServiceService : IEndOfServiceService
{
    private readonly IEndOfServiceRepository _eosRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IEndOfServiceCalculationEngine _engine;
    private readonly IPolicyRepository _policyRepository;
    private readonly IEndOfServiceTransactionRunner _transactionRunner;
    private readonly IHrUnitOfWork _unitOfWork;

    public EndOfServiceService(
        IEndOfServiceRepository eosRepo,
        IEmployeeRepository employeeRepo,
        IEndOfServiceCalculationEngine engine,
        IPolicyRepository? policyRepository = null,
        IEndOfServiceTransactionRunner? transactionRunner = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _eosRepo = eosRepo;
        _employeeRepo = employeeRepo;
        _engine = engine;
        _policyRepository = policyRepository ?? new NullPolicyRepository();
        _transactionRunner = transactionRunner ?? new PassThroughEndOfServiceTransactionRunner();
        _unitOfWork = unitOfWork ?? new PassThroughHrUnitOfWork();
    }

    public async Task<EndOfServiceSettlementDto> CalculateSettlementAsync(CreateEndOfServiceRequest request, string user, long companyId, CancellationToken ct = default)
        => Data(await new CalculateSettlementCommandHandler(_eosRepo, _employeeRepo, _engine, _policyRepository)
            .Handle(new CalculateSettlementCommand(request, companyId, user), ct));

    public async Task<EndOfServiceSettlementDto> ApproveSettlementAsync(long settlementId, string user, CancellationToken ct = default)
        => Data(await new ApproveSettlementCommandHandler(_eosRepo, _employeeRepo, _transactionRunner, _unitOfWork)
            .Handle(new ApproveSettlementCommand(settlementId, user), ct));

    public async Task<EndOfServiceSettlementDto> CancelSettlementAsync(long settlementId, string reason, string user, CancellationToken ct = default)
        => Data(await new CancelSettlementCommandHandler(_eosRepo, _employeeRepo, _transactionRunner, _unitOfWork)
            .Handle(new CancelSettlementCommand(settlementId, reason, user), ct));

    public async Task<EndOfServiceSettlementDto?> GetByIdAsync(long settlementId, CancellationToken ct = default)
        => DataOrNull(await new GetSettlementByIdQueryHandler(_eosRepo)
            .Handle(new GetSettlementByIdQuery(settlementId), ct));

    public async Task<EndOfServiceSettlementDto?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken ct = default)
        => DataOrNull(await new GetSettlementByEmployeeCodeQueryHandler(_eosRepo)
            .Handle(new GetSettlementByEmployeeCodeQuery(employeeCode), ct));

    public async Task<IReadOnlyList<EndOfServiceSettlementDto>> GetListAsync(long companyId, long? branchId = null, string? status = null, CancellationToken ct = default)
        => Data(await new GetSettlementsQueryHandler(_eosRepo)
            .Handle(new GetSettlementsQuery(companyId, branchId, status), ct));

    public async Task<EndOfServiceHandoffStatusDto?> GetHandoffStatusAsync(long settlementId, CancellationToken ct = default)
        => DataOrNull(await new GetEndOfServiceHandoffStatusQueryHandler(_eosRepo)
            .Handle(new GetEndOfServiceHandoffStatusQuery(settlementId), ct));

    private static T Data<T>(ApiResponse<T> response) => response.Data!;

    private static T? DataOrNull<T>(ApiResponse<T> response) where T : class => response.Success ? response.Data : null;
}
