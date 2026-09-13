using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Services.Pos;

public class ParsedScaleBarcode
{
    public bool IsValid { get; set; }
    public ScaleBarcodeType BarcodeType { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public decimal WeightOrQuantity { get; set; }
    public decimal? EmbeddedPrice { get; set; }
    public string RawBarcode { get; set; } = string.Empty;
}

public interface IPosScaleBarcodeParser
{
    ParsedScaleBarcode ParseBarcode(string barcode);
}

public class PosScaleBarcodeParser : IPosScaleBarcodeParser
{
    public ParsedScaleBarcode ParseBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length != 13 || !barcode.All(char.IsDigit))
        {
            return new ParsedScaleBarcode
            {
                IsValid = false,
                BarcodeType = ScaleBarcodeType.Standard,
                RawBarcode = barcode ?? string.Empty
            };
        }

        var prefix = barcode.Substring(0, 2);
        // Typical prefixes for in-store embedded barcodes: 20-29
        if (prefix is "20" or "21" or "22" or "23" or "24" or "25" or "26" or "27" or "28" or "29")
        {
            // Digits 2..6 (5 digits) = Item Code
            var itemCode = barcode.Substring(2, 5);
            // Digits 7..11 (5 digits) = Value
            var valueString = barcode.Substring(7, 5);
            if (decimal.TryParse(valueString, out var rawVal))
            {
                // If prefix is 20-24, treat as Weight in grams (e.g. 01500 = 1.500 kg)
                if (prefix is "20" or "21" or "22")
                {
                    return new ParsedScaleBarcode
                    {
                        IsValid = true,
                        BarcodeType = ScaleBarcodeType.WeightEmbedded,
                        ItemCode = itemCode,
                        WeightOrQuantity = rawVal / 1000m, // Convert grams to kg
                        RawBarcode = barcode
                    };
                }
                else // 23-29: treat as Price embedded in cents/halalas
                {
                    return new ParsedScaleBarcode
                    {
                        IsValid = true,
                        BarcodeType = ScaleBarcodeType.PriceEmbedded,
                        ItemCode = itemCode,
                        WeightOrQuantity = 1m,
                        EmbeddedPrice = rawVal / 100m,
                        RawBarcode = barcode
                    };
                }
            }
        }

        return new ParsedScaleBarcode
        {
            IsValid = false,
            BarcodeType = ScaleBarcodeType.Standard,
            ItemCode = barcode,
            WeightOrQuantity = 1m,
            RawBarcode = barcode
        };
    }
}
