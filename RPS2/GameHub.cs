using Microsoft.AspNetCore.SignalR;

public class GameHub : Hub
{
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