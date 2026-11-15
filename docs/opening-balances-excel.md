# Inventory opening balances Excel

This workbook is separate from the item master workbook. It loads opening balances for **existing** items into the existing `INV_OPENING_BATCH` / `INV_OPENING_LINE` draft workflow.

## Routes

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/inventory/opening-balances/export?branchId=1&template=true` | Empty template with branch item and warehouse dropdowns |
| GET | `/api/inventory/opening-balances/export?branchId=1` | Export existing opening batch lines for this branch |
| GET | `/api/inventory/opening-balances/export?branchId=1&batchId=123` | Export one opening batch belonging to this branch |
| POST | `/api/inventory/opening-balances/import?branchId=1&fiscalYearId=1&batchDate=2026-10-25` | Upload multipart field `file` and create one draft batch |

Import also accepts an optional `description` query parameter (maximum 300 characters). Use the same authentication and tenant headers as other inventory requests. The caller must have access to the active branch. The selected fiscal year must be active, open, and belong to that branch; batchDate is required and must fall within it.

## Worksheet

Keep the worksheet name `OpeningBalances` and its headers unchanged:

| Column | Meaning |
| --- | --- |
| Item | Required dropdown: `ItemCode - ItemNameLocal`, active items in the selected branch |
| Warehouse | Required dropdown: `WarehouseCode - WarehouseNameLocal`, active warehouses in the selected branch |
| Quantity | Required, non-negative, up to 4 decimal places |
| UnitCost | Required, non-negative, up to 4 decimal places; explicit zero stays zero |
| Notes | Optional, maximum 500 characters |
| BinId | Dependent dropdown, filtered by the selected Warehouse: `BinId - ZoneName/BinCode`. Optional unless bin tracking is enabled. Plain numeric IDs remain accepted for older files |
| LotNumber | Required for lot-tracked items, maximum 50 characters |
| SerialNumbers | A list, for example `["SN001","SN002"]`, `SN001,SN002`, `SN001;SN002`, or one serial per line in the cell. Each serial has a maximum length of 100; Quantity must equal the list count and UomFactor must be 1 |
| ExpiryDate | Required for expiry-tracked items; use `yyyy-MM-dd` or an Excel date cell |
| UomCode | Optional; defaults to the item's base unit. Other units must be configured on that item |
| UomFactor | Optional; defaults to the configured unit conversion. If filled, it must match that conversion |

Dropdown lookup sheets are hidden. The BinId dropdown follows the Warehouse on the same row. After changing Warehouse, select its BinId again: Excel does not automatically clear an earlier choice, and the backend rejects any bin that belongs to another warehouse, is inactive, or belongs to another branch. Numeric IDs and the exact exported labels are accepted; edited labels are rejected.

Serial-tracked items require a non-empty serial list. Each list expands into individual opening lines with Quantity = 1 and the same item, warehouse, bin, cost, lot, expiry and notes. Export combines compatible serial lines into lists (up to 50 serials per cell). The older `SerialNumber` column header and single-serial cells remain accepted. `TotalRows` counts spreadsheet rows; `Imported` counts saved opening lines after serial expansion. The 10,000-line limit also applies after expansion.

The same item can appear in multiple warehouses, bins, lots or serials. Duplicate item/warehouse/bin/lot/serial combinations within a workbook are rejected, as are duplicate serials for an item within/across lists or already in the serial database. Formula and Excel error cells must be replaced with values.

Quantity, converted base quantity and total batch quantity must fit NUMBER(14,4). Cost, line valuation and total valuation must fit NUMBER(18,4). Every row is validated before one atomic SaveChanges creates the batch and its lines. Limits match the other inventory workbooks: 10 MB file, 40 MB uncompressed parts, 10,000 data rows.

## Review and post

A successful import returns `totalRows`, `imported`, `batchId`, `batchNo`, `status: "Draft"`, `isValid: true`, and an empty errors list. It does **not** change stock balances or post journal entries. Review the batch using the existing opening balance APIs and post with `POST /api/inventory/opening-balances/{batchId}/post`.

Validation failures return HTTP 400 with Excel row/column errors and no saved batch. Row 0 identifies request/workbook errors. Reuploading the same normalized lines for the same branch, fiscal year and batch date is rejected, even if row order or numeric trailing zeroes change. This replay check applies to Excel imports; manually created batches keep the existing workflow.

Exports contain opening batch lines, not current on-hand inventory. They retain original quantity, unit and conversion details and can include multiple batches when batchId is omitted. Inactive referenced items/warehouses remain visible in historical exports but cannot be selected for a new import. For a clean new batch, download the empty template.

## Tests

Run `dotnet test tests/ThinkOnErp.Inventory.Tests/ThinkOnErp.Inventory.Tests.csproj`. Automated tests cover dropdowns, draft persistence, export values, branch isolation, fiscal dates, tracking fields, conversions, duplicate uploads and Inventory Swagger generation. Database tests use EF InMemory; live Oracle and the final Excel UI still require separate verification.
