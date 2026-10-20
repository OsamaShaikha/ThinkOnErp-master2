using System.Globalization;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Inventory;

public sealed class InventoryExcelService(OracleDbContext db) : IInventoryExcelService
{
    private static string Label(InvItemCategory category) => $"{category.CategoryCode} - {category.CategoryNameLocal}";

    public async Task<byte[]> ExportAsync(long branchId, bool items, bool template, CancellationToken ct)
    {
        var categories = await db.InvItemCategories.AsNoTracking().Where(c => c.BranchId == branchId && c.IsActive).OrderBy(c => c.CategoryCode).ToListAsync(ct);
        IEnumerable<object?[]> rows;
        if (items)
        {
            var records = template ? new List<InvItem>() : await db.InvItems.AsNoTracking().Include(i => i.Category).Where(i => i.BranchId == branchId && i.IsActive).OrderBy(i => i.ItemCode).ToListAsync(ct);
            rows = records.Select(i => new object?[] { i.ItemCode, i.Sku, i.ItemNameLocal, i.ItemNameEn, i.Category == null ? "" : Label(i.Category), i.UomBase, i.ItemType, i.CostingMethod, i.StandardCost, i.DefaultSellingPrice, i.ShowInPos });
        }
        else
        {
            var parents = await db.InvItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.CategoryCode, ct);
            rows = template ? Enumerable.Empty<object?[]>() : categories.Select(c => new object?[] { c.CategoryCode, c.CategoryNameLocal, c.CategoryNameEn, c.ParentCategoryId.HasValue ? parents.GetValueOrDefault(c.ParentCategoryId.Value) : null, c.ShowInPos });
        }
        return InventoryWorkbook.Write(items, rows, categories.Select(Label).ToList());
    }

    public async Task<InventoryExcelResult> ImportAsync(Stream workbook, long branchId, bool items, string username, CancellationToken ct)
    {
        var result = new InventoryExcelResult();
        List<(int Number, Dictionary<string, string> Cells)> rows;
        try { rows = InventoryWorkbook.Read(workbook, items); }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or FormatException or ArgumentException or InvalidOperationException or OverflowException)
        {
            result.Errors.Add(new(0, "Workbook", "Invalid XLSX workbook. " + ex.Message));
            return result;
        }
        result.TotalRows = rows.Count;
        if (rows.Count == 0) { result.Errors.Add(new(0, "Workbook", "No data rows found.")); return result; }
        var categories = await db.InvItemCategories.AsNoTracking().ToListAsync(ct);
        var branchCategories = categories.Where(c => c.BranchId == branchId && c.IsActive).ToList();
        var labels = branchCategories.ToDictionary(Label, StringComparer.OrdinalIgnoreCase);
        var codes = new HashSet<string>(items ? await db.InvItems.Select(i => i.ItemCode).ToListAsync(ct) : categories.Select(c => c.CategoryCode.ToString(CultureInfo.InvariantCulture)), StringComparer.OrdinalIgnoreCase);
        var newItems = new List<InvItem>();
        var newCategories = new Dictionary<long, (InvItemCategory Entity, long? Parent, int Row)>();
        foreach (var (number, cells) in rows)
        {
            ct.ThrowIfCancellationRequested();
            string Get(string key) => cells.GetValueOrDefault(key, "");
            void Error(string key, string message) => result.Errors.Add(new(number, key, message));
            string Text(string key, int max, bool required = false)
            {
                var value = Get(key);
                if (required && value.Length == 0) Error(key, "Value is required.");
                if (value.Length > max) Error(key, $"Maximum length is {max}.");
                return value;
            }
            long Integer(string key, bool optional = false)
            {
                if (optional && Get(key) == "") return 0;
                if (!long.TryParse(Get(key), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0) { Error(key, "A positive whole number is required."); return 0; }
                return value;
            }
            bool Boolean(string key)
            {
                if (Get(key) == "") return true;
                if (string.Equals(Get(key), "Yes", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(Get(key), "No", StringComparison.OrdinalIgnoreCase)) return false;
                if (bool.TryParse(Get(key), out var value)) return value;
                if (Get(key) is "1" or "0") return Get(key) == "1";
                Error(key, "Select Yes or No (true/false and 1/0 are also accepted)."); return false;
            }
            decimal Money(string key)
            {
                if (Get(key) == "") return 0;
                if (!decimal.TryParse(Get(key), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 99999999999999.9999m || decimal.Round(value, 4) != value)
                { Error(key, "Use a non-negative decimal with at most 4 decimal places."); return 0; }
                return value;
            }
            T Choice<T>(string key, T fallback) where T : struct, Enum
            {
                if (Get(key) == "") return fallback;
                if (Enum.TryParse<T>(Get(key), true, out var value) && Enum.IsDefined(value)) return value;
                Error(key, "Allowed values: " + string.Join(", ", Enum.GetNames<T>())); return fallback;
            }
            if (items)
            {
                var code = Text("ItemCode", 30, true).ToUpperInvariant();
                if (!codes.Add(code)) Error("ItemCode", "Code already exists in the database or workbook.");
                labels.TryGetValue(Get("Category"), out var category);
                if (category == null) Error("Category", "Select an active category belonging to this branch from the dropdown.");
                var uom = Integer("UomBase");
                if (uom > 999999) Error("UomBase", "Maximum unit code is 999999.");
                newItems.Add(new InvItem { BranchId = branchId, ItemCode = code, Sku = Text("Sku", 100), ItemNameLocal = Text("ItemNameLocal", 200, true), ItemNameEn = Text("ItemNameEn", 200), CategoryId = category?.Id ?? 0, UomBase = uom <= int.MaxValue ? (int)uom : 0, ItemType = Choice("ItemType", ItemType.Stock), CostingMethod = Choice("CostingMethod", CostingMethod.WeightedAverage), StandardCost = Money("StandardCost"), DefaultSellingPrice = Money("DefaultSellingPrice"), ShowInPos = Boolean("ShowInPos"), CreationUser = username });
            }
            else
            {
                var code = Integer("CategoryCode");
                if (code > 9999999999L) Error("CategoryCode", "Maximum category code is 9999999999.");
                if (!codes.Add(code.ToString(CultureInfo.InvariantCulture))) Error("CategoryCode", "Code already exists in the database or workbook.");
                var parent = Integer("ParentCategoryCode", true);
                var category = new InvItemCategory { BranchId = branchId, CategoryCode = code, CategoryNameLocal = Text("CategoryNameLocal", 150, true), CategoryNameEn = Text("CategoryNameEn", 150), ShowInPos = Boolean("ShowInPos"), CreationUser = username };
                newCategories.TryAdd(code, (category, parent == 0 ? null : parent, number));
            }
        }
        // Resolve parent links in any row order; reject cycles before tracking any entities.
        var resolved = new HashSet<long>();
        bool Resolve(long code, HashSet<long> visiting)
        {
            if (resolved.Contains(code)) return true;
            var entry = newCategories[code];
            if (!visiting.Add(code)) { result.Errors.Add(new(entry.Row, "ParentCategoryCode", "Category hierarchy contains a cycle.")); return false; }
            if (entry.Parent is long parentCode)
            {
                var parent = branchCategories.FirstOrDefault(c => c.CategoryCode == parentCode);
                if (parent != null) { entry.Entity.ParentCategoryId = parent.Id; entry.Entity.CategoryLevel = parent.CategoryLevel + 1; }
                else if (newCategories.TryGetValue(parentCode, out var pending))
                {
                    if (visiting.Count > 100) { result.Errors.Add(new(entry.Row, "ParentCategoryCode", "Category hierarchy exceeds 100 levels.")); return false; }
                    if (!Resolve(parentCode, visiting)) return false;
                    entry.Entity.ParentCategory = pending.Entity;
                    entry.Entity.CategoryLevel = pending.Entity.CategoryLevel + 1;
                }
                else { result.Errors.Add(new(entry.Row, "ParentCategoryCode", "Parent must be active and belong to this branch, or appear in this workbook.")); return false; }
            }
            visiting.Remove(code);
            resolved.Add(code);
            return true;
        }
        foreach (var code in newCategories.Keys) Resolve(code, new HashSet<long>());
        if (!result.IsValid) return result;
        if (items) db.InvItems.AddRange(newItems);
        else db.InvItemCategories.AddRange(newCategories.Values.Select(c => c.Entity));
        // EF wraps this single SaveChanges in a transaction, including parent/child inserts.
        try { await db.SaveChangesAsync(ct); result.Imported = rows.Count; }
        catch (DbUpdateException)
        {
            foreach (var entity in newItems.Cast<object>().Concat(newCategories.Values.Select(c => (object)c.Entity))) db.Entry(entity).State = EntityState.Detached;
            result.Errors.Add(new(0, "Workbook", "Database rejected the import. No rows were saved. Check duplicate codes and referenced unit codes."));
        }
        return result;
    }
}
