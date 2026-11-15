# POS report suite: branded PDF exports

All 16 reports use the same green/cream/gold A4 layout as the sales PDF: company logo and details, localized report title, filter band, KPI cards, striped detail tables, signature lines and repeated page footer. Reports have no charts. Arabic mirrors the page and table direction; English uses LTR. Both font families are embedded. See [sales PDF](pos-sales-pdf.md) for common authentication, currency and language behavior.

## Endpoints

Each existing report path below now supports `GET /api/pos/reports/{path}/pdf`. Alternative names resolve to the same report. The original JSON endpoints remain available.

| Canonical path | Report | Alternative path |
| --- | --- | --- |
| `sales-summary` | Daily sales summary | |
| `sales-by-payment-method` | Payment methods | |
| `sales-by-cashier` | Cashier sales, refunds and averages | |
| `sales-by-item` | Item sales and profitability | |
| `sales-by-category` | Category sales and profitability | |
| `voids-and-refunds` | Voided items and refund orders | |
| `discounts` | Discount details, reasons and cashiers | |
| `cash-drawer-variance` | Cash/card reconciliation and audit | `drawer-variance` |
| `cash-movements` | Cash in/out vouchers | `cash-in-out` |
| `hourly-sales` | Hourly sales | |
| `sales-by-floor-and-table` | Floor/table sales and covers | `sales-by-table` |
| `sales-by-order-type` | Order types and averages | |
| `sales-by-customer` | Customer sales, balances and visits | |
| `tax-summary` | Tax summary, rate breakdown and tax bases | |
| `open-tables-live` | Current table status and service | `open-tables` |
| `kitchen-prep-time` | Station performance and slowest items | |

`GET /api/pos/reports/export/catalog?language=1` returns all 16 canonical codes, localized names, aliases, PDF URLs and filter capabilities. `language=2` returns English. Language defaults to the session when omitted.

`GET /api/pos/reports/{path}/print-data` returns the localized presentation model (header, branch sections, metrics, tables and notes). The existing `sales-summary/print-data` keeps its original daily sales model for compatibility.

`GET /api/pos/reports/{path}/options` returns accessible branches and relevant cashiers. The existing summary options endpoint is preserved. Live-table options also return active floors after selecting a branch.

## Filters

Dated reports accept `BranchId`, inclusive UTC `StartDate` / `EndDate`, `CashierUsername`, `ShiftId`, `TillId` and optional `Language=1|2`. Omit branch/cashier for All. The standard maximum period is 366 days. Payment-method filtering is currently supported only by the existing summary PDF; other exports reject that parameter rather than silently ignoring it.

Live tables is a current snapshot, so it rejects date, cashier, shift and till filters. It supports a branch and optional `FloorId`; choose a branch before choosing a floor. Foreign/inactive floors are rejected. The PDF shows the snapshot time instead of a date range, and its download filename contains `snapshot`.

Kitchen preparation adds `DelayedThresholdMinutes`, from 1 to 1440, default 15. This value affects delayed-item counts and is printed on the report. The existing kitchen JSON report now also applies its supplied cashier filter, consistent with the rest of the report suite.

```http
GET /api/pos/reports/sales-by-item/pdf?BranchId=1&StartDate=2026-10-01&EndDate=2026-10-28&Language=1
GET /api/pos/reports/open-tables-live/pdf?BranchId=1&FloorId=1&Language=2
GET /api/pos/reports/kitchen-prep-time/pdf?BranchId=1&DelayedThresholdMinutes=20&Language=1
```

## Calculations and access

The new exports call the existing `IPosReportService` methods with the authorized branch and normalized filters. Values follow each report's current JSON definitions, including its status, refund and date-attribution rules. The existing finalized daily summary remains unchanged. Do not assume that every report has the same gross/net definition as the daily summary.

Selecting All branches produces a named section for each accessible branch, with its own KPI cards and tables. Branch percentages, averages and reconciliation figures are kept separate; they are not added together or relabeled as a weighted global average. Monetary exports require one shared base currency. Kitchen time, which has no monetary figures, can run without a configured currency.

Branch access and company metadata are shared with the existing summary export. No cross-company branches or floors can be selected. Report source failure returns an error; no successful partial PDF is issued. Calls run sequentially on the scoped EF context. Missing data is shown as an explicit empty-table message, and missing nullable counts/amounts remain `-` rather than being invented as zero.

Cashier choices include active assigned accounts and historical operators. Reconciliation adds shift operators; cash-movement reports add voucher creators even if those accounts never created a sale. These choices remain restricted to the accessible/selected branches and period. Stored names, reasons and notes are retained; fixed headings, method names, known statuses, units and empty-state text are localized.

Rate-breakdown tables preserve the tax JSON endpoint's sales-only breakdown; refund tax appears in the KPI summary. Refund-order payment names reflect the first method exposed by that source report. Financial and time values are not recomputed from the formatted PDF strings.

## Export screen

`/pos-sales-report.html` now includes a report-type selector populated by the catalog. Its branch/cashier choices update for the selected report. Live reports hide dates and cashier; floor selection becomes available after selecting a branch. Kitchen preparation displays its delay threshold. Language changes reload localized report names and options while retaining valid choices. Changing report or branch clears dependent selections. Download remains disabled while options load or an export runs.

For frontend integration, use catalog capability flags (`isLive`, `supportsCashier`, `supportsFloor`, `supportsDelayThreshold`) rather than hardcoding assumptions about all reports. Existing POS frontends can use the same endpoints without using this standalone screen.

## Verification

```powershell
$env:POS_PDF_SAMPLE_DIR = Join-Path (Get-Location) 'output/pdf'
dotnet test tests/ThinkOnErp.HR.Api.Tests/ThinkOnErp.HR.Api.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~PosSalesPdfTests|FullyQualifiedName~PosReportsTests|FullyQualifiedName~PosReportSuitePdfTests'
```

The suite covers every report/language pair with populated DTO fixtures, empty and long tables, all aliases, source error propagation, mixed-branch isolation, filter validation, floor scope, catalog/Swagger coverage, and the actual kitchen cashier query. Generated synthetic samples are placed in `output/pdf/pos-suite-samples`; those samples deliberately exercise formatting rather than representing actual company figures.

On 2026-10-28, the focused suite passed 72 tests. All 16 reports were also exported through the running API for company 1122 in both Arabic and English (32 successful PDF responses plus their print-data snapshots), for October 1-28 or the current live snapshot. Actual outputs and verification manifests are under `output/pdf/company-1122-pos-reports`. After the final footer adjustment, PDFs were regenerated from those captured print-data snapshots and rendered with Poppler for visual review. The shared footer reserves a fixed signature area and draws signatures only on the last page, preventing a separate page containing signatures alone. All four PDF alias routes and historical-operator options were checked against the final running API; an Arabic kitchen PDF download also completed through the export screen. The ZIP contains 32 PDFs and a page-count/hash manifest (49 pages total).
