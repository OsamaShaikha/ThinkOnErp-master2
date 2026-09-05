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
            CurrencyNameLocal = c.CurrencyNameLocal,
            CurrencyNameEn = c.CurrencyNameEn,
            ShortNameLocal = c.ShortNameLocal,
            ShortNameEn = c.ShortNameEn,
            SingularNameLocal = c.SingularNameLocal,
            SingularNameEn = c.SingularNameEn,
            DualNameLocal = c.DualNameLocal,
            DualNameEn = c.DualNameEn,
            CollectiveNameLocal = c.CollectiveNameLocal,
            CollectiveNameEn = c.CollectiveNameEn,
            FractionNameLocal = c.FractionNameLocal,
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
