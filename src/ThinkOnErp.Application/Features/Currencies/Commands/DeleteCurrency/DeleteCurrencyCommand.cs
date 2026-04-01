using MediatR;

namespace ThinkOnErp.Application.Features.Currencies.Commands.DeleteCurrency;

public class DeleteCurrencyCommand : IRequest<int>
{
    public decimal RowId { get; set; }
}
