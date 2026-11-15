using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Commands.ProcessRawAttendance;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.ProcessRawAttendanceBatch;

public sealed record ProcessRawAttendanceBatchCommand(ProcessRawBatchDto Dto, string CurrentUser)
    : IRequest<ApiResponse<IReadOnlyList<RawProcessingResult>>>;

public sealed class ProcessRawAttendanceBatchCommandValidator : AbstractValidator<ProcessRawAttendanceBatchCommand>
{
    public ProcessRawAttendanceBatchCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Size).InclusiveBetween(1, 100);
    }
}

public sealed class ProcessRawAttendanceBatchCommandHandler
    : IRequestHandler<ProcessRawAttendanceBatchCommand, ApiResponse<IReadOnlyList<RawProcessingResult>>>
{
    private readonly IAttendanceCorrectionRepository _repository;
    private readonly IMediator _mediator;

    public ProcessRawAttendanceBatchCommandHandler(
        IAttendanceCorrectionRepository repository,
        IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task<ApiResponse<IReadOnlyList<RawProcessingResult>>> Handle(
        ProcessRawAttendanceBatchCommand request,
        CancellationToken cancellationToken)
    {
        var size = request.Dto.Size;
        if (size < 1 || size > 100)
            throw new ArgumentException("Batch size must be 1..100.");

        var pending = await _repository.GetUnprocessedRawPunchesAsync(size, cancellationToken);
        var results = new List<RawProcessingResult>();

        foreach (var raw in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var processResponse = await _mediator.Send(
                    new ProcessRawAttendanceCommand(raw.Id, request.CurrentUser), cancellationToken);

                if (processResponse.Data != null)
                {
                    results.Add(processResponse.Data);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
            {
                var current = await _repository.GetRawEventAsync(raw.Id, cancellationToken);
                results.Add(new(
                    raw.Id,
                    false,
                    false,
                    current?.ResolvedWorkDate.HasValue == true ? DateOnly.FromDateTime(current.ResolvedWorkDate.Value) : null,
                    current?.AttendanceDayId,
                    current?.ProcessingOutcome,
                    true,
                    ex.Message));
            }
        }

        return ApiResponse<IReadOnlyList<RawProcessingResult>>.CreateSuccess(results);
    }
}
