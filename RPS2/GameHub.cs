using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Numerics;

public class Room
{
    public string team1Player1 { get; set; }
    public string team1Player2 { get; set; }
    public string team2Player1 { get; set; }
    public string team2Player2 { get; set; }
    public Dictionary<string, bool> playersAlive { get; set; }
    public Dictionary<string, string> playerMoves { get; set; }
    public int inactivityCount { get; set; }

    public Room(string team1Player1, string team1Player2, string team2Player1, string team2Player2, Dictionary<string, bool> playersAlive, Dictionary<string, string> playerMoves)
    {
        this.team1Player1 = team1Player1;
        this.team1Player2 = team1Player2;
        this.team2Player1 = team2Player1;
        this.team2Player2 = team2Player2;
        this.playersAlive = playersAlive;
        this.playerMoves = playerMoves;
        inactivityCount = 0;
    }
}

public class GameHub : Hub
{
    private readonly IHubContext<GameHub> _hubContext;

    public GameHub(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // players connected to the server
    private static Dictionary<string, string> _connectedUsers = [];

    // players connected to matchmaking
    private static Dictionary<string, string> _waitingPlayers = [];

    // players connected to random matchmaking
    private static Queue<string> _waitingRandoms = new();

    // teams in matchmaking step 2
    private static Dictionary<string, string> _waitingPairLeaders = [];
    private static Dictionary<string, string> _reversePairs = [];
    private static Dictionary<string, string> _specifiedPairs = [];

    // player actions
    private static Dictionary<string, string> _playerActions = [];

    private static int _roundTime = 10000;

    private static Dictionary<string, Room> roomMap = [];

    public override async Task OnConnectedAsync()
    {
        _connectedUsers[Context.ConnectionId] = "";
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (roomMap.ContainsKey(Context.ConnectionId.Substring(0, 8)))
        {
            LeaveLobby();
        }
        else
        {
            await LeaveMatchmaking();
        }
        _connectedUsers.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private string GetFullConnectionId(string partialConnectionId) => _connectedUsers.First(u => u.Key.StartsWith(partialConnectionId)).Key;

    public async Task StartMatchmaking(string friendConnectionId, string name)
    {
        _connectedUsers[Context.ConnectionId] = name;
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
            _specifiedPairs[userConnectionId] = friendConnectionId;
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
        _playerActions[Context.ConnectionId.Substring(0, 8)] = action;
    }

    public void LeaveLobby()
    {
        string playerCode = Context.ConnectionId.Substring(0, 8);
        roomMap.Remove(playerCode);
    }

    public async Task LeaveMatchmaking()
    {
        string playerCode = Context.ConnectionId.Substring(0, 8);

        if (_waitingPairLeaders.ContainsKey(playerCode) || _reversePairs.ContainsKey(playerCode))
        {
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
                partnerDict = _reversePairs;
            }
            string partner = yourDict[playerCode];
            yourDict.Remove(playerCode);
            partnerDict.Remove(partner);

            if (_specifiedPairs.ContainsKey(playerCode))
            {
                _specifiedPairs.Remove(playerCode);
                _specifiedPairs.Remove(partner);
                await _hubContext.Clients.Client(GetFullConnectionId(playerCode)).SendAsync("SetInactive", "Your partner has disconnected during matchmaking, make sure you use the correct code if you retry");
                await _hubContext.Clients.Client(GetFullConnectionId(partner)).SendAsync("SetInactive", "Your partner has disconnected during matchmaking, make sure you use the correct code if you retry");
            }
            else
            {
                await Clients.Client(GetFullConnectionId(partner)).SendAsync("UpdateMatchmakingProgress", 1);
                _waitingRandoms.Enqueue(partner);
            }
            
        }
        else if (_waitingPlayers.ContainsKey(playerCode))
        {
            _waitingPlayers.Remove(playerCode);
        }
        else if (_waitingRandoms.Contains(playerCode))
        {
            _waitingRandoms = new Queue<string>(_waitingRandoms.Where(s => s != playerCode));
        }
    }


    // res: -1 = loss, 0 = tie, 1 = win
    private int GetRPSResult(int a, int b)
    {
        // rock
        if (a == 0)
        {
            return b == 0 ? 0 : (b == 1 ? -1 : 1);
        }
        // paper
        if (a == 1)
        {
            return b == 0 ? 1 : (b == 1 ? 0 : -1);
        }
        // scissors
        return b == 0 ? -1 : (b == 1 ? 1 : 0);
    }

    private void setPlayersAlive(bool[] playersAlive, int[] playerChoices)
    {
        // evaluate round 
        // all players alive
        if (playersAlive[0] && playersAlive[1] && playersAlive[2] && playersAlive[3])
        {
            int selfRes = GetRPSResult(playerChoices[0], playerChoices[1]);
            int selfEnemyRes = -selfRes;
            int partnerRes = GetRPSResult(playerChoices[2], playerChoices[3]);
            int partnerEnemyRes = -partnerRes;
            playersAlive[0] = selfRes >= 0;
            playersAlive[1] = selfEnemyRes >= 0;
            playersAlive[2] = partnerRes >= 0;
            playersAlive[3] = partnerEnemyRes >= 0;
        }
        // you dead
        else if (!playersAlive[0] && playersAlive[1] && playersAlive[2] && playersAlive[3])
        {
            int partnerRes1 = GetRPSResult(playerChoices[2], playerChoices[1]);
            int selfEnemyRes = -partnerRes1;
            int partnerRes2 = GetRPSResult(playerChoices[2], playerChoices[3]);
            int partnerEnemyRes = -partnerRes2;
            playersAlive[1] = selfEnemyRes >= 0;
            playersAlive[2] = partnerRes2 >= 0;
            playersAlive[3] = partnerEnemyRes >= 0;
        }
        // partner dead
        else if (playersAlive[0] && playersAlive[1] && !playersAlive[2] && playersAlive[3])
        {
            int selfRes1 = GetRPSResult(playerChoices[0], playerChoices[1]);
            int selfEnemyRes = -selfRes1;
            int selfRes2 = GetRPSResult(playerChoices[0], playerChoices[3]);
            int partnerEnemyRes = -selfRes2;
            playersAlive[0] = selfRes1 >= 0 && selfRes2 >= 0;
            playersAlive[1] = selfEnemyRes >= 0;
            playersAlive[3] = partnerEnemyRes >= 0;
        }
        // your enemy dead
        else if (playersAlive[0] && !playersAlive[1] && playersAlive[2] && playersAlive[3])
        {
            int selfRes = GetRPSResult(playerChoices[0], playerChoices[3]);
            int partnerEnemyRes1 = -selfRes;
            int partnerRes = GetRPSResult(playerChoices[2], playerChoices[3]);
            int partnerEnemyRes2 = -partnerRes;
            playersAlive[0] = selfRes >= 0;
            playersAlive[2] = partnerRes >= 0;
            playersAlive[3] = partnerEnemyRes1 >= 0 && partnerEnemyRes2 >= 0;
        }
        // partner enemy dead
        else if (playersAlive[0] && playersAlive[1] && playersAlive[2] && !playersAlive[3])
        {
            int selfRes = GetRPSResult(playerChoices[0], playerChoices[1]);
            int selfEnemyRes1 = -selfRes;
            int partnerRes = GetRPSResult(playerChoices[2], playerChoices[1]);
            int selfEnemyRes2 = -partnerRes;
            playersAlive[0] = selfRes >= 0;
            playersAlive[1] = selfEnemyRes1 >= 0 && selfEnemyRes2 >= 0;
            playersAlive[2] = partnerRes >= 0;
        }

        // two dead
        else
        {
            // there shouldn't be a case where two on the same team are alive as the round would be reset
            int teamAlive = playersAlive[0] ? 0 : 2;
            int enemyAlive = playersAlive[1] ? 1 : 3;
            int teamRes = GetRPSResult(playerChoices[teamAlive], playerChoices[enemyAlive]);
            int enemyRes = -teamRes;
            playersAlive[teamAlive] = teamRes >= 0;
            playersAlive[enemyAlive] = enemyRes >= 0;
        }
    }

    public async Task StartRoundTimer(Room room)
    {
        // wait for round timer to expire, or for all players to submit their move
        string team1Player1 = room.team1Player1;
        string team1Player2 = room.team1Player2;
        string team2Player1 = room.team2Player1;
        string team2Player2 = room.team2Player2;
        string[] roomPlayers = { team1Player1, team2Player1, team1Player2, team2Player2 };

        int divisions = 20;
        HashSet<string> movesReceived = [];
        for (int div = 0; div < divisions; div++)
        {
            bool playerStillInRoom = false;
            bool shouldBreak = true;
            for (int i = 0; i < roomPlayers.Length; i++)
            {
                string roomPlayer = roomPlayers[i];
                // If any player is still in the room, continue the game
                if (roomMap.ContainsKey(roomPlayer) && roomMap[roomPlayer] == room)
                {
                    playerStillInRoom = true;
                }

                if (_playerActions.ContainsKey(roomPlayer) && !movesReceived.Contains(roomPlayer))
                {
                    string act = _playerActions[roomPlayer];
                    foreach (string sendPlayer in roomPlayers)
                    {
                        await _hubContext.Clients.Client(GetFullConnectionId(sendPlayer)).SendAsync("PlayerMoved", roomPlayer);
                    }
                    movesReceived.Add(roomPlayer);
                }

                // Break if all alive players have made a choice
                if (!_playerActions.ContainsKey(roomPlayer) && room.playersAlive[roomPlayer])
                {
                    shouldBreak = false;
                }
            }


            // If all players have exited the room, stop the game loop
            // Once we have proper server-authoritative logic, we can check win conditions and things here. 
            if (!playerStillInRoom)
            {
                return;
            }

            if (shouldBreak)
            {
                break;
            }
            await Task.Delay(_roundTime / divisions);
        }

        // After ten rounds on inactivity, stop server
        if(movesReceived.Count == 0)
        {
            room.inactivityCount++;
            if (room.inactivityCount > 10)
            {
                foreach (string player in roomPlayers)
                {
                    await _hubContext.Clients.Client(GetFullConnectionId(player)).SendAsync("SetInactive", "Your room has gone inactive");
                    roomMap.Remove(player);
                }
                return;
            }
        }

        // The things we do to not have to rewrite the RPS logic when tired...
        int[] playerChoices = new int[4];
        bool[] playersAlive = new bool[4];
        for(int i = 0; i < roomPlayers.Length; ++i)
        {
            string player = roomPlayers[i];

            if (!_playerActions.ContainsKey(player))
            {
                _playerActions[player] = "0"; // Default to rock
            }
            playerChoices[i] = Int32.Parse(_playerActions[player]);
            playersAlive[i] = room.playersAlive[player];
        }

        setPlayersAlive(playersAlive, playerChoices);
        for (int i = 0; i < roomPlayers.Length; ++i)
        {
            string player = roomPlayers[i];
            room.playersAlive[player] = playersAlive[i];
        }

        string t1p1Move = _playerActions[team1Player1];
        string t1p2Move = _playerActions[team1Player2];
        string t2p1Move = _playerActions[team2Player1];
        string t2p2Move = _playerActions[team2Player2];

        // clear moves after processing
        _playerActions.Remove(team1Player1);
        _playerActions.Remove(team1Player2);
        _playerActions.Remove(team2Player1);
        _playerActions.Remove(team2Player2);

        // send moves to each client in their expected order
        await _hubContext.Clients.Client(GetFullConnectionId(team1Player1)).SendAsync("ReceiveMoves", t1p1Move + t2p1Move + t1p2Move + t2p2Move);
        await _hubContext.Clients.Client(GetFullConnectionId(team1Player2)).SendAsync("ReceiveMoves", t1p2Move + t2p2Move + t1p1Move + t2p1Move);
        await _hubContext.Clients.Client(GetFullConnectionId(team2Player1)).SendAsync("ReceiveMoves", t2p1Move + t1p1Move + t2p2Move + t1p2Move);
        await _hubContext.Clients.Client(GetFullConnectionId(team2Player2)).SendAsync("ReceiveMoves", t2p2Move + t1p2Move + t2p1Move + t1p1Move);

        // wait 3 seconds and then start the next round
        // TODO: stop on game over
        await Task.Delay(3000);
        await _hubContext.Clients.Client(GetFullConnectionId(team1Player1)).SendAsync("StartRound");
        await _hubContext.Clients.Client(GetFullConnectionId(team1Player2)).SendAsync("StartRound");
        await _hubContext.Clients.Client(GetFullConnectionId(team2Player1)).SendAsync("StartRound");
        await _hubContext.Clients.Client(GetFullConnectionId(team2Player2)).SendAsync("StartRound");
        
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
                player1, player2, team2Player1, team2Player2
                );

            await Clients.Client(GetFullConnectionId(player2)).SendAsync("JoinRoom",
                _connectedUsers[GetFullConnectionId(player2)], _connectedUsers[GetFullConnectionId(player1)], _connectedUsers[GetFullConnectionId(team2Player2)], _connectedUsers[GetFullConnectionId(team2Player1)],
                player2, player1, team2Player2, team2Player1
                );

            await Clients.Client(GetFullConnectionId(team2Player1)).SendAsync("JoinRoom",
                _connectedUsers[GetFullConnectionId(team2Player1)],  _connectedUsers[GetFullConnectionId(team2Player2)], _connectedUsers[GetFullConnectionId(player1)], _connectedUsers[GetFullConnectionId(player2)],
                team2Player1, team2Player2, player1, player2
                );

            await Clients.Client(GetFullConnectionId(team2Player2)).SendAsync("JoinRoom",
                _connectedUsers[GetFullConnectionId(team2Player2)], _connectedUsers[GetFullConnectionId(team2Player1)], _connectedUsers[GetFullConnectionId(player2)], _connectedUsers[GetFullConnectionId(player1)],
                team2Player2, team2Player1, player2, player1
                );

            bool[] roomAlive = { true, true, true, true };
            Dictionary<string, bool> playersAlive = [];
            Dictionary<string, string> roomMoves = [];
            string[] roomPlayers = { player1, team2Player1, player2, team2Player2 };
            Room room = new Room(player1, player2, team2Player1, team2Player2, playersAlive, roomMoves);
            foreach (string roomPlayer in roomPlayers)
            {
                playersAlive[roomPlayer] = true;
                roomMap[roomPlayer] = room;
            }
            _ = StartRoundTimer(room);
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
