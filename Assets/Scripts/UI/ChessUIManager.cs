using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ChessUIManager : MonoBehaviour
{
    [Header("Scene Dependencies")]
    [SerializeField] private NetworkManager networkManager;


    [Header("Buttons")]
    [SerializeField] private Button blackTeamButton;
    [SerializeField] private Button whiteTeamButton;

    [Header("Texts")]
    [SerializeField] private Text resultText;
    [SerializeField] private Text connectionStatusText;

    [Header("Screen Gameobjects")]
    [SerializeField] private GameObject gameoverScreen;
    [SerializeField] private GameObject connectScreen;
    [SerializeField] private GameObject teamSelectionScreen;
    [SerializeField] private GameObject gameModeSelectionScreen;

    [Header("Other UI")]
    [SerializeField] private Dropdown gameLevelSelection;



    private void Awake()
    {
        // Get options for rooms as Chess Levels enum
        gameLevelSelection.AddOptions(Enum.GetNames(typeof(ChessLevel)).ToList());
        OnGameLaunched();
    }

    private void OnGameLaunched()
    {
        DisableAllScreens();
        gameModeSelectionScreen.SetActive(true);
    }

    public void OnSinglePlayerModeSelected()
    {
        DisableAllScreens();
    }

    public void OnMultiplayerModelSelected()
    {
        connectionStatusText.gameObject.SetActive(true);
        DisableAllScreens();
        connectScreen.SetActive(true);
    }

    // Set the room type from enum and connect
    public void OnConnect()
    {
        networkManager.SetPlayerLevel((ChessLevel)gameLevelSelection.value);
        networkManager.Connect();
    }

    public void SetConnectionStatus(string status)
    {
        connectionStatusText.text = status;
    }

    private void DisableAllScreens()
    {
        gameoverScreen.SetActive(false);
        connectScreen.SetActive(false);
        teamSelectionScreen.SetActive(false);
        gameModeSelectionScreen.SetActive(false);
    }
}
