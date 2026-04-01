using MediatR;

namespace ThinkOnErp.Application.Features.Currencies.Commands.CreateCurrency;

/// <summary>
/// Command to create a new currency in the system.
/// Returns the newly created currency's ID.
/// </summary>
public class CreateCurrencyCommand : IRequest<Int64>
{
    public string CurrencyNameAr { get; set; } = string.Empty;
    public string CurrencyNameEn { get; set; } = string.Empty;
    public string ShortDesc { get; set; } = string.Empty;
    public string ShortDescE { get; set; } = string.Empty;
    public string SingulerDesc { get; set; } = string.Empty;
    public string SingulerDescE { get; set; } = string.Empty;
    public string DualDesc { get; set; } = string.Empty;
    public string DualDescE { get; set; } = string.Empty;
    public string SumDesc { get; set; } = string.Empty;
    public string SumDescE { get; set; } = string.Empty;
    public string FracDesc { get; set; } = string.Empty;
    public string FracDescE { get; set; } = string.Empty;
    public decimal? CurrRate { get; set; }
    public DateTime? CurrRateDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
