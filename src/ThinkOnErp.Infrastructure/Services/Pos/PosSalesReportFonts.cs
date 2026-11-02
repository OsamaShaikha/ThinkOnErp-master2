using QuestPDF.Drawing;

namespace ThinkOnErp.Infrastructure.Services.Pos;

internal static class PosSalesReportFonts
{
    private static readonly Lazy<bool> Registered = new(() =>
    {
        var assembly = typeof(PosSalesReportFonts).Assembly;
        foreach (var family in new[] { "NotoSans", "NotoSansArabic" })
        foreach (var weight in new[] { "Regular", "Bold" })
        {
            using var font = assembly.GetManifestResourceStream($"ThinkOnErp.Reports.Fonts.{family}-{weight}.ttf")
                ?? throw new InvalidOperationException("An embedded report font is missing.");
            FontManager.RegisterFontWithCustomName(family == "NotoSans" ? "ThinkOnLatin" : "ThinkOnArabic", font);
        }
        return true;
    });

    public static void EnsureRegistered() => _ = Registered.Value;
}
