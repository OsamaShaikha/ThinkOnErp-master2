using MediatR;
using ThinkOnErp.Application.DTOs.Currency;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Currencies.Queries.GetCurrencyById;

public class GetCurrencyByIdQueryHandler : IRequestHandler<GetCurrencyByIdQuery, CurrencyDto?>
{
    private readonly ICurrencyRepository _currencyRepository;

    public GetCurrencyByIdQueryHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<CurrencyDto?> Handle(GetCurrencyByIdQuery request, CancellationToken cancellationToken)
    {
        var currency = await _currencyRepository.GetByIdAsync(request.RowId);

        if (currency == null)
            return null;

        return new CurrencyDto
        {
            RowId = currency.RowId,
            RowDesc = currency.RowDesc,
            RowDescE = currency.RowDescE,
            ShortDesc = currency.ShortDesc,
            ShortDescE = currency.ShortDescE,
            SingulerDesc = currency.SingulerDesc,
            SingulerDescE = currency.SingulerDescE,
            DualDesc = currency.DualDesc,
            DualDescE = currency.DualDescE,
            SumDesc = currency.SumDesc,
            SumDescE = currency.SumDescE,
            FracDesc = currency.FracDesc,
            FracDescE = currency.FracDescE,
            CurrRate = currency.CurrRate,
            CurrRateDate = currency.CurrRateDate,
            CreationUser = currency.CreationUser,
            CreationDate = currency.CreationDate,
            UpdateUser = currency.UpdateUser,
            UpdateDate = currency.UpdateDate
        };
    }
}
