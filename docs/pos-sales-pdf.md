# POS sales PDF export

All 16 report types now support the same PDF theme and export screen; see [POS report suite PDF exports](pos-report-suite-pdf.md) for the full catalog and report-specific filters.

`GET /api/pos/reports/sales-summary/pdf` downloads an A4 PDF with the green, cream and gold sales-report theme. The response is `application/pdf` with an attachment filename. The PDF includes company branding, report reference, filters, four summary cards, daily table, totals, signature lines, repeated company header, table headings and numbered page footer. The daily chart and payment-method panel have been removed in both languages; the table follows the summary cards directly.

`GET /api/pos/reports/sales-summary/print-data` returns the same report model wrapped in the usual `ApiResponse`. Use this endpoint to display totals that match the PDF. The existing `/sales-summary` endpoint retains its existing behavior, including its different treatment of open orders and refunds.

## Request

Use the usual bearer token and tenant-selection headers. SuperAdmins must select a tenant using `X-Company-Id` or `X-Company-Code`. The report never accepts a company identity from query input. Regular users can export their primary and assigned branches; tenant administrators and SuperAdmins can export active branches of the selected company.

Example:

```http
GET /api/pos/reports/sales-summary/pdf?BranchId=1&StartDate=2026-10-01&EndDate=2026-10-10
Authorization: Bearer <token>
```

Optional filters: `BranchId`, `StartDate`, `EndDate`, `CashierUsername`, `ShiftId`, `TillId`, `PaymentMethod` (POS payment enum code), `Language` (1 Arabic, 2 English). Omit `BranchId` for all accessible active branches. Dates are inclusive UTC calendar dates; time components are ignored. Defaults are the last ten UTC calendar days. Maximum range: 366 days. All selected branches must share one configured base currency; no exchange-rate conversion is performed. Unknown currency configuration is rejected rather than labeling amounts as JOD automatically.

## Arabic and English

Both the PDF and print-data endpoints use the active session language through `ICurrentTenantContext`: the JWT `lang` claim takes precedence over `defaultLang` / `language`, then `Accept-Language` if no language claim is present. Login `language: 1` produces Arabic, `language: 2` produces English; login `language: 0` uses the user's stored default. A query override `Language=1` or `Language=2` is available if the frontend switches report language without logging in again. Other session languages currently fall back to English; unsupported explicit override values are rejected.

Arabic reverses the entire page layout: logo/company block, title, filters, summary cards, daily table columns, signatures and page footer. All report labels, notes and day names are translated. Arabic selects local company/branch names with English fallback; English does the reverse. User-entered names and usernames are retained. ISO currency codes, Gregorian dates, report references and three-place decimal amounts remain readable with Latin digits in both layouts; dates/IDs and numeric cells use explicit LTR runs. UTC date boundaries and all accounting calculations are identical in both languages. Print-data includes `language` (`ar`/`en`) and `isRightToLeft` for frontend use.

Successful responses include `Content-Language: ar` or `en`; PDF download filenames include the language suffix. For example: `/api/pos/reports/sales-summary/pdf?StartDate=2026-10-01&EndDate=2026-10-26&Language=1`.

Noto Sans and Noto Sans Arabic Regular/Bold are embedded in the assembly and registered once; rendering in either language does not depend on server-installed fonts. The fonts' SIL OFL license is included with the application output. Each language uses the other family as a fallback for mixed-script names.

The payment filter selects complete invoices containing that method. A split cash/card invoice selected by `PaymentMethod=2` contributes its full sales and both tenders. This keeps invoice totals meaningful and avoids allocating discounts/tax across tenders.

## Choose branch and cashier before downloading

Open `/pos-sales-report.html` on the running API host for the bilingual export page. Sign in, choose dates, then select a branch and cashier (both default to All). The language menu changes the form and PDF language. Changing branch resets the cashier selection to All and reloads available cashiers. Download is disabled while options are loading, dates are invalid or export is in progress. Tokens are kept only in page memory, passwords are cleared after login, and all API calls use the same origin.

Existing POS frontends can populate their own controls with:

```http
GET /api/pos/reports/sales-summary/options?StartDate=2026-10-01&EndDate=2026-10-26&Language=1
GET /api/pos/reports/sales-summary/options?BranchId=1&StartDate=2026-10-01&EndDate=2026-10-26&Language=1
GET /api/pos/reports/sales-summary/pdf?BranchId=1&CashierUsername=admin&StartDate=2026-10-01&EndDate=2026-10-26&Language=1
```

The options response includes `branches: [{ id, name }]`, `cashiers: [{ userName, name }]`, localized `allBranchesLabel` and `allCashiersLabel`, and language/direction metadata. The branch list always contains every accessible active branch. Cashiers are scoped to the selected branch, or all accessible branches when omitted: active assigned accounts with no sales are included, along with historical creators of finalized orders within the period, even if deactivated or deleted. Display names use the selected language and fall back to usernames. The export page also shows the username beside the name so accounts with identical display names remain distinguishable. Current `CashierUsername` is ignored when loading choices so the user can still choose a different cashier.

Use branch IDs and cashier usernames as values. Omit `BranchId` for all accessible branches, and omit `CashierUsername` for all cashiers. Do not send the translated All label, `0` or the string `all`. Both choices are independent: one branch/all cashiers, all branches/one cashier, one branch/one cashier or all/all. Options and PDF export share authorization and finalized-order filters; inaccessible/foreign branches return 403. Options remain available when accessible branches have different currencies so the user can select an individual branch; all-branch export still requires one base currency.

## Accounting definitions

- Only active `Completed` and `Refunded` records are included. Original completed sales later marked `Refunded` remain included; separate `IsRefund` records subtract the refund once. Voids and unfinished orders are excluded.
- Date attribution uses order creation date, matching the existing reporting date field. Refunded amounts appear on the refund record's date.
- Sales = non-refund subtotal. Discount = non-refund order discounts. Returns = absolute refund subtotal less its discount, excluding tax.
- Net = Sales - Discount - Returns. Tax = sale tax less absolute refund tax; tax is never hardcoded at 16%.
- Total = recorded sale total less absolute refund total. It includes service charges, delivery fees and tips. A Charges column appears when applicable.
- Invoices counts non-refund finalized sale records, including those without an assigned invoice number. Average invoice = net total including tax / sale invoice count.
- Tender amounts = sale payments less absolute refund payments, grouped by actual tender method. Credit account entries are labeled Credit (AR). These are recorded tender amounts, not a reconstructed allocation of receivable advances. Tender totals can differ from sales because of unpaid balances. Percentages use net tender total; the PDF no longer displays tender bars.
- Amounts display three decimal places. Daily rows include zero-sales dates. Tender data remains in print-data JSON for compatibility, although no payment-method panel appears in the PDF.

## Branding and deployment

The company name, logo and currency come from company configuration; a single branch supplies its tax number and phone. The existing logo storage service loads the configured company logo. Missing logos use a localized neutral company box. The footer uses a typographic ThinkOn wordmark; replace it with the approved logo asset if supplied.

The existing QuestPDF 2024.3.0 dependency is reused. Startup reads `PdfReports:License` (or environment variable `PdfReports__License`); values are `Community`, `Professional`, `Enterprise`, default `Community`. Configure the tier appropriate for the deployment under the terms applicable to that installed version.

No schema migration is required. Deploy/restart the API to expose the endpoints and the built-in export page. The main POS application frontend is outside this checkout; it can use the options endpoint and this download handler for its own Export PDF button:

```js
async function exportSalesPdf(apiBase, token, filters, tenantHeaders = {}) {
  const query = new URLSearchParams(
    Object.entries(filters).filter(([, value]) => value !== undefined && value !== null && value !== '')
  );
  const response = await fetch(`${apiBase}/api/pos/reports/sales-summary/pdf?${query}`, {
    headers: { Authorization: `Bearer ${token}`, ...tenantHeaders }
  });
  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || error.title || 'PDF export failed');
  }
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url;
  link.download = 'sales-report.pdf';
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
```

## Verification

```powershell
$env:POS_PDF_SAMPLE_DIR = Join-Path (Get-Location) 'output/pdf'
dotnet test tests/ThinkOnErp.HR.Api.Tests/ThinkOnErp.HR.Api.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter "FullyQualifiedName~PosSalesPdfTests|FullyQualifiedName~PosReportsTests"
```

The `pos-sales-report-*` samples use synthetic demo data. Tests use EF InMemory for access, filters, language selection, localized names, tender labels and refund reconciliation and the actual QuestPDF renderer for empty, ten-day, ninety-day and full-year reports in both languages. The focused suite (`PosSalesPdfTests` plus existing `PosReportsTests`) passed 33 cases, including branch/cashier options, historical and zero-sales cashiers, cross-tenant denial and independent All/specific branch and cashier combinations.

Live verification on 2026-10-26 used company 1122 and the period 2026-10-01 through 2026-10-26. Its options returned one accessible branch and three cashier accounts. All four All/specific combinations selecting admin correctly retained 161 invoices, net sales 2,683.400 JOD and total 3,125.6925 JOD (displayed 3,125.693), because the existing sales in that period were created by admin. Distinct cashiers and multiple branches are also covered by isolated tests.

The final Arabic and English PDFs were exported after removing the two panels and adjusting table spacing. Each fits this 26-day report, totals and signatures onto one A4 page without reducing the font size. Both were rendered and visually checked; the English PDF text was also checked for absence of chart/payment headings and preservation of totals. Longer reports retain repeated page and table headers. Files prefixed `company-1122-sales-report-ar/en` contain these actual company exports.

The browser flow was verified through login, All/specific branch and cashier selection, changing branch resetting cashier to All, Arabic/English switching, and successful PDF download. Cashier labels include usernames so duplicate display names can be distinguished. The served page was left ready with All branches / All cashiers selected. No database schema migration is needed.
