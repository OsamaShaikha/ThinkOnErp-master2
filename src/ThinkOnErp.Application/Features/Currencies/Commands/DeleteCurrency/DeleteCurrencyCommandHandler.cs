using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency;

public class DeleteCurrencyCommandHandler : IRequestHandler<DeleteCurrencyCommand, int>
{
    private readonly ICurrencyRepository _currencyRepository;

    public DeleteCurrencyCommandHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<int> Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)
    {
        return await _currencyRepository.DeleteAsync(request.RowId);
    }
}
