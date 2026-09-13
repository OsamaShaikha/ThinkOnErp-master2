using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.API.Hubs.Pos;

public class PosCfdHub : Hub
{
    public async Task JoinTillCfd(string tillId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"till_cfd_{tillId}");
    }

    public async Task LeaveTillCfd(string tillId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"till_cfd_{tillId}");
    }

    public async Task BroadcastCartState(string tillId, CfdCartStateDto cartState)
    {
        await Clients.Group($"till_cfd_{tillId}").SendAsync("OnCartStateChanged", cartState);
    }
}

public class PosTableStatusHub : Hub
{
    public async Task JoinBranchTables(string branchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"branch_tables_{branchId}");
    }

    public async Task LeaveBranchTables(string branchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"branch_tables_{branchId}");
    }

    public async Task BroadcastTableStatus(string branchId, long tableId, string status, long? orderId)
    {
        await Clients.Group($"branch_tables_{branchId}").SendAsync("OnTableStatusChanged", tableId, status, orderId);
    }
}
