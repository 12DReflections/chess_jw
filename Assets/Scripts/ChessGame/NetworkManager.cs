using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [SerializeField] public ChessUIManager uiManager;

    private void Update()
    {
        uiManager.SetConnectionStatus(PhotonNetwork.NetworkClientState.ToString());

    }

    //If Connected Join a room, otherwise connect.
    public void Connect()
    {
        if(PhotonNetwork.IsConnected)
        {
            PhotonNetwork.JoinRandomRoom();
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
        Debug.LogError($"Connected to server. Looking for random room");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogError($"Joining room failed because of {message}. Creating a new one.");
        PhotonNetwork.CreateRoom(null);
    }

    public override void OnJoinedRoom()
    {
        Debug.LogError($"Player {PhotonNetwork.LocalPlayer.ActorNumber} joined the room.");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.LogError($"Player {newPlayer.ActorNumber} entered the room.");
    }
    #endregion

}
