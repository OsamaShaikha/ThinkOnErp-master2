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
        var currency = await _currencyRepository.GetByIdAsync(request.CurrencyId);

        if (currency == null)
            return null;

        return new CurrencyDto
        {
            CurrencyId = currency.Id,
            CurrencyNameLocal = currency.CurrencyNameLocal,
            CurrencyNameEn = currency.CurrencyNameEn,
            ShortNameLocal = currency.ShortNameLocal,
            ShortNameEn = currency.ShortNameEn,
            SingularNameLocal = currency.SingularNameLocal,
            SingularNameEn = currency.SingularNameEn,
            DualNameLocal = currency.DualNameLocal,
            DualNameEn = currency.DualNameEn,
            CollectiveNameLocal = currency.CollectiveNameLocal,
            CollectiveNameEn = currency.CollectiveNameEn,
            FractionNameLocal = currency.FractionNameLocal,
            FractionNameEn = currency.FractionNameEn,
            CurrRate = currency.CurrRate,
            CurrRateDate = currency.CurrRateDate,
            CreationUser = currency.CreationUser,
            CreationDate = currency.CreationDate,
            UpdateUser = currency.UpdateUser,
            UpdateDate = currency.UpdateDate
        };
    }
}
