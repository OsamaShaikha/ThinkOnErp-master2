using System.Text.Json.Serialization;
using MediatR;

namespace ThinkOnErp.Application.Features.Currencies.Commands.UpdateCurrency;

public class UpdateCurrencyCommand : IRequest<Int64>
{
    [JsonIgnore]
    public Int64 CurrencyId { get; set; }
    public string CurrencyNameLocal { get; set; } = string.Empty;
    public string CurrencyNameEn { get; set; } = string.Empty;
    public string ShortNameLocal { get; set; } = string.Empty;
    public string ShortNameEn { get; set; } = string.Empty;
    public string SingularNameLocal { get; set; } = string.Empty;
    public string SingularNameEn { get; set; } = string.Empty;
    public string DualNameLocal { get; set; } = string.Empty;
    public string DualNameEn { get; set; } = string.Empty;
    public string CollectiveNameLocal { get; set; } = string.Empty;
    public string CollectiveNameEn { get; set; } = string.Empty;
    public string FractionNameLocal { get; set; } = string.Empty;
    public string FractionNameEn { get; set; } = string.Empty;
    public decimal? CurrRate { get; set; }
    public DateTime? CurrRateDate { get; set; }
    [JsonIgnore]
    public string UpdateUser { get; set; } = string.Empty;
}
