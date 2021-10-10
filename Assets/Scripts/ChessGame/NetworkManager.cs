using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChessLevel
{
    Beginner, Normal, Pro
}

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private const string LEVEL = "level";
    private const string TEAM = "team";
    private const byte MAX_PLAYERS = 2;
    [SerializeField] private ChessUIManager uiManager;
    [SerializeField] private GameInitializer gameInitializer;
    private MultiplayerChessGameController chessGameController;

    private ChessLevel playerLevel;

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void SetDependencies(MultiplayerChessGameController chessGameController)
    {
        this.chessGameController = chessGameController;
    }

    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable() { { LEVEL, playerLevel } }, MAX_PLAYERS);
            //PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    private void Update()
    {
        uiManager.SetConnectionStatusText(PhotonNetwork.NetworkClientState.ToString());
    }

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {

        Debug.LogError($"Connected to server. Looking for random room with level {playerLevel}");
        PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable() { { LEVEL, playerLevel } }, MAX_PLAYERS);
        //PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogError($"Joining random room failed becuse of {message}. Creating new one with player level {playerLevel}");
        PhotonNetwork.CreateRoom(null, new RoomOptions
        {
            CustomRoomPropertiesForLobby = new string[] { LEVEL },
            MaxPlayers = MAX_PLAYERS,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable() { { LEVEL, playerLevel } }
        });
        //PhotonNetwork.CreateRoom(null);
    }

    public override void OnJoinedRoom()
    {
        Debug.LogError($"Player {PhotonNetwork.LocalPlayer.ActorNumber} joined a room with level: {(ChessLevel)PhotonNetwork.CurrentRoom.CustomProperties[LEVEL]}");
        gameInitializer.CreateMultiplayerBoard();
        PrepareTeamSelectionOptions();
        uiManager.ShowTeamSelectionScreen();

    }


    private void PrepareTeamSelectionOptions()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount > 1)
        {
            var player = PhotonNetwork.CurrentRoom.GetPlayer(1);
            if (player.CustomProperties.ContainsKey(TEAM))
            {
                var occupiedTeam = player.CustomProperties[TEAM];
                uiManager.RestrictTeamChoice((TeamColor)occupiedTeam);
            }
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.LogError($"Player {newPlayer.ActorNumber} entered a room");
    }
    #endregion

    public void SetPlayerLevel(ChessLevel level)
    {
        playerLevel = level;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable() { { LEVEL, level } });
    }

    //public void SetPlayerTeam(int teamInt)
    //{
    //    if (PhotonNetwork.CurrentRoom.PlayerCount > 1)
    //    {
    //        var player = PhotonNetwork.CurrentRoom.GetPlayer(1);
    //        if (player.CustomProperties.ContainsKey(TEAM))
    //        {
    //            var occupiedTeam = player.CustomProperties[TEAM];
    //            teamInt = (int)occupiedTeam == 0 ? 1 : 0;
    //        }
    //    }
    //    PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable() { { TEAM, teamInt } });
    //    gameInitializer.InitializeMultiplayerController();
    //    chessGameController.SetupCamera((TeamColor)teamInt);
    //    chessGameController.SetLocalPlayer((TeamColor)teamInt);
    //    chessGameController.StartNewGame();
    //}
    public void SetPlayerTeam(int teamInt)
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount > 1)
        {
            var player = PhotonNetwork.CurrentRoom.GetPlayer(1);
            if (player.CustomProperties.ContainsKey(TEAM))
            {
                var occupiedTeam = player.CustomProperties[TEAM];
                teamInt = (int)occupiedTeam == 0 ? 1 : 0;
            }
        }
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable() { { TEAM, teamInt } });
        gameInitializer.InitializeMultiplayerController();
        chessGameController.SetupCamera((TeamColor)teamInt);
        chessGameController.SetLocalPlayer((TeamColor)teamInt);
        chessGameController.StartNewGame();
    }




    internal bool IsRoomFull()
    {
        return PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers;
    }

}
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using Photon.Pun;
//using Photon.Realtime;



//public enum ChessLevel
//{
//    Beginner, Normal, Pro
//}

//public class NetworkManager : MonoBehaviourPunCallbacks
//{
//    private const string LEVEL = "level";
//    private const string TEAM = "team";
//    private const int MAX_PLAYERS = 2;
//    [SerializeField] public ChessUIManager uiManager;
//    //[SerializeField] private GameInitializer gameInitializer;
//    private MultiplayerChessGameController chessGameController;

//    private ChessLevel playerLevel;

//    private void Awake()
//    {
//        PhotonNetwork.AutomaticallySyncScene = true;
//    }

//    private void Update()
//    {
//        uiManager.SetConnectionStatus(PhotonNetwork.NetworkClientState.ToString());

//    }

//    // Join a room from a hash of the enum/dropdown for level selection.
//    public void Connect()
//    {
//        if(PhotonNetwork.IsConnected)
//        {
//            Debug.LogError($"Connected to server Connect(). Looking for random room with level {playerLevel}.");
//            // Pass the room of the player enum variable, and max number of room attendants
//            PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable() { { LEVEL, playerLevel }   }, MAX_PLAYERS);
//        }
//        else
//        {
//            PhotonNetwork.ConnectUsingSettings();
//        }
//    }

//    #region Photon Callbacks
//    // Connect to a random room from level selection
//    public override void OnConnectedToMaster()
//    {
//        Debug.LogError($"Connected to server OnConnectedToMaster(). Looking for random room with level {playerLevel}.");
//        // Pass the room of the player enum variable, and max number of room attendants
//        PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable() { { LEVEL, playerLevel } }, MAX_PLAYERS);
//    }

//    public override void OnJoinRandomFailed(short returnCode, string message)
//    {
//        // Create a room with custom properties if could not join room. 
//        Debug.LogError($"Joining room failed because of {message}. Creating a new one with player level category {playerLevel}.");
//        PhotonNetwork.CreateRoom(null, new RoomOptions
//        {
//            CustomRoomPropertiesForLobby = new string[] {LEVEL},
//            MaxPlayers = MAX_PLAYERS,
//            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable() { {  LEVEL, playerLevel} },
//        });
//    }


//    public override void OnJoinedRoom()
//    {
//        Debug.LogError($"Player {PhotonNetwork.LocalPlayer.ActorNumber} joined the room with level {(ChessLevel)PhotonNetwork.CurrentRoom.CustomProperties[LEVEL]}.");
//        PrepareTeamSelectionOptions();
//        uiManager.ShowTeamSelectionScreen();
//    }

//    private void PrepareTeamSelectionOptions()
//    {
//        if (PhotonNetwork.CurrentRoom.PlayerCount > 1)
//        {
//            var player = PhotonNetwork.CurrentRoom.GetPlayer(1);
//            if (player.CustomProperties.ContainsKey(TEAM))
//            {
//                var occupiedTeam = player.CustomProperties[TEAM];
//                uiManager.RestrictTeamChoice((TeamColor)occupiedTeam);
//            }
//        }
//    }

//    public override void OnPlayerEnteredRoom(Player newPlayer)
//    {
//        Debug.LogError($"Player {newPlayer.ActorNumber} entered the room.");
//    }

//    public void SetPlayerLevel(ChessLevel level)
//    {
//        playerLevel = level;
//        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { LEVEL, level } });
//    }

//    internal void SelectTeam(int team)
//    {
//        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { TEAM, team } });
//    }
//    #endregion

//}
