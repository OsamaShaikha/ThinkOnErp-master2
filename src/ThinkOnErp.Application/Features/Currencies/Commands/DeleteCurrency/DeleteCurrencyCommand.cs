using MediatR;

namespace ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency;

public class DeleteCurrencyCommand : IRequest<Int64>
{
    public Int64 CurrencyId { get; set; }
}
