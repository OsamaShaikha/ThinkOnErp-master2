using MediatR;

namespace ThinkOnErp.Application.Features.Currencies.Commands.UpdateCurrency;

public class UpdateCurrencyCommand : IRequest<Int64>
{
    public Int64 CurrencyId { get; set; }
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
    public string UpdateUser { get; set; } = string.Empty;
}
