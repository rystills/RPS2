using Microsoft.AspNetCore.SignalR;

public class GameHub : Hub
{
    // players connected to the server
    private static HashSet<string> _connectedUsers = [];

    // players connected to matchmaking
    private static Dictionary<string, string> _waitingPlayers = [];
    
    // teams in matchmaking step 2
    private static Dictionary<string, string> _waitingPairLeaders = [];
    
    // rooms
    private static List<(string Team1Player1, string Team1Player2, string Team2Player1, string Team2Player2)> _rooms = [];

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
        await Groups.AddToGroupAsync(Context.ConnectionId.Substring(0, 8), roomCode);
    }

    private string GetFullConnectionId(string partialConnectionId) => _connectedUsers.First(u => u.StartsWith(partialConnectionId));

    public async Task StartMatchmaking(string friendConnectionId)
    {
        string userConnectionId = Context.ConnectionId.Substring(0, 8);

        // check if friend has already requested user
        if (_waitingPlayers.ContainsKey(friendConnectionId) && _waitingPlayers[friendConnectionId] == userConnectionId)
        {
            // user and friend requested each other; attempt team matchmaking
            _waitingPlayers.Remove(friendConnectionId);
            await MatchTeams(friendConnectionId, userConnectionId);
        }
        else
        {
            // add user to waiting list, requesting friend
            _waitingPlayers[userConnectionId] = friendConnectionId;
        }
    }

    private async Task MatchTeams(string player1, string player2)
    {
        // check if there is already a pair waiting
        if (_waitingPairLeaders.Count > 0)
        {
            // take first pair from waiting list
            var otherPair = _waitingPairLeaders.First();
            var team2Player1 = otherPair.Key;
            var team2Player2 = otherPair.Value;
            _waitingPairLeaders.Remove(team2Player1);

            // create a new room
            _rooms.Add((player1, player2, team2Player1, team2Player2));

            // notify players that they have joined the room
            await Clients.Client(GetFullConnectionId(player1)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);
            await Clients.Client(GetFullConnectionId(player2)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);
            await Clients.Client(GetFullConnectionId(team2Player1)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);
            await Clients.Client(GetFullConnectionId(team2Player2)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);
        }
        else
        {
            // add pair to waiting list
            _waitingPairLeaders[player1] = player2;
        }
    }
}
