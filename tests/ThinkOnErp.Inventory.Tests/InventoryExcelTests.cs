using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Inventory;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Controllers.Inventory;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Inventory.Tests;

public sealed class InventoryExcelTests
{
    private static OracleDbContext Context() => new(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static MemoryStream Items(params object?[][] rows) => new(InventoryWorkbook.Write(true, rows, new[] { "10 - Food" }));
    private static object?[] Item(string code = "I001", string category = "10 - Food") => new object?[] { code, "00123", "صنف", "Item", category, 1, "Stock", "WeightedAverage", 2.5m, 5m, true };
    private static InvItemCategory Category(long id, long branch, long code, string name) => new() { Id = id, BranchId = branch, CategoryCode = code, CategoryNameLocal = name };

    [Fact]
    public void WorkbookRoundTripPreservesTextAndDropdown()
    {
        using var stream = Items(Item());
        var row = Assert.Single(InventoryWorkbook.Read(stream, true));
        Assert.Equal("00123", row.Cells["Sku"]);
        Assert.Equal("صنف", row.Cells["ItemNameLocal"]);
        Assert.Equal(2, row.Number);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Contains(XDocument.Load(sheet).Descendants(ns + "formula1"), e => e.Value == "BranchCategories");
    }

    [Fact]
    public async Task ExportLookupContainsOnlyActiveSelectedBranchCategories()
    {
        using var db = Context();
        db.InvItemCategories.AddRange(Category(1, 1, 10, "Food"), Category(2, 2, 20, "Other"), new InvItemCategory { Id = 3, CategoryCode = 30, CategoryNameLocal = "Shared" }, new InvItemCategory { Id = 4, BranchId = 1, CategoryCode = 40, CategoryNameLocal = "Inactive", IsActive = false });
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(await new InventoryExcelService(db).ExportAsync(1, true, true, default));
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var lookup = zip.GetEntry("xl/worksheets/sheet2.xml")!.Open();
        var xml = XDocument.Load(lookup).ToString();
        Assert.Contains("10 - Food", xml);
        Assert.DoesNotContain("Other", xml);
        Assert.DoesNotContain("Shared", xml);
        Assert.DoesNotContain("Inactive", xml);
    }

    [Fact]
    public async Task ImportRejectsOtherBranchCategoryWithoutSavingAnyRows()
    {
        using var db = Context();
        db.InvItemCategories.AddRange(Category(1, 1, 10, "Food"), Category(2, 2, 20, "Other"));
        await db.SaveChangesAsync();
        using var stream = Items(Item(), Item("I002", "20 - Other"));
        var result = await new InventoryExcelService(db).ImportAsync(stream, 1, true, "tester", default);
        Assert.Equal(0, result.Imported);
        Assert.Contains(result.Errors, e => e.Row == 3 && e.Column == "Category");
        Assert.Empty(await db.InvItems.ToListAsync());
    }

    [Fact]
    public async Task ImportRejectsWorkbookDuplicateAndExistingCodeInAnotherBranch()
    {
        using var db = Context();
        db.InvItemCategories.Add(Category(1, 1, 10, "Food"));
        db.InvItems.Add(new InvItem { Id = 1, BranchId = 2, ItemCode = "I001", ItemNameLocal = "Existing" });
        await db.SaveChangesAsync();
        using var stream = Items(Item(), Item("i001"));
        var result = await new InventoryExcelService(db).ImportAsync(stream, 1, true, "tester", default);
        Assert.Equal(2, result.Errors.Count(e => e.Column == "ItemCode"));
        Assert.Equal(1, await db.InvItems.CountAsync());
    }

    [Fact]
    public async Task ValidItemImportUsesBranchAndCategoryId()
    {
        using var db = Context();
        db.InvItemCategories.Add(Category(9, 1, 10, "Food"));
        await db.SaveChangesAsync();
        using var stream = Items(Item());
        var result = await new InventoryExcelService(db).ImportAsync(stream, 1, true, "tester", default);
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Imported);
        var item = await db.InvItems.SingleAsync();
        Assert.Equal(9, item.CategoryId);
        Assert.Equal(1, item.BranchId);
        Assert.Equal("tester", item.CreationUser);
    }

    [Fact]
    public async Task CategoriesResolveParentsAppearingLaterInWorkbook()
    {
        using var db = Context();
        using var stream = new MemoryStream(InventoryWorkbook.Write(false, new[] { new object?[] { 20, "Child", "", 10, true }, new object?[] { 10, "Parent", "", "", true } }, Array.Empty<string>()));
        var result = await new InventoryExcelService(db).ImportAsync(stream, 1, false, "tester", default);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Imported);
        var child = await db.InvItemCategories.SingleAsync(c => c.CategoryCode == 20);
        var parent = await db.InvItemCategories.SingleAsync(c => c.CategoryCode == 10);
        Assert.Equal(parent.Id, child.ParentCategoryId);
        Assert.Equal(2, child.CategoryLevel);
    }

    [Fact]
    public async Task CategoryCyclesSaveNothing()
    {
        using var db = Context();
        using var stream = new MemoryStream(InventoryWorkbook.Write(false, new[] { new object?[] { 20, "Child", "", 10, true }, new object?[] { 10, "Parent", "", 20, true } }, Array.Empty<string>()));
        var result = await new InventoryExcelService(db).ImportAsync(stream, 1, false, "tester", default);
        Assert.False(result.IsValid);
        Assert.Empty(await db.InvItemCategories.ToListAsync());
    }

    [Fact]
    public async Task InvalidWorkbookReturnsError()
    {
        using var db = Context();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await new InventoryExcelService(db).ImportAsync(stream, 1, true, "tester", default);
        Assert.Contains(result.Errors, e => e.Column == "Workbook");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ExportsYesNoValuesAndDropdownForBothSheets(bool items, bool flag)
    {
        var values = items ? Item() : new object?[] { 10, "Food", "", "", flag };
        values[^1] = flag;
        using var stream = new MemoryStream(InventoryWorkbook.Write(items, new[] { values }, Array.Empty<string>()));
        var row = Assert.Single(InventoryWorkbook.Read(stream, items));
        Assert.Equal(flag ? "Yes" : "No", row.Cells["ShowInPos"]);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var part = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var validation = Assert.Single(XDocument.Load(part).Descendants(ns + "dataValidation"));
        Assert.Equal(items ? "K2:K1048576" : "E2:E1048576", (string?)validation.Attribute("sqref"));
        Assert.Equal("\"Yes,No\"", validation.Element(ns + "formula1")!.Value);
    }

    [Theory]
    [InlineData("Yes", true)]
    [InlineData(" no ", false)]
    [InlineData("yEs", true)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("", true)]
    public async Task ImportsYesNoAndLegacyBooleans(string input, bool expected)
    {
        using var db = Context();
        db.InvItemCategories.Add(Category(9, 1, 10, "Food"));
        await db.SaveChangesAsync();
        var values = Item();
        values[^1] = input;
        using var itemStream = Items(values);
        var service = new InventoryExcelService(db);
        Assert.True((await service.ImportAsync(itemStream, 1, true, "tester", default)).IsValid);
        Assert.Equal(expected, (await db.InvItems.SingleAsync()).ShowInPos);
        using var categoryStream = new MemoryStream(InventoryWorkbook.Write(false, new[] { new object?[] { 20, "Other", "", "", input } }, Array.Empty<string>()));
        Assert.True((await service.ImportAsync(categoryStream, 1, false, "tester", default)).IsValid);
        Assert.Equal(expected, (await db.InvItemCategories.SingleAsync(c => c.CategoryCode == 20)).ShowInPos);
    }

    [Fact]
    public async Task InvalidYesNoValueReturnsRowErrorWithoutSaving()
    {
        using var db = Context();
        using var stream = new MemoryStream(InventoryWorkbook.Write(false, new[] { new object?[] { 20, "Other", "", "", "Maybe" } }, Array.Empty<string>()));
        var result = await new InventoryExcelService(db).ImportAsync(stream, 1, false, "tester", default);
        Assert.Contains(result.Errors, e => e.Row == 2 && e.Column == "ShowInPos");
        Assert.Empty(await db.InvItemCategories.ToListAsync());
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task ControllerRequiresAssignedBranch(long requestedBranch, bool allowed)
    {
        using var db = Context();
        db.SysBranches.AddRange(new SysBranch { Id = 1, CompanyId = 1, IsActive = true }, new SysBranch { Id = 2, CompanyId = 1, IsActive = true });
        db.SysUsers.Add(new SysUser { Id = 1, CompanyId = 1, IsActive = true });
        db.SysUserBranches.Add(new SysUserBranch { UserId = 1, BranchId = 1 });
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", "1") }, "test")) };
        http.Items[TenantRequestContext.HttpContextItemKey] = new TenantRequestContext(1, "TEST", "TEST");
        var controller = new InventoryExcelController(new InventoryExcelService(db), db) { ControllerContext = new ControllerContext { HttpContext = http } };
        var result = await controller.ExportItems(requestedBranch, true);
        if (allowed) Assert.IsType<FileContentResult>(result);
        else Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ControllerRejectsBranchFromDifferentCompanyEvenForSuperAdmin()
    {
        using var db = Context();
        db.SysBranches.Add(new SysBranch { Id = 1, CompanyId = 2, IsActive = true });
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("isSuperAdmin", "true") }, "test")) };
        http.Items[TenantRequestContext.HttpContextItemKey] = new TenantRequestContext(1, "TEST", "TEST");
        var controller = new InventoryExcelController(new InventoryExcelService(db), db) { ControllerContext = new ControllerContext { HttpContext = http } };
        Assert.IsType<ForbidResult>(await controller.ExportItems(1, true));
    }
}
