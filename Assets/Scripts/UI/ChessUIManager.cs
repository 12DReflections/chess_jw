using System;
using System.Collections;
using System.Collections.Generic;
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

    public void OnConnect()
    {
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
