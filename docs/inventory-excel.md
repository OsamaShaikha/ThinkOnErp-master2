# Inventory Excel import and export

All routes require authentication, the existing tenant context, and access to the requested active branch. Administrators can access branches within the selected company; other users need a `SYS_USER_BRANCH` assignment.

| Method | Route | Result |
| --- | --- | --- |
| GET | `/api/inventory/items/export?branchId=1` | Active branch items in an XLSX workbook |
| GET | `/api/inventory/categories/export?branchId=1` | Active branch categories in an XLSX workbook |
| POST | `/api/inventory/items/import?branchId=1` | Import new items from multipart form field `file` |
| POST | `/api/inventory/categories/import?branchId=1` | Import new categories from multipart form field `file` |

Add `&template=true` to either export route to download an empty template. Use the existing authentication and tenant headers from other inventory requests. Import categories first, then download the item template so its category dropdown includes them.

## Items

The `Items` worksheet contains `ItemCode`, `Sku`, `ItemNameLocal`, `ItemNameEn`, `Category`, `UomBase`, `ItemType`, `CostingMethod`, `StandardCost`, `DefaultSellingPrice`, and `ShowInPos`.

Required values: item code, local name, category, and positive base unit code. Category must match a dropdown entry (`CategoryCode - CategoryNameLocal`). The hidden `CategoryLookup` worksheet includes only active categories with exactly the requested BranchId; shared categories with a null BranchId are excluded. Import revalidates the selection against current database records.

ItemType values: `Stock`, `NonStock`, `Service`, `Kit`, `Assembly` (default `Stock`). CostingMethod values: `WeightedAverage`, `Fifo`, `SpecificId`, `Standard` (default `WeightedAverage`). Prices default to zero and accept a decimal point with up to four decimal places. ShowInPos exports Yes/No with a dropdown in both workbooks. Imports accept Yes/No (case-insensitive), legacy true/false, or 1/0; blank defaults to Yes. Codes and SKU text retain leading zeroes. Other item fields use entity defaults; barcodes, unit conversions, opening balances, images, and tax settings are outside this sheet's scope.

## Categories

The `Categories` worksheet contains `CategoryCode`, `CategoryNameLocal`, `CategoryNameEn`, `ParentCategoryCode`, and `ShowInPos`.

CategoryCode and CategoryNameLocal are required. Leave ParentCategoryCode empty for a root category. A parent may be an existing active category from the same branch or a new category anywhere in the workbook. Cycles and missing parents are rejected. Hierarchy levels are calculated automatically.

## Import behavior

Imports create new records only. Existing codes (including inactive records and codes in other branches) and duplicates within the workbook are rejected. Exported existing rows must be removed or given new codes before import. BranchId comes from the request, not from spreadsheet cells. Exports include the supported columns above, rather than every database field.

Every row is validated before any insert. Successful imports use a single EF SaveChanges transaction. Validation failures return HTTP 400 with `totalRows`, `imported: 0`, `isValid: false`, and `errors` containing the Excel `row`, `column`, and `message`. Successful imports return HTTP 200 with the imported count. Workbook-level errors use row 0. Database conflicts return a workbook-level error and roll back the batch.

Limits: XLSX only, 10 MB uploaded file, 40 MB total uncompressed parts, and 10,000 non-empty data rows per import. Preserve the exported worksheet names and headers. Formula/error cells must be replaced with values before importing. If no branch categories exist, create/import them before filling the item template.

## Verification

Run `dotnet test tests/ThinkOnErp.Inventory.Tests/ThinkOnErp.Inventory.Tests.csproj` for focused workbook, import, hierarchy, and branch access tests. These tests use EF's in-memory provider; Oracle transaction and constraint behavior should also be checked against a staging tenant before production use.

