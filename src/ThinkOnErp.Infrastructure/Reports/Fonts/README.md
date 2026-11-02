# Arabic report fonts

The unmodified Noto Sans and Noto Sans Arabic Regular/Bold fonts are embedded in the Infrastructure assembly and registered once under the `ThinkOnLatin` / `ThinkOnArabic` aliases by `PosSalesReportFonts`. This keeps the PDF independent of fonts installed on the application server. Noto Sans is primary for English and provides Latin glyphs in Arabic reports; Noto Sans Arabic is primary for Arabic and provides Arabic glyphs in English reports.

Sources: [Noto Sans Arabic](https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSansArabic), [Noto Sans](https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSans).

License: SIL Open Font License 1.1, reproduced in `OFL.txt` and included in build/publish output. The TTF files retain their upstream copyright metadata.
