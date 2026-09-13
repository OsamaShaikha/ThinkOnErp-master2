using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.API.Hubs.Pos;

public class PosKdsHub : Hub
{
    public async Task JoinBranchKitchen(string branchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"branch_kds_{branchId}");
    }

    public async Task LeaveBranchKitchen(string branchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"branch_kds_{branchId}");
    }

    public async Task BroadcastNewTicket(string branchId, KdsTicketDto ticket)
    {
        await Clients.Group($"branch_kds_{branchId}").SendAsync("OnNewKitchenTicket", ticket);
    }

    public async Task BroadcastTicketUpdated(string branchId, long orderId, string status)
    {
        await Clients.Group($"branch_kds_{branchId}").SendAsync("OnTicketStatusChanged", orderId, status);
    }
}
