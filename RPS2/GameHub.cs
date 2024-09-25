using Microsoft.AspNetCore.SignalR;

public class GameHub : Hub
{
    private static HashSet<string> _connectedUsers = new HashSet<string>();

    public override async Task OnConnectedAsync()
    {
        _connectedUsers.Add(Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _connectedUsers.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    
    public async Task SendMove(string roomCode, string playerMove)
    {
        // broadcast the move to all players in the room
        await Clients.Group(roomCode).SendAsync("ReceiveMove", playerMove);
    }

    public async Task JoinRoom(string roomCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
    }
}