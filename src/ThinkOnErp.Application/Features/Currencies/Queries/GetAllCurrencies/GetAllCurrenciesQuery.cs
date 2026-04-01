using MediatR;
using ThinkOnErp.Application.DTOs.Currency;

namespace ThinkOnErp.Application.Features.Currencies.Queries.GetAllCurrencies;

public class GetAllCurrenciesQuery : IRequest<List<CurrencyDto>>
{
}
