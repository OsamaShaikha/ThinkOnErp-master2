using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Inventory;

/// <summary>Imports opening stock as a draft using the existing opening-batch workflow.</summary>
public sealed class OpeningBalanceExcelService(OracleDbContext db) : IOpeningBalanceExcelService
{
    private static string ItemLabel(InvItem item) => $"{item.ItemCode} - {item.ItemNameLocal}";
    private static string WarehouseLabel(InvWarehouse warehouse) => $"{warehouse.WarehouseCode} - {warehouse.WarehouseNameLocal}";
    private static string BinLabel(InvBin bin) => $"{bin.Id} - {bin.Zone?.ZoneName}/{bin.BinCode}";
    private const decimal MaxQuantity = 9999999999.9999m;
    private const decimal MaxValue = 99999999999999.9999m;

    public async Task<byte[]> ExportAsync(long branchId, long? batchId, bool template, CancellationToken ct)
    {
        var items = await db.InvItems.AsNoTracking().Where(i => i.BranchId == branchId && i.IsActive).OrderBy(i => i.ItemCode).ToListAsync(ct);
        var warehouses = await db.InvWarehouses.AsNoTracking().Where(w => w.BranchId == branchId && w.IsActive).OrderBy(w => w.WarehouseCode).ToListAsync(ct);
        var bins = await db.InvBins.AsNoTracking().Include(b => b.Zone).Where(b => b.IsActive && b.Zone != null && b.Zone.Warehouse != null && b.Zone.Warehouse.IsActive && b.Zone.Warehouse.BranchId == branchId).OrderBy(b => b.ZoneId).ThenBy(b => b.BinCode).ToListAsync(ct);
        var binChoices = warehouses.ToDictionary(WarehouseLabel, w => (IReadOnlyList<string>)bins.Where(b => b.Zone?.WarehouseId == w.Id).Select(BinLabel).ToList());
        var lines = template ? new List<InvOpeningLine>() : await db.InvOpeningLines.AsNoTracking().Include(l => l.Item).Include(l => l.Warehouse)
            .Where(l => l.BranchId == branchId && l.Batch != null && l.Batch.BranchId == branchId && l.Item != null && l.Item.BranchId == branchId && l.Warehouse != null && l.Warehouse.BranchId == branchId && (!batchId.HasValue || l.BatchId == batchId.Value))
            .OrderBy(l => l.BatchId).ThenBy(l => l.Id).ToListAsync(ct);
        return InventoryWorkbook.Write(InventoryWorkbookKind.OpeningBalances,
            ExportRows(lines, bins.ToDictionary(b => b.Id)),
            items.Select(ItemLabel).ToList(), warehouses.Select(WarehouseLabel).ToList(), binChoices);
    }

    private static IEnumerable<object?[]> ExportRows(List<InvOpeningLine> lines, Dictionary<long, InvBin> bins)
    {
        var groups = lines.GroupBy(l => new { l.BatchId, l.ItemId, l.WarehouseId, l.BinId, l.UomCode, l.UomFactor, l.UnitCost, l.LotNumber, l.ExpiryDate, l.Notes,
            SingleLine = !string.IsNullOrWhiteSpace(l.SerialNumber) && l.Quantity == 1 && l.UomFactor == 1 ? 0 : l.Id });
        foreach (var group in groups)
        {
            // Bound each list to fit an Excel cell even with escaped serial characters.
            foreach (var chunk in group.Chunk(50))
            {
                var line = chunk[0];
                var serialList = chunk.Where(l => !string.IsNullOrWhiteSpace(l.SerialNumber)).Select(l => l.SerialNumber!).ToList();
                yield return new object?[] { line.Item == null ? "" : ItemLabel(line.Item), line.Warehouse == null ? "" : WarehouseLabel(line.Warehouse), chunk.Sum(l => l.Quantity), line.UnitCost, line.Notes,
                    line.BinId.HasValue && bins.TryGetValue(line.BinId.Value, out var bin) ? BinLabel(bin) : line.BinId?.ToString(CultureInfo.InvariantCulture), line.LotNumber,
                    serialList.Count == 0 ? "" : JsonSerializer.Serialize(serialList), line.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), line.UomCode, line.UomFactor };
            }
        }
    }

    private static List<string> ParseSerials(string value)
    {
        if (value.Length > 32767) throw new FormatException("Serial list exceeds the Excel cell limit.");
        if (value == "") return new();
        if (!value.StartsWith('[')) return value.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var serials = JsonSerializer.Deserialize<List<string>>(value) ?? throw new FormatException("Use a JSON array of serial strings.");
        if (serials.Any(string.IsNullOrWhiteSpace)) throw new FormatException("Serial numbers cannot be empty or null.");
        return serials.Select(s => s.Trim()).ToList();
    }

    public async Task<OpeningBalanceExcelResult> ImportAsync(Stream workbook, long branchId, long fiscalYearId, DateTime batchDate, string? description, string username, CancellationToken ct)
    {
        var result = new OpeningBalanceExcelResult();
        var fiscalYear = await db.SysFiscalYears.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fiscalYearId && f.BranchId == branchId && f.IsActive && !f.IsClosed, ct);
        if (fiscalYear == null) result.Errors.Add(new(0, "FiscalYearId", "Select an active, open fiscal year belonging to this branch."));
        else if (batchDate.Date < fiscalYear.StartDate.Date || batchDate.Date > fiscalYear.EndDate.Date)
            result.Errors.Add(new(0, "BatchDate", "Batch date must fall within the selected fiscal year."));
        if (description?.Length > 300) result.Errors.Add(new(0, "Description", "Maximum length is 300."));
        if (!result.IsValid) return result;
        List<(int Number, Dictionary<string, string> Cells)> rows;
        try { rows = InventoryWorkbook.Read(workbook, InventoryWorkbookKind.OpeningBalances); }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or FormatException or ArgumentException or InvalidOperationException or OverflowException)
        {
            result.Errors.Add(new(0, "Workbook", "Invalid XLSX workbook. " + ex.Message));
            return result;
        }
        result.TotalRows = rows.Count;
        if (rows.Count == 0) { result.Errors.Add(new(0, "Workbook", "No data rows found.")); return result; }
        var items = (await db.InvItems.AsNoTracking().Include(i => i.UomConversions).Where(i => i.BranchId == branchId && i.IsActive).ToListAsync(ct)).ToDictionary(ItemLabel, StringComparer.OrdinalIgnoreCase);
        var warehouses = (await db.InvWarehouses.AsNoTracking().Where(w => w.BranchId == branchId && w.IsActive).ToListAsync(ct)).ToDictionary(WarehouseLabel, StringComparer.OrdinalIgnoreCase);
        var bins = await db.InvBins.AsNoTracking().Include(b => b.Zone).Where(b => b.IsActive && b.Zone != null && b.Zone.Warehouse != null && b.Zone.Warehouse.BranchId == branchId).ToDictionaryAsync(b => b.Id, ct);
        var existingSerials = (await db.InvSerialMasters.AsNoTracking().Where(s => s.Item != null && s.Item.BranchId == branchId).Select(s => new { s.ItemId, s.SerialNumber }).ToListAsync(ct))
            .Select(s => (s.ItemId, s.SerialNumber.ToUpperInvariant())).ToHashSet();
        var serials = new HashSet<(long, string)>();
        var keys = new HashSet<(long, long, long?, string, string)>();
        var batch = new InvOpeningBatch { BranchId = branchId, FiscalYearId = fiscalYearId, BatchDate = batchDate.Date, Description = description?.Trim(), CreationUser = username, StatusCode = 1 };
        foreach (var (number, cells) in rows)
        {
            ct.ThrowIfCancellationRequested();
            string Get(string key) => cells.GetValueOrDefault(key, "");
            void Error(string key, string message) => result.Errors.Add(new(number, key, message));
            string Text(string key, int max)
            {
                var value = Get(key);
                if (value.Length > max) Error(key, $"Maximum length is {max}.");
                return value;
            }
            decimal Amount(string key, decimal max)
            {
                if (!decimal.TryParse(Get(key), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) || value < 0 || value > max || decimal.Round(value, 4) != value)
                { Error(key, $"A value between 0 and {max.ToString(CultureInfo.InvariantCulture)} with at most four decimal places is required."); return 0; }
                return value;
            }
            items.TryGetValue(Get("Item"), out var item);
            warehouses.TryGetValue(Get("Warehouse"), out var warehouse);
            if (item == null) Error("Item", "Select an active item belonging to this branch.");
            if (warehouse == null) Error("Warehouse", "Select an active warehouse belonging to this branch.");
            var quantity = Amount("Quantity", MaxQuantity);
            var cost = Amount("UnitCost", MaxValue);
            var total = decimal.Round(quantity * cost, 4);
            if (total > MaxValue) Error("UnitCost", "Line stock value exceeds the database limit.");
            var uomCode = item?.UomBase ?? 0;
            if (Get("UomCode") != "" && (!int.TryParse(Get("UomCode"), NumberStyles.None, CultureInfo.InvariantCulture, out uomCode) || uomCode <= 0 || uomCode > 999999))
                Error("UomCode", "Use a valid unit code assigned to this item, or leave blank for its base unit.");
            var factor = uomCode == item?.UomBase ? 1m : item?.UomConversions.FirstOrDefault(u => u.UomCode == uomCode)?.ConversionFactor ?? 0m;
            if (item != null && (factor <= 0 || factor > MaxValue)) { Error("UomCode", "Unit is not assigned to this item or has an invalid conversion factor."); factor = 0; }
            if (Get("UomFactor") != "" && (!decimal.TryParse(Get("UomFactor"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var suppliedFactor) || suppliedFactor != factor))
                Error("UomFactor", "Factor must match the unit conversion configured for this item; leave blank to use it automatically.");
            var baseQuantity = quantity * factor;
            if (baseQuantity > MaxQuantity || decimal.Round(baseQuantity, 4) != baseQuantity) Error("Quantity", "Base quantity must fit NUMBER(14,4) after unit conversion.");
            long? binId = null;
            if (Get("BinId") != "")
            {
                var selection = Get("BinId");
                var idText = selection.Split(" - ", 2, StringSplitOptions.None)[0];
                if (!long.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !bins.TryGetValue(id, out var bin) || bin.Zone?.WarehouseId != warehouse?.Id ||
                    (selection != idText && !string.Equals(selection, BinLabel(bin), StringComparison.OrdinalIgnoreCase)))
                    Error("BinId", "Select an active bin in the selected warehouse.");
                else binId = id;
            }
            if (warehouse?.EnableBinTracking == true && !binId.HasValue) Error("BinId", "BinId is required for a warehouse with bin tracking enabled.");
            var lot = Text("LotNumber", 50);
            List<string> rowSerials;
            try { rowSerials = ParseSerials(Get("SerialNumbers")); }
            catch (Exception ex) when (ex is JsonException or FormatException)
            { Error("SerialNumbers", "Use a JSON string array or serials separated by commas, semicolons or new lines. " + ex.Message); rowSerials = new(); }
            if (item?.LotTracking == true && lot == "") Error("LotNumber", "Lot number is required for this item.");
            if ((item?.SerialTracking == true || rowSerials.Count > 0) && (rowSerials.Count == 0 || quantity != rowSerials.Count || factor != 1))
                Error("SerialNumbers", "Provide one serial per unit: Quantity must equal the list count and the unit factor must be 1.");
            if (batch.Lines.Count + Math.Max(1, rowSerials.Count) > InventoryWorkbook.MaxRows)
            { Error("SerialNumbers", $"Import is limited to {InventoryWorkbook.MaxRows} opening lines after expanding serial lists."); return result; }
            DateTime? expiry = null;
            if (Get("ExpiryDate") != "")
            {
                if (DateTime.TryParseExact(Get("ExpiryDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) expiry = date.Date;
                else if (double.TryParse(Get("ExpiryDate"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var serialDate) && serialDate >= 1 && serialDate <= 2958465) expiry = DateTime.FromOADate(serialDate).Date;
                else Error("ExpiryDate", "Use yyyy-MM-dd or an Excel date cell.");
            }
            if (item?.ExpiryTracking == true && expiry == null) Error("ExpiryDate", "Expiry date is required for this item.");
            if (item != null && (item.UomBase <= 0 || item.UomBase > 999999)) Error("Item", "Item has an invalid base unit code.");
            var notes = Text("Notes", 500);
            foreach (var serial in rowSerials.Count == 0 ? new List<string> { "" } : rowSerials)
            {
                if (serial.Length > 100) Error("SerialNumbers", "Each serial number has a maximum length of 100.");
                if (item != null && serial != "" && (!serials.Add((item.Id, serial.ToUpperInvariant())) || existingSerials.Contains((item.Id, serial.ToUpperInvariant())))) Error("SerialNumbers", "Serial already exists for this item in the database or workbook.");
                if (item != null && warehouse != null && !keys.Add((item.Id, warehouse.Id, binId, lot.ToUpperInvariant(), serial.ToUpperInvariant()))) Error("Item", "Duplicate item/warehouse/bin/lot/serial combination in workbook.");
                var lineQty = rowSerials.Count > 0 ? 1m : quantity;
                batch.Lines.Add(new InvOpeningLine { BranchId = branchId, ItemId = item?.Id ?? 0, WarehouseId = warehouse?.Id ?? 0, BinId = binId, UomCode = uomCode, UomFactor = factor, Quantity = lineQty, BaseQuantity = lineQty * factor, UnitCost = cost, TotalCost = decimal.Round(lineQty * cost, 4), LotNumber = lot == "" ? null : lot, SerialNumber = serial == "" ? null : serial, ExpiryDate = expiry, Notes = notes });
            }
            batch.TotalQuantity += baseQuantity;
            batch.TotalValuationAmount += total;
        }
        if (batch.TotalQuantity > MaxQuantity) result.Errors.Add(new(0, "Quantity", "Total batch quantity exceeds the database limit."));
        if (batch.TotalValuationAmount > MaxValue) result.Errors.Add(new(0, "UnitCost", "Total batch valuation exceeds the database limit."));
        if (!result.IsValid) return result;
        // A stable batch number prevents uploading the same normalized opening balances twice.
        var canonical = JsonSerializer.Serialize(new { branchId, fiscalYearId, date = batch.BatchDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), lines = batch.Lines.OrderBy(l => l.ItemId).ThenBy(l => l.WarehouseId).ThenBy(l => l.BinId).ThenBy(l => l.LotNumber, StringComparer.Ordinal).ThenBy(l => l.SerialNumber, StringComparer.Ordinal).Select(l => new { l.ItemId, l.WarehouseId, l.BinId, l.UomCode, quantity = l.Quantity.ToString("G29", CultureInfo.InvariantCulture), cost = l.UnitCost.ToString("G29", CultureInfo.InvariantCulture), l.LotNumber, l.SerialNumber, l.ExpiryDate, l.Notes }) });
        batch.BatchNo = "XLS-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24];
        if (await db.InvOpeningBatches.AnyAsync(b => b.BatchNo == batch.BatchNo, ct))
        { result.Errors.Add(new(0, "Workbook", "This opening balance file has already been imported for this branch, fiscal year and date.")); return result; }
        db.InvOpeningBatches.Add(batch);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            foreach (var line in batch.Lines) db.Entry(line).State = EntityState.Detached;
            db.Entry(batch).State = EntityState.Detached;
            result.Errors.Add(new(0, "Workbook", "Database rejected the batch. No opening balances were saved; check duplicate imports and referenced records."));
            return result;
        }
        result.Imported = batch.Lines.Count;
        result.BatchId = batch.Id;
        result.BatchNo = batch.BatchNo;
        result.Status = "Draft";
        return result;
    }
}
