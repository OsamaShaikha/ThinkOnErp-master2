using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThinkOnErp.Application.Services.Pos;

public class EInvoiceQrData
{
    public string SellerName { get; set; } = string.Empty;
    public string VatRegistrationNumber { get; set; } = string.Empty;
    public DateTime InvoiceTimestamp { get; set; }
    public decimal InvoiceTotalWithVat { get; set; }
    public decimal VatTotal { get; set; }
    public string? InvoiceHash { get; set; }
}

public interface IPosEInvoicingService
{
    string GenerateTlvQrCodeBase64(EInvoiceQrData data);
    string ComputeInvoiceHash(string invoiceXmlOrData);
}

public class PosEInvoicingService : IPosEInvoicingService
{
    public string GenerateTlvQrCodeBase64(EInvoiceQrData data)
    {
        using var ms = new MemoryStream();

        // Tag 1: Seller Name
        WriteTlvTag(ms, 1, data.SellerName);

        // Tag 2: VAT Registration Number
        WriteTlvTag(ms, 2, data.VatRegistrationNumber);

        // Tag 3: Invoice Timestamp (ISO 8601: yyyy-MM-ddTHH:mm:ssZ)
        WriteTlvTag(ms, 3, data.InvoiceTimestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));

        // Tag 4: Invoice Total (with VAT)
        WriteTlvTag(ms, 4, data.InvoiceTotalWithVat.ToString("F2"));

        // Tag 5: VAT Total
        WriteTlvTag(ms, 5, data.VatTotal.ToString("F2"));

        // Tag 6: Invoice Hash (if provided)
        if (!string.IsNullOrWhiteSpace(data.InvoiceHash))
        {
            WriteTlvTag(ms, 6, data.InvoiceHash);
        }

        var tlvBytes = ms.ToArray();
        return Convert.ToBase64String(tlvBytes);
    }

    public string ComputeInvoiceHash(string invoiceXmlOrData)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(invoiceXmlOrData);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static void WriteTlvTag(MemoryStream ms, byte tag, string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        ms.WriteByte(tag);
        ms.WriteByte((byte)valueBytes.Length);
        ms.Write(valueBytes, 0, valueBytes.Length);
    }
}
