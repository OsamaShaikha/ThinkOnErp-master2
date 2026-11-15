using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.RejectShortLeave;

public sealed record RejectShortLeaveCommand(long RequestId, string Reason, string User) : IRequest<ShortLeaveDto>;

public sealed class RejectShortLeaveCommandValidator : AbstractValidator<RejectShortLeaveCommand>
{
    public RejectShortLeaveCommandValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class RejectShortLeaveCommandHandler : IRequestHandler<RejectShortLeaveCommand, ShortLeaveDto>
{
    private readonly IShortLeaveRepository _repo;

    public RejectShortLeaveCommandHandler(IShortLeaveRepository repo)
    {
        _repo = repo;
    }

    public async Task<ShortLeaveDto> Handle(RejectShortLeaveCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A rejection reason is required.", nameof(request.Reason));

        var entity = await _repo.GetByIdAsync(request.RequestId, ct)
            ?? throw new KeyNotFoundException($"Short leave request {request.RequestId} not found.");

        if (entity.Status != ShortLeaveStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot reject request in status '{entity.Status}'. Only PENDING_APPROVAL requests can be rejected.");

        entity.Status = ShortLeaveStatus.Rejected;
        entity.RejectionReason = request.Reason.Trim();
        entity.RejectedBy = request.User;
        entity.RejectedAtUtc = DateTime.UtcNow;
        entity.UpdateUser = request.User;
        entity.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateRequestAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return ShortLeaveCommon.MapToDto(entity, entity.Employee?.NameLocal);
    }
}
