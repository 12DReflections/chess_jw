using System;
using System.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ChessUIManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NetworkManager networkManager;

    [Header("Buttons")]
    [SerializeField] private Button whiteTeamButtonButton;
    [SerializeField] private Button blackTeamButtonButton;

    [Header("Texts")]
    [SerializeField] private Text finishText;
    [SerializeField] private Text connectionStatus;

    [Header("Screen Gameobjects")]
    [SerializeField] private GameObject GameOverScreen;
    [SerializeField] private GameObject ConnectScreen;
    [SerializeField] private GameObject TeamSelectionScreen;
    [SerializeField] private GameObject GameModeSelectionScreen;

    [Header("Other UI")]
    [SerializeField] private Dropdown gameLevelSelection;

    private void Awake()
    {
        gameLevelSelection.AddOptions(Enum.GetNames(typeof(ChessLevel)).ToList());
        OnGameLaunched();
    }



    internal void OnGameLaunched()
    {
        GameOverScreen.SetActive(false);
        TeamSelectionScreen.SetActive(false);
        ConnectScreen.SetActive(false);
        GameModeSelectionScreen.SetActive(true);
    }

    public void OnSinglePlayerModeSelected()
    {
        GameOverScreen.SetActive(false);
        TeamSelectionScreen.SetActive(false);
        ConnectScreen.SetActive(false);
        GameModeSelectionScreen.SetActive(false);
    }

    public void OnMultiPlayerModeSelected()
    {
        connectionStatus.gameObject.SetActive(true);
        GameOverScreen.SetActive(false);
        TeamSelectionScreen.SetActive(false);
        ConnectScreen.SetActive(true);
        GameModeSelectionScreen.SetActive(false);
    }

    internal void OnGameFinished(string winner)
    {

        GameOverScreen.SetActive(true);
        TeamSelectionScreen.SetActive(false);
        ConnectScreen.SetActive(false);
        finishText.text = string.Format("{0} won", winner);
    }

    public void OnConnect()
    {
        networkManager.SetPlayerLevel((ChessLevel)gameLevelSelection.value);
        networkManager.Connect();
    }

    public void SetConnectionStatusText(string status)
    {
        connectionStatus.text = status;
    }

    internal void ShowTeamSelectionScreen()
    {
        GameOverScreen.SetActive(false);
        TeamSelectionScreen.SetActive(true);
        ConnectScreen.SetActive(false);
    }

    public void OnGameStarted()
    {
        GameOverScreen.SetActive(false);
        TeamSelectionScreen.SetActive(false);
        ConnectScreen.SetActive(false);
        connectionStatus.gameObject.SetActive(false);
        GameModeSelectionScreen.SetActive(false);
    }

    public void SelectTeam(int team)
    {
        networkManager.SetPlayerTeam(team);
    }

    internal void RestrictTeamChoice(TeamColor occpiedTeam)
    {
        Button buttonToDeactivate = occpiedTeam == TeamColor.White ? whiteTeamButtonButton : blackTeamButtonButton;
        buttonToDeactivate.interactable = false;
    }
}
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;
//using System.Linq;

//public class ChessUIManager : MonoBehaviour
//{
//    [Header("Dependencies")]
//    [SerializeField] private NetworkManager networkManager;

//    [Header("Buttons")]
//    [SerializeField] private Button whiteTeamButton;
//    [SerializeField] private Button blackTeamButton;

//    [Header("Texts")]
//    [SerializeField] private Text finishText;
//    [SerializeField] private Text connectionStatus;

//    [Header("Screen Gameobjects")]
//    [SerializeField] private GameObject GameOverScreen;
//    [SerializeField] private GameObject ConnectScreen;
//    [SerializeField] private GameObject TeamSelectionScreen;
//    [SerializeField] private GameObject GameModeSelectionScreen;

//    [Header("Other UI")]
//    [SerializeField] private Dropdown gameLevelSelection;

//    private void Awake()
//    {
//        gameLevelSelection.AddOptions(Enum.GetNames(typeof(ChessLevel)).ToList());
//        OnGameLaunched();
//    }



//    internal void OnGameLaunched()
//    {
//        GameOverScreen.SetActive(false);
//        TeamSelectionScreen.SetActive(false);
//        ConnectScreen.SetActive(false);
//        GameModeSelectionScreen.SetActive(true);
//    }

//    public void OnSinglePlayerModeSelected()
//    {
//        GameOverScreen.SetActive(false);
//        TeamSelectionScreen.SetActive(false);
//        ConnectScreen.SetActive(false);
//        GameModeSelectionScreen.SetActive(false);
//    }

//    public void OnMultiPlayerModeSelected()
//    {
//        connectionStatus.gameObject.SetActive(true);
//        GameOverScreen.SetActive(false);
//        TeamSelectionScreen.SetActive(false);
//        ConnectScreen.SetActive(true);
//        GameModeSelectionScreen.SetActive(false);
//    }

//    internal void OnGameFinished(string winner)
//    {

//        GameOverScreen.SetActive(true);
//        TeamSelectionScreen.SetActive(false);
//        ConnectScreen.SetActive(false);
//        finishText.text = string.Format("{0} won", winner);
//    }

//    public void OnConnect()
//    {
//        networkManager.SetPlayerLevel((ChessLevel)gameLevelSelection.value);
//        networkManager.Connect();
//    }

//    public void SetConnectionStatusText(string status)
//    {
//        connectionStatus.text = status;
//    }

//    internal void ShowTeamSelectionScreen()
//    {
//        GameOverScreen.SetActive(false);
//        TeamSelectionScreen.SetActive(true);
//        ConnectScreen.SetActive(false);
//    }

//    public void OnGameStarted()
//    {
//        GameOverScreen.SetActive(false);
//        TeamSelectionScreen.SetActive(false);
//        ConnectScreen.SetActive(false);
//        connectionStatus.gameObject.SetActive(false);
//        GameModeSelectionScreen.SetActive(false);
//    }

//    public void SelectTeam(int team)
//    {
//        networkManager.SelectTeam(team);
//    }

//    internal void RestrictTeamChoice(TeamColor occpiedTeam)
//    {
//        Button buttonToDeactivate = occpiedTeam == TeamColor.White ? whiteTeamButton : blackTeamButton;
//        buttonToDeactivate.interactable = false;
//    }
//}

////using System;
////using System.Collections;
////using System.Collections.Generic;
////using System.Linq;
////using UnityEngine;
////using UnityEngine.UI;

////public class ChessUIManager : MonoBehaviour
////{
////    [Header("Scene Dependencies")]
////    [SerializeField] private NetworkManager networkManager;


////    [Header("Buttons")]
////    [SerializeField] private Button blackTeamButton;
////    [SerializeField] private Button whiteTeamButton;

////    [Header("Texts")]
////    [SerializeField] private Text resultText;
////    [SerializeField] private Text connectionStatusText;

////    [Header("Screen Gameobjects")]
////    [SerializeField] private GameObject gameoverScreen;
////    [SerializeField] private GameObject connectScreen;
////    [SerializeField] private GameObject teamSelectionScreen;
////    [SerializeField] private GameObject gameModeSelectionScreen;

////    [Header("Other UI")]
////    [SerializeField] private Dropdown gameLevelSelection;



////    private void Awake()
////    {
////        // Get options for rooms as Chess Levels enum
////        gameLevelSelection.AddOptions(Enum.GetNames(typeof(ChessLevel)).ToList());
////        OnGameLaunched();
////    }

////    private void OnGameLaunched()
////    {
////        DisableAllScreens();
////        gameModeSelectionScreen.SetActive(true);
////    }

////    public void OnSinglePlayerModeSelected()
////    {
////        DisableAllScreens();
////    }

////    public void OnMultiplayerModelSelected()
////    {
////        connectionStatusText.gameObject.SetActive(true);
////        DisableAllScreens();
////        connectScreen.SetActive(true);
////    }

////    // Set the room type from enum and connect
////    public void OnConnect()
////    {
////        networkManager.SetPlayerLevel((ChessLevel)gameLevelSelection.value);
////        networkManager.Connect();
////    }

////    public void SetConnectionStatus(string status)
////    {
////        connectionStatusText.text = status;
////    }

////    private void DisableAllScreens()
////    {
////        gameoverScreen.SetActive(false);
////        connectScreen.SetActive(false);
////        teamSelectionScreen.SetActive(false);
////        gameModeSelectionScreen.SetActive(false);
////    }

////    public void ShowTeamSelectionScreen()
////    {
////        DisableAllScreens();
////        teamSelectionScreen.SetActive(true);
////    }

////    public void SelectTeam(int team)
////    {
////        networkManager.SelectTeam(team);
////    }

////    internal void RestrictTeamChoice(TeamColor occpiedTeam)
////    {
////        //Button buttonToDeactivate = occpiedTeam == TeamColor.White ? whiteTeamButton : blackTeamButton;
////        //buttonToDeactivate.interactable = false;
////        var buttonToDeactivate = occpiedTeam == TeamColor.White ? whiteTeamButton : blackTeamButton;
////        buttonToDeactivate.interactable = false;

////    }
////}
