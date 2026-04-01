using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Currencies.Commands.UpdateCurrency;

public class UpdateCurrencyCommandHandler : IRequestHandler<UpdateCurrencyCommand, Int64>
{
    private readonly ICurrencyRepository _currencyRepository;

    public UpdateCurrencyCommandHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<Int64> Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currency = new SysCurrency
        {
            RowId = request.CurrencyId,
            RowDesc = request.CurrencyNameAr,
            RowDescE = request.CurrencyNameEn,
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
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _currencyRepository.UpdateAsync(currency);
    }
}
