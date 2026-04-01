using MediatR;
using ThinkOnErp.Application.DTOs.Currency;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Currencies.Queries.GetAllCurrencies;

public class GetAllCurrenciesQueryHandler : IRequestHandler<GetAllCurrenciesQuery, List<CurrencyDto>>
{
    private readonly ICurrencyRepository _currencyRepository;

    public GetAllCurrenciesQueryHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<List<CurrencyDto>> Handle(GetAllCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var currencies = await _currencyRepository.GetAllAsync();

        return currencies.Select(c => new CurrencyDto
        {
            CurrencyId = c.RowId,
            CurrencyNameAr = c.RowDesc,
            CurrencyNameEn = c.RowDescE,
            ShortDesc = c.ShortDesc,
            ShortDescE = c.ShortDescE,
            SingulerDesc = c.SingulerDesc,
            SingulerDescE = c.SingulerDescE,
            DualDesc = c.DualDesc,
            DualDescE = c.DualDescE,
            SumDesc = c.SumDesc,
            SumDescE = c.SumDescE,
            FracDesc = c.FracDesc,
            FracDescE = c.FracDescE,
            CurrRate = c.CurrRate,
            CurrRateDate = c.CurrRateDate,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
    }
}
