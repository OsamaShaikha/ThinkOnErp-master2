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

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.CancelShortLeave;

public sealed record CancelShortLeaveCommand(long RequestId, string Reason, string User) : IRequest<ShortLeaveDto>;

public sealed class CancelShortLeaveCommandValidator : AbstractValidator<CancelShortLeaveCommand>
{
    public CancelShortLeaveCommandValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class CancelShortLeaveCommandHandler : IRequestHandler<CancelShortLeaveCommand, ShortLeaveDto>
{
    private readonly IShortLeaveRepository _repo;

    public CancelShortLeaveCommandHandler(IShortLeaveRepository repo)
    {
        _repo = repo;
    }

    public async Task<ShortLeaveDto> Handle(CancelShortLeaveCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A cancellation reason is required.", nameof(request.Reason));

        var entity = await _repo.GetByIdAsync(request.RequestId, ct)
            ?? throw new KeyNotFoundException($"Short leave request {request.RequestId} not found.");

        if (entity.Status == ShortLeaveStatus.Cancelled)
            throw new InvalidOperationException("Short leave request is already cancelled.");

        if (entity.Status == ShortLeaveStatus.Rejected)
            throw new InvalidOperationException("Cannot cancel an already rejected short leave request.");

        entity.Status = ShortLeaveStatus.Cancelled;
        entity.CancellationReason = request.Reason.Trim();
        entity.CancelledBy = request.User;
        entity.CancelledAtUtc = DateTime.UtcNow;
        entity.UpdateUser = request.User;
        entity.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateRequestAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return ShortLeaveCommon.MapToDto(entity, entity.Employee?.NameLocal);
    }
}
