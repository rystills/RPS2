using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

public class GameHub : Hub
{
    private readonly IHubContext<GameHub> _hubContext;

    public GameHub(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // players connected to the server
    private static HashSet<string> _connectedUsers = [];

    // players connected to matchmaking
    private static Dictionary<string, string> _waitingPlayers = [];

    // players connected to random matchmaking
    private static Queue<string> _waitingRandoms = new();

    // teams in matchmaking step 2
    private static Dictionary<string, string> _waitingPairLeaders = [];
    
    // rooms
    private static List<(string Team1Player1, string Team1Player2, string Team2Player1, string Team2Player2)> _rooms = [];

    // player actions
    private static Dictionary<string, string> _playerActions = [];

    private static int _roundTime = 10000;

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

    private string GetFullConnectionId(string partialConnectionId) => _connectedUsers.First(u => u.StartsWith(partialConnectionId));

    public async Task StartMatchmaking(string friendConnectionId)
    {
        string userConnectionId = Context.ConnectionId.Substring(0, 8);


        if (friendConnectionId.Length == 0)
        {
            string possibleFriend;
            if (_waitingRandoms.TryDequeue(out possibleFriend))
            {
                await MatchTeams(userConnectionId, possibleFriend);
            }
            else
            {
                _waitingRandoms.Enqueue(userConnectionId);
            }
        }

        else
        {
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
    }

    public void SubmitMove(string action)
    {
        Debug.WriteLine(Context.ConnectionId + " " + action);
        _playerActions[Context.ConnectionId.Substring(0, 8)] = action;
    }
    
    public async Task StartRoundTimer((string team1Player1, string team1Player2, string team2Player1, string team2Player2) room)
    {
        // wait for round timer to expire, or for all players to submit their move
        int divisions = 10;
        for (int div = 0; div < divisions; div++)
        {
            string[] roomPlayers = { room.team1Player1, room.team1Player2, room.team2Player1, room.team2Player2 };
            // TODO: don't require an action for dead players
            if (roomPlayers.All(_playerActions.ContainsKey))
            {
                break;
            }
            await Task.Delay(_roundTime / divisions);
        }

        // moves default to rock
        string t1p1Move = _playerActions.ContainsKey(room.team1Player1) ? _playerActions[room.team1Player1] : "0";
        string t1p2Move = _playerActions.ContainsKey(room.team1Player2) ? _playerActions[room.team1Player2] : "0";
        string t2p1Move = _playerActions.ContainsKey(room.team2Player1) ? _playerActions[room.team2Player1] : "0";
        string t2p2Move = _playerActions.ContainsKey(room.team2Player2) ? _playerActions[room.team2Player2] : "0";
        
        // clear moves after processing
        _playerActions.Remove(room.team1Player1);
        _playerActions.Remove(room.team1Player2);
        _playerActions.Remove(room.team2Player1);
        _playerActions.Remove(room.team2Player2);

        // send moves to each client in their expected order
        await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player1)).SendAsync("ReceiveMoves", t1p1Move + t2p1Move + t1p2Move + t2p2Move);
        await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player2)).SendAsync("ReceiveMoves", t1p2Move + t2p2Move + t1p1Move + t2p1Move);
        await _hubContext.Clients.Client(GetFullConnectionId(room.team2Player1)).SendAsync("ReceiveMoves", t2p1Move + t1p1Move + t2p2Move + t1p2Move);
        await _hubContext.Clients.Client(GetFullConnectionId(room.team2Player2)).SendAsync("ReceiveMoves", t2p2Move + t1p2Move + t2p1Move + t1p1Move);

        // wait 3 seconds and then start the next round
        // TODO: stop on game over
        await Task.Delay(3000);
        await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player1)).SendAsync("StartRound");
        await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player2)).SendAsync("StartRound");
        await _hubContext.Clients.Client(GetFullConnectionId(room.team2Player1)).SendAsync("StartRound");
        await _hubContext.Clients.Client(GetFullConnectionId(room.team2Player2)).SendAsync("StartRound");
        
        _ = StartRoundTimer(room);
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
            (string, string, string, string) room = (player1, player2, team2Player1, team2Player2);
            _rooms.Add(room);

            // notify players that they have joined the room
            await Clients.Client(GetFullConnectionId(player1)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);
            await Clients.Client(GetFullConnectionId(player2)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);
            await Clients.Client(GetFullConnectionId(team2Player1)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);
            await Clients.Client(GetFullConnectionId(team2Player2)).SendAsync("JoinRoom", player1, player2, team2Player1, team2Player2);

            _ = StartRoundTimer(room);
        }
        else
        {
            // add pair to waiting list
            _waitingPairLeaders[player1] = player2;
        }
    }
}
