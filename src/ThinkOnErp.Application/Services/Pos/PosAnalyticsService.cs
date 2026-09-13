using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosAnalyticsService
{
    Task<ApiResponse<MenuEngineeringMatrixDto>> GetMenuEngineeringMatrixAsync(long branchId, DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<ApiResponse<SalesHeatmapDto>> GetSalesHeatmapAsync(long branchId, DateTime startDate, DateTime endDate, CancellationToken ct = default);
}

public class PosAnalyticsService : IPosAnalyticsService
{
    private readonly IPosOrderRepository _orderRepository;

    public PosAnalyticsService(IPosOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<ApiResponse<MenuEngineeringMatrixDto>> GetMenuEngineeringMatrixAsync(long branchId, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        var orders = await _orderRepository.GetActiveOrdersAsync(branchId, null, ct);
        var completedOrders = orders
            .Where(o => !o.IsRefund && o.CreationDate >= startDate && o.CreationDate <= endDate)
            .ToList();

        var itemSales = new Dictionary<long, (string Code, string Name, decimal Qty, decimal Revenue, decimal Cost)>();

        foreach (var order in completedOrders)
        {
            foreach (var line in order.Lines.Where(l => !l.IsVoided))
            {
                if (!itemSales.TryGetValue(line.ItemId, out var val))
                {
                    itemSales[line.ItemId] = (line.ItemCode, line.ItemName, line.Quantity, line.LineTotal, line.CostPrice * line.Quantity);
                }
                else
                {
                    itemSales[line.ItemId] = (
                        val.Code,
                        val.Name,
                        val.Qty + line.Quantity,
                        val.Revenue + line.LineTotal,
                        val.Cost + (line.CostPrice * line.Quantity)
                    );
                }
            }
        }

        if (!itemSales.Any())
            return ApiResponse<MenuEngineeringMatrixDto>.CreateSuccess(new MenuEngineeringMatrixDto(), "No sales data found for the period");

        var itemsList = itemSales.Select(kv => new MenuEngineeringItemDto
        {
            ItemId = kv.Key,
            ItemCode = kv.Value.Code,
            ItemName = kv.Value.Name,
            QuantitySold = kv.Value.Qty,
            AverageSellingPrice = kv.Value.Qty > 0 ? kv.Value.Revenue / kv.Value.Qty : 0m,
            UnitCost = kv.Value.Qty > 0 ? kv.Value.Cost / kv.Value.Qty : 0m
        }).ToList();

        var avgPopularity = itemsList.Average(i => i.QuantitySold);
        var avgMargin = itemsList.Average(i => i.UnitMargin);

        var matrix = new MenuEngineeringMatrixDto
        {
            AveragePopularity = avgPopularity,
            AverageContributionMargin = avgMargin
        };

        foreach (var item in itemsList)
        {
            bool isHighMargin = item.UnitMargin >= avgMargin;
            bool isHighPopularity = item.QuantitySold >= avgPopularity;

            if (isHighMargin && isHighPopularity)
            {
                item.Classification = "Star";
                matrix.Stars.Add(item);
            }
            else if (!isHighMargin && isHighPopularity)
            {
                item.Classification = "Workhorse";
                matrix.Workhorses.Add(item);
            }
            else if (isHighMargin && !isHighPopularity)
            {
                item.Classification = "Puzzle";
                matrix.Puzzles.Add(item);
            }
            else
            {
                item.Classification = "Dog";
                matrix.Dogs.Add(item);
            }
        }

        return ApiResponse<MenuEngineeringMatrixDto>.CreateSuccess(matrix);
    }

    public async Task<ApiResponse<SalesHeatmapDto>> GetSalesHeatmapAsync(long branchId, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        var orders = await _orderRepository.GetActiveOrdersAsync(branchId, null, ct);
        var periodOrders = orders
            .Where(o => !o.IsRefund && o.CreationDate >= startDate && o.CreationDate <= endDate)
            .ToList();

        var result = new SalesHeatmapDto { BranchId = branchId };
        var grouped = periodOrders
            .GroupBy(o => new { Day = (int)o.CreationDate.DayOfWeek, Hour = o.CreationDate.Hour })
            .Select(g => new HeatmapCellDto
            {
                DayOfWeek = g.Key.Day,
                HourOfDay = g.Key.Hour,
                OrderCount = g.Count(),
                TotalSales = g.Sum(o => o.TotalAmount)
            }).ToList();

        result.Cells = grouped;
        return ApiResponse<SalesHeatmapDto>.CreateSuccess(result);
    }
}
