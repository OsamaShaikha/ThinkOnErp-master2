namespace ThinkOnErp.Application.Services.Hr;

public interface IHrUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}
