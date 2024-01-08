using Microsoft.AspNetCore.SignalR;

namespace VideoWebapp.Hubs
{
    public class LivestreamHub : Hub
    {
        public async Task JoinRoom(string roomId, string userId)
        {
            LivestreamUsers.list.Add(Context.ConnectionId, userId);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("user-connected", userId);
        }
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Clients.All.SendAsync("user-disconnected", LivestreamUsers.list[Context.ConnectionId]);
            return base.OnDisconnectedAsync(exception);
        }
    }

    public static class LivestreamUsers
    {
        public static IDictionary<string, string> list = new Dictionary<string, string>();
    }
}
