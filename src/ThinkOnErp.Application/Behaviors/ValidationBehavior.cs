using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ThinkOnErp.Application.Services.Validation;

namespace ThinkOnErp.Application.Behaviors;

/// <summary>
/// Pipeline behavior that validates MediatR requests using FluentValidation AND dynamic database rules.
/// Executes before the request handler and collects all validation errors.
/// Throws ValidationException if any validation failures occur.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly IDynamicValidationEngine _dynamicEngine;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        IDynamicValidationEngine dynamicEngine)
    {
        _validators = validators;
        _dynamicEngine = dynamicEngine;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        // 1. Run static FluentValidators if any are registered
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            failures.AddRange(validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null));
        }

        // 2. Run Dynamic Database-Driven Validation from THINKON_ERP.SYS_FIELD_VALIDATION_RULE
        var dynamicResult = await _dynamicEngine.ValidateAsync(request, cancellationToken: cancellationToken);
        if (!dynamicResult.IsValid)
        {
            failures.AddRange(dynamicResult.Errors);
        }

        // 3. Throw ValidationException if any failures occurred
        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Proceed to next behavior or handler
        return await next();
    }
}
