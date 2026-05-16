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
            Id = request.CurrencyId,
            CurrencyNameAr = request.CurrencyNameAr,
            CurrencyNameEn = request.CurrencyNameEn,
            ShortNameAr = request.ShortNameAr,
            ShortNameEn = request.ShortNameEn,
            SingularNameAr = request.SingularNameAr,
            SingularNameEn = request.SingularNameEn,
            DualNameAr = request.DualNameAr,
            DualNameEn = request.DualNameEn,
            CollectiveNameAr = request.CollectiveNameAr,
            CollectiveNameEn = request.CollectiveNameEn,
            FractionNameAr = request.FractionNameAr,
            FractionNameEn = request.FractionNameEn,
            CurrRate = request.CurrRate,
            CurrRateDate = request.CurrRateDate,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _currencyRepository.UpdateAsync(currency);
    }
}
