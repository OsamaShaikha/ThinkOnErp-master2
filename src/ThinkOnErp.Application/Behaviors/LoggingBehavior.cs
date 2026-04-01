using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ThinkOnErp.Application.Behaviors;

/// <summary>
/// Pipeline behavior that logs all MediatR requests and responses.
/// Logs request type, parameters, response data, and execution time.
/// Executes before ValidationBehavior in the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Handling {RequestName} | Request: {@Request}",
            requestName,
            request);

        try
        {
            var response = await next();

            stopwatch.Stop();

            _logger.LogInformation(
                "Handled {RequestName} | Duration: {ElapsedMilliseconds}ms | Response: {@Response}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                response);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Error handling {RequestName} | Duration: {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
