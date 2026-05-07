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
            CurrencyNameAr = currency.CurrencyNameAr,
            CurrencyNameEn = currency.CurrencyNameEn,
            ShortNameAr = currency.ShortNameAr,
            ShortNameEn = currency.ShortNameEn,
            SingularNameAr = currency.SingularNameAr,
            SingularNameEn = currency.SingularNameEn,
            DualNameAr = currency.DualNameAr,
            DualNameEn = currency.DualNameEn,
            CollectiveNameAr = currency.CollectiveNameAr,
            CollectiveNameEn = currency.CollectiveNameEn,
            FractionNameAr = currency.FractionNameAr,
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
