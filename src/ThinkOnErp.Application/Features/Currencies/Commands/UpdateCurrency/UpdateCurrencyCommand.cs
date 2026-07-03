using System.Text.Json.Serialization;
using MediatR;

namespace ThinkOnErp.Application.Features.Currencies.Commands.UpdateCurrency;

public class UpdateCurrencyCommand : IRequest<Int64>
{
    [JsonIgnore]
    public Int64 CurrencyId { get; set; }
    public string CurrencyNameAr { get; set; } = string.Empty;
    public string CurrencyNameEn { get; set; } = string.Empty;
    public string ShortNameAr { get; set; } = string.Empty;
    public string ShortNameEn { get; set; } = string.Empty;
    public string SingularNameAr { get; set; } = string.Empty;
    public string SingularNameEn { get; set; } = string.Empty;
    public string DualNameAr { get; set; } = string.Empty;
    public string DualNameEn { get; set; } = string.Empty;
    public string CollectiveNameAr { get; set; } = string.Empty;
    public string CollectiveNameEn { get; set; } = string.Empty;
    public string FractionNameAr { get; set; } = string.Empty;
    public string FractionNameEn { get; set; } = string.Empty;
    public decimal? CurrRate { get; set; }
    public DateTime? CurrRateDate { get; set; }
    [JsonIgnore]
    public string UpdateUser { get; set; } = string.Empty;
}
