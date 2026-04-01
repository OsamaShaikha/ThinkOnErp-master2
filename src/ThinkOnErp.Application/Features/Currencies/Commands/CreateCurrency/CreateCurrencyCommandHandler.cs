using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Currencies.Commands.CreateCurrency;

public class CreateCurrencyCommandHandler : IRequestHandler<CreateCurrencyCommand, decimal>
{
    private readonly ICurrencyRepository _currencyRepository;

    public CreateCurrencyCommandHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<decimal> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currency = new SysCurrency
        {
            RowDesc = request.RowDesc,
            RowDescE = request.RowDescE,
            ShortDesc = request.ShortDesc,
            ShortDescE = request.ShortDescE,
            SingulerDesc = request.SingulerDesc,
            SingulerDescE = request.SingulerDescE,
            DualDesc = request.DualDesc,
            DualDescE = request.DualDescE,
            SumDesc = request.SumDesc,
            SumDescE = request.SumDescE,
            FracDesc = request.FracDesc,
            FracDescE = request.FracDescE,
            CurrRate = request.CurrRate,
            CurrRateDate = request.CurrRateDate,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _currencyRepository.CreateAsync(currency);
    }
}
