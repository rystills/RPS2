using Microsoft.AspNetCore.SignalR;

public class Room
{
    public string team1Player1 { get; set; }
    public string team1Player2 { get; set; }
    public string team2Player1 { get; set; }
    public string team2Player2 { get; set; }
    public string[] players => [team1Player1, team2Player1, team1Player2, team2Player2];
    public Dictionary<string, bool> playersAlive { get; set; } = [];
    public Dictionary<string, string> playerMoves { get; set; } = [];
    public int inactivityCount { get; set; }

    public Room(string team1Player1, string team1Player2, string team2Player1, string team2Player2)
    {
        this.team1Player1 = team1Player1;
        this.team1Player2 = team1Player2;
        this.team2Player1 = team2Player1;
        this.team2Player2 = team2Player2;

        foreach (string player in players)
        {
            playersAlive[player] = true;
            GameHub.roomMap[player] = this;
        }
    }
}

public class GameHub : Hub
{
    private readonly IHubContext<GameHub> _hubContext;

    public GameHub(IHubContext<GameHub> hubContext)
        => _hubContext = hubContext;

    // players connected to the server and their corresponding nicknames
    private static Dictionary<string, string> _connectedUsers = [];

    // players connected to matchmaking and their corresponding partner IDs
    private static Dictionary<string, string> _waitingPlayers = [];

    // players connected to random matchmaking
    private static Queue<string> _waitingRandoms = new();

    // teams in matchmaking step 2
    private static Dictionary<string, string> _waitingPairLeaders = [];
    private static Dictionary<string, string> _reversePairs = [];
    private static Dictionary<string, string> _specifiedPairs = [];

    // player actions
    private static Dictionary<string, string> _playerActions = [];

    // round timer in ms
    private static int _roundTime = 10000;

    // player ids and their corresponding rooms
    public static Dictionary<string, Room> roomMap = [];

    public override async Task OnConnectedAsync()
    {
        _connectedUsers[Context.ConnectionId] = "";
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (roomMap.ContainsKey(Context.ConnectionId.Substring(0, 8)))
            LeaveLobby();
        else
            await LeaveMatchmaking();
        _connectedUsers.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private string GetFullConnectionId(string partialConnectionId) => _connectedUsers.First(u => u.Key.StartsWith(partialConnectionId)).Key;

    public async Task StartMatchmaking(string friendConnectionId, string name)
    {
        // update nickname
        _connectedUsers[Context.ConnectionId] = name;
        string userConnectionId = Context.ConnectionId.Substring(0, 8);

        // random matchmaking
        if (friendConnectionId.Length == 0)
        {
            // enter team matchmaking with first available random
            if (_waitingRandoms.TryDequeue(out string? possibleFriend))
                await MatchTeams(userConnectionId, possibleFriend);
            
            // no randoms available; wait in random matchmaking queue
            else _waitingRandoms.Enqueue(userConnectionId);
        }

        // friend matchmaking
        else
        {
            // check if friend has already requested user
            _specifiedPairs[userConnectionId] = friendConnectionId;
            if (_waitingPlayers.ContainsKey(friendConnectionId) && _waitingPlayers[friendConnectionId] == userConnectionId)
            {
                // user and friend requested each other; attempt team matchmaking
                _waitingPlayers.Remove(friendConnectionId);
                await MatchTeams(friendConnectionId, userConnectionId);
            }
            
            // add user to waiting list, requesting friend
            else _waitingPlayers[userConnectionId] = friendConnectionId;
        }
    }

    public void SubmitMove(string action)
        => _playerActions[Context.ConnectionId.Substring(0, 8)] = action;

    public void LeaveLobby()
    {
        string playerCode = Context.ConnectionId.Substring(0, 8);
        roomMap.Remove(playerCode);
    }

    public async Task LeaveMatchmaking()
    {
        string playerCode = Context.ConnectionId.Substring(0, 8);

        // remove from team matchmaking
        if (_waitingPairLeaders.ContainsKey(playerCode) || _reversePairs.ContainsKey(playerCode))
        {
            // determine whether you're in the primary or reverse matchmaking pair dict
            Dictionary<string, string> yourDict;
            Dictionary<string, string> partnerDict;
            if (_waitingPairLeaders.ContainsKey(playerCode))
            {
                yourDict = _waitingPairLeaders;
                partnerDict = _reversePairs;
            }
            else
            {
                yourDict = _reversePairs;
                partnerDict = _waitingPairLeaders;
            }
            string partnerCode = yourDict[playerCode];
            
            // remove you and your partner from the matchmaking pair dicts
            yourDict.Remove(playerCode);
            partnerDict.Remove(partnerCode);

            // if players connected via friend matchmaking, kick both to the main menu
            if (_specifiedPairs.ContainsKey(playerCode))
            {
                _specifiedPairs.Remove(playerCode);
                _specifiedPairs.Remove(partnerCode);
                await _hubContext.Clients.Client(GetFullConnectionId(playerCode)).SendAsync("SetInactive", "Your partner has disconnected during matchmaking, make sure you use the correct code if you retry");
                await _hubContext.Clients.Client(GetFullConnectionId(partnerCode)).SendAsync("SetInactive", "Your partner has disconnected during matchmaking, make sure you use the correct code if you retry");
            }

            // if players connected randomly, send your partner back to the random queue
            else
            {
                await Clients.Client(GetFullConnectionId(partnerCode)).SendAsync("UpdateMatchmakingProgress", 1);
                _waitingRandoms.Enqueue(partnerCode);
            }
        }

        // remove from friend matchmaking
        else if (_waitingPlayers.ContainsKey(playerCode))
            _waitingPlayers.Remove(playerCode);
        
        // remove from random matchmaking
        else if (_waitingRandoms.Contains(playerCode))
            _waitingRandoms = new(_waitingRandoms.Where(s => s != playerCode));
    }

    // res: -1 = loss, 0 = tie, 1 = win
    private int GetRPSResult(int a, int b)
    {
        // rock
        if (a == 0)
            return b == 0 ? 0 : (b == 1 ? -1 : 1);
        // paper
        if (a == 1)
            return b == 0 ? 1 : (b == 1 ? 0 : -1);
        // scissors
        return b == 0 ? -1 : (b == 1 ? 1 : 0);
    }

    private void setPlayersAlive(bool[] playersAlive, int[] playerChoices)
    {
        void UpdatePlayersAlive(int p1, int p2, int p3, int p4)
        {
            int res1 = GetRPSResult(playerChoices[p1], playerChoices[p2]);
            int res2 = GetRPSResult(playerChoices[p3], playerChoices[p4]);
            playersAlive[p1] &= res1 >= 0;
            playersAlive[p2] &= -res1 >= 0;
            playersAlive[p3] &= res2 >= 0;
            playersAlive[p4] &= -res2 >= 0;
        }

        // evaluate round
        // all players alive
        if (playersAlive[0] && playersAlive[1] && playersAlive[2] && playersAlive[3])
            UpdatePlayersAlive(0, 1, 2, 3);
        
        // you dead
        else if (!playersAlive[0] && playersAlive[1] && playersAlive[2] && playersAlive[3])
            UpdatePlayersAlive(2, 1, 2, 3);
        
        // partner dead
        else if (playersAlive[0] && playersAlive[1] && !playersAlive[2] && playersAlive[3])
            UpdatePlayersAlive(0, 1, 0, 3);
        
        // your enemy dead
        else if (playersAlive[0] && !playersAlive[1] && playersAlive[2] && playersAlive[3])
            UpdatePlayersAlive(0, 3, 2, 3);
        
        // partner enemy dead
        else if (playersAlive[0] && playersAlive[1] && playersAlive[2] && !playersAlive[3])
            UpdatePlayersAlive(0, 1, 2, 1);
        
        // one from each team dead
        else
        {
            int teamAlive = playersAlive[0] ? 0 : 2;
            int enemyAlive = playersAlive[1] ? 1 : 3;
            int teamRes = GetRPSResult(playerChoices[teamAlive], playerChoices[enemyAlive]);
            playersAlive[teamAlive] = teamRes >= 0;
            playersAlive[enemyAlive] = -teamRes >= 0;
        }
    }

    private bool PlayerInRoom(string playerIdShort, Room room)
        => roomMap.ContainsKey(playerIdShort) && roomMap[playerIdShort] == room;

    public async Task StartRoundTimer(Room room)
    {
        // wait for round timer to expire, or for all players to submit their move
        // If a full team has wiped, reset
        if ((!room.playersAlive[room.team1Player1] && !room.playersAlive[room.team1Player2]) 
            || (!room.playersAlive[room.team2Player1] && !room.playersAlive[room.team2Player2]))
        {
            foreach(string player in room.players)
                room.playersAlive[player] = true;
        }

        int divisions = 20;
        HashSet<string> movesReceived = [];
        for (int div = 0; div < divisions; ++div)
        {
            bool playerStillInRoom = false;
            bool allPlayersChosen = true;
            foreach (string roomPlayer in room.players)
            {
                // If any player is still in the room, continue the game
                playerStillInRoom |= PlayerInRoom(roomPlayer, room);

                // alert all players each time a move is entered
                if (_playerActions.ContainsKey(roomPlayer) && !movesReceived.Contains(roomPlayer))
                {
                    movesReceived.Add(roomPlayer);
                    foreach (string sendPlayer in room.players.Where(p => PlayerInRoom(p, room)))
                        await _hubContext.Clients.Client(GetFullConnectionId(sendPlayer)).SendAsync("PlayerMoved", roomPlayer);
                }

                // Break if all alive players have made a choice
                allPlayersChosen &= _playerActions.ContainsKey(roomPlayer) || !room.playersAlive[roomPlayer];
            }

            // If all players have exited the room, stop the game loop
            // Once we have proper server-authoritative logic, we can check win conditions and things here. 
            if (!playerStillInRoom) return;

            // stop round timer immediately once all players have selected a move
            if (allPlayersChosen) break;

            await Task.Delay(_roundTime / divisions);
        }

        // After ten rounds on inactivity, stop server
        if (movesReceived.Count == 0 && ++room.inactivityCount > 10)
        {
            foreach (string player in room.players.Where(p => PlayerInRoom(p, room)))
            {
                await _hubContext.Clients.Client(GetFullConnectionId(player)).SendAsync("SetInactive", "Your room has gone inactive");
                roomMap.Remove(player);
            }
            return;
        }

        // parse player choices and alive status
        int[] playerChoices = new int[4];
        bool[] playersAlive = new bool[4];
        for(int i = 0; i < room.players.Length; ++i)
        {
            string player = room.players[i];

            // default to rock
            if (!_playerActions.ContainsKey(player))
                _playerActions[player] = "0";
            
            playerChoices[i] = int.Parse(_playerActions[player]);
            playersAlive[i] = room.playersAlive[player];
        }

        // update players alive status
        setPlayersAlive(playersAlive, playerChoices);
        for (int i = 0; i < room.players.Length; ++i)
            room.playersAlive[room.players[i]] = playersAlive[i];

        // record player moves
        string t1p1Move = _playerActions[room.team1Player1];
        string t1p2Move = _playerActions[room.team1Player2];
        string t2p1Move = _playerActions[room.team2Player1];
        string t2p2Move = _playerActions[room.team2Player2];

        // clear moves after processing
        foreach (string player in room.players)
            _playerActions.Remove(player);

        // send moves to each client in their expected order
        // send alive to players as well to avoid going out of sync
        if (PlayerInRoom(room.team1Player1, room))
            await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player1)).SendAsync("ReceiveMoves", t1p1Move + t2p1Move + t1p2Move + t2p2Move, room.playersAlive);
        if (PlayerInRoom(room.team1Player2, room))
            await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player2)).SendAsync("ReceiveMoves", t1p2Move + t2p2Move + t1p1Move + t2p1Move, room.playersAlive);
        if (PlayerInRoom(room.team2Player1, room))
            await _hubContext.Clients.Client(GetFullConnectionId(room.team2Player1)).SendAsync("ReceiveMoves", t2p1Move + t1p1Move + t2p2Move + t1p2Move, room.playersAlive);
        if (PlayerInRoom(room.team2Player2, room))
            await _hubContext.Clients.Client(GetFullConnectionId(room.team2Player2)).SendAsync("ReceiveMoves", t2p2Move + t1p2Move + t2p1Move + t1p1Move, room.playersAlive);

        // wait 3 seconds and then start the next round
        await Task.Delay(3000);
        if (PlayerInRoom(room.team1Player1, room))
            await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player1)).SendAsync("StartRound");
        if (PlayerInRoom(room.team1Player2, room))
            await _hubContext.Clients.Client(GetFullConnectionId(room.team1Player2)).SendAsync("StartRound");
        if (PlayerInRoom(room.team2Player1, room))
            await _hubContext.Clients.Client(GetFullConnectionId(room.team2Player1)).SendAsync("StartRound");
        if (PlayerInRoom(room.team2Player2, room))
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
            _reversePairs.Remove(team2Player2);

            // notify players that they have joined the room
            await Clients.Client(GetFullConnectionId(player1)).SendAsync("JoinRoom",
                _connectedUsers[GetFullConnectionId(player1)], _connectedUsers[GetFullConnectionId(player2)], _connectedUsers[GetFullConnectionId(team2Player1)], _connectedUsers[GetFullConnectionId(team2Player2)],
                player1, player2, team2Player1, team2Player2);

            await Clients.Client(GetFullConnectionId(player2)).SendAsync("JoinRoom",
                _connectedUsers[GetFullConnectionId(player2)], _connectedUsers[GetFullConnectionId(player1)], _connectedUsers[GetFullConnectionId(team2Player2)], _connectedUsers[GetFullConnectionId(team2Player1)],
                player2, player1, team2Player2, team2Player1);

            await Clients.Client(GetFullConnectionId(team2Player1)).SendAsync("JoinRoom",
                _connectedUsers[GetFullConnectionId(team2Player1)],  _connectedUsers[GetFullConnectionId(team2Player2)], _connectedUsers[GetFullConnectionId(player1)], _connectedUsers[GetFullConnectionId(player2)],
                team2Player1, team2Player2, player1, player2);

            await Clients.Client(GetFullConnectionId(team2Player2)).SendAsync("JoinRoom",
                _connectedUsers[GetFullConnectionId(team2Player2)], _connectedUsers[GetFullConnectionId(team2Player1)], _connectedUsers[GetFullConnectionId(player2)], _connectedUsers[GetFullConnectionId(player1)],
                team2Player2, team2Player1, player2, player1);

            _ = StartRoundTimer(new Room(player1, player2, team2Player1, team2Player2));
        }
        else
        {
            // add pair to waiting list
            _waitingPairLeaders[player1] = player2;
            _reversePairs[player2] = player1;
            await Clients.Client(GetFullConnectionId(player1)).SendAsync("UpdateMatchmakingProgress", 2);
            await Clients.Client(GetFullConnectionId(player2)).SendAsync("UpdateMatchmakingProgress", 2);
        }
    }
}
