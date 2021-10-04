using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;



public enum ChessLevel
{
    Beginner, Normal, Pro
}

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private const string LEVEL = "level";
    private const int MAX_PLAYERS = 2;


    [SerializeField] public ChessUIManager uiManager;

    private ChessLevel playerLevel;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    private void Update()
    {
        uiManager.SetConnectionStatus(PhotonNetwork.NetworkClientState.ToString());

    }

    //If Connected Join a room, otherwise connect based on room hash.
    public void Connect()
    {
        if(PhotonNetwork.IsConnected)
        {
            Debug.LogError($"Connected to server Connect(). Looking for random room with level {playerLevel}.");
            // Pass the room of the player enum variable, and max number of room attendants
            PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable() { { LEVEL, playerLevel }   }, MAX_PLAYERS);
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    #region Photon Callbacks
    // Connect to a random room
    public override void OnConnectedToMaster()
    {
        Debug.LogError($"Connected to server OnConnectedToMaster(). Looking for random room with level {playerLevel}.");
        // Pass the room of the player enum variable, and max number of room attendants
        PhotonNetwork.JoinRandomRoom(new ExitGames.Client.Photon.Hashtable() { { LEVEL, playerLevel } }, MAX_PLAYERS);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // Create a room with custom properties if could not join room. 
        Debug.LogError($"Joining room failed because of {message}. Creating a new one with player level category {playerLevel}.");
        PhotonNetwork.CreateRoom(null, new RoomOptions
        {
            CustomRoomPropertiesForLobby = new string[] {LEVEL},
            MaxPlayers = MAX_PLAYERS,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable() { {  LEVEL, playerLevel} },
        });
    }


    public override void OnJoinedRoom()
    {
        Debug.LogError($"Player {PhotonNetwork.LocalPlayer.ActorNumber} joined the room with level {(ChessLevel)PhotonNetwork.CurrentRoom.CustomProperties[LEVEL]}.");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.LogError($"Player {newPlayer.ActorNumber} entered the room.");
    }

    public void SetPlayerLevel(ChessLevel level)
    {
        playerLevel = level;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { LEVEL, level } });
    }
    #endregion

}
