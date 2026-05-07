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
            CurrencyId = c.Id,
            CurrencyNameAr = c.CurrencyNameAr,
            CurrencyNameEn = c.CurrencyNameEn,
            ShortNameAr = c.ShortNameAr,
            ShortNameEn = c.ShortNameEn,
            SingularNameAr = c.SingularNameAr,
            SingularNameEn = c.SingularNameEn,
            DualNameAr = c.DualNameAr,
            DualNameEn = c.DualNameEn,
            CollectiveNameAr = c.CollectiveNameAr,
            CollectiveNameEn = c.CollectiveNameEn,
            FractionNameAr = c.FractionNameAr,
            FractionNameEn = c.FractionNameEn,
            CurrRate = c.CurrRate,
            CurrRateDate = c.CurrRateDate,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
    }
}
