using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Pos;

public class MenuEngineeringMatrixDto
{
    public decimal AveragePopularity { get; set; }
    public decimal AverageContributionMargin { get; set; }
    public List<MenuEngineeringItemDto> Stars { get; set; } = new();       // High Profit, High Volume
    public List<MenuEngineeringItemDto> Workhorses { get; set; } = new();  // Low Profit, High Volume
    public List<MenuEngineeringItemDto> Puzzles { get; set; } = new();     // High Profit, Low Volume
    public List<MenuEngineeringItemDto> Dogs { get; set; } = new();        // Low Profit, Low Volume
}

public class MenuEngineeringItemDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal AverageSellingPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitMargin => AverageSellingPrice - UnitCost;
    public decimal TotalProfit => QuantitySold * UnitMargin;
    public string Classification { get; set; } = string.Empty; // Star, Workhorse, Puzzle, Dog
}

public class SalesHeatmapDto
{
    public long BranchId { get; set; }
    public List<HeatmapCellDto> Cells { get; set; } = new();
}

public class HeatmapCellDto
{
    public int DayOfWeek { get; set; } // 0 = Sunday, 1 = Monday, ... 6 = Saturday
    public int HourOfDay { get; set; } // 0..23
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
}
