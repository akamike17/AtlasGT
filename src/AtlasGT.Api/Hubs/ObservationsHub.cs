using System.Threading.Tasks;
using AtlasGT.Domain.Models;
using Microsoft.AspNetCore.SignalR;

namespace AtlasGT.Api.Hubs
{
    /// <summary>Hub de observations para actualizaciones en tiempo real.</summary>
    public sealed class ObservationsHub : Hub
    {
        public Task JoinAsset(string assetId) =>
            Groups.AddToGroupAsync(Context.ConnectionId, $"asset-{assetId}");

        public Task LeaveAsset(string assetId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, $"asset-{assetId}");
    }
}
