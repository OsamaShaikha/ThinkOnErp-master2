using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Currencies.Commands.CreateCurrency;

public class CreateCurrencyCommandHandler : IRequestHandler<CreateCurrencyCommand, Int64>
{
    private readonly ICurrencyRepository _currencyRepository;

    public CreateCurrencyCommandHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<Int64> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currency = new SysCurrency
        {
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
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };

        return await _currencyRepository.CreateAsync(currency);
    }
}
