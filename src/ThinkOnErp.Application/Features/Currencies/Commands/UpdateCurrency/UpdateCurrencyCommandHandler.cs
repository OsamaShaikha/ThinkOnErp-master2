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
            CurrencyNameLocal = request.CurrencyNameLocal,
            CurrencyNameEn = request.CurrencyNameEn,
            ShortNameLocal = request.ShortNameLocal,
            ShortNameEn = request.ShortNameEn,
            SingularNameLocal = request.SingularNameLocal,
            SingularNameEn = request.SingularNameEn,
            DualNameLocal = request.DualNameLocal,
            DualNameEn = request.DualNameEn,
            CollectiveNameLocal = request.CollectiveNameLocal,
            CollectiveNameEn = request.CollectiveNameEn,
            FractionNameLocal = request.FractionNameLocal,
            FractionNameEn = request.FractionNameEn,
            CurrRate = request.CurrRate,
            CurrRateDate = request.CurrRateDate,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        return await _currencyRepository.UpdateAsync(currency);
    }
}
