using FluentValidation;
using MediatR;

namespace ThinkOnErp.Application.Behaviors;

/// <summary>
/// Pipeline behavior that validates MediatR requests using FluentValidation.
/// Executes before the request handler and collects all validation errors.
/// Throws ValidationException if any validation failures occur.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip validation if no validators are registered for this request type
        if (!_validators.Any())
        {
            return await next();
        }

        // Create validation context
        var context = new ValidationContext<TRequest>(request);

        // Execute all validators and collect results
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Collect all validation failures
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        // Throw ValidationException if any failures occurred
        if (failures.Any())
        {
            throw new ValidationException(failures);
        }

        // Proceed to next behavior or handler
        return await next();
    }
}
