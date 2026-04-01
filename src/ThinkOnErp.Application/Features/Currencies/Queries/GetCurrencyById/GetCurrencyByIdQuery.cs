using MediatR;
using ThinkOnErp.Application.DTOs.Currency;

namespace ThinkOnErp.Application.Features.Currencies.Queries.GetCurrencyById;

public class GetCurrencyByIdQuery : IRequest<CurrencyDto?>
{
    public decimal RowId { get; set; }
}
