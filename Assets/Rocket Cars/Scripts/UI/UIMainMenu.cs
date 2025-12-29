using Netick;
using Netick.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UI;
using Network = Netick.Unity.Network;

public class UIMainMenu : NetworkEventsListener
{
  public NetworkTransportProvider Transport;
  public GameObject               SandboxPrefab;
  public int                      Port;
  public int                      FirstLevelIndex    = 1;
  public int                      DedicatedServerFPS = 60;

  [Header("UI")]
  public TextMeshProUGUI          ConnectionErrorText;
  public TextMeshProUGUI          ReplayErrorText;
  public TMP_InputField           ServerIPAddressText;
  public TMP_InputField           PlayerNameText;

  public Button                   ButtonPlay;
  public Button                   ButtonHost;
  public Button                   ButtonJoin;
  public Button                   ButtonReplay;
  public Button                   ButtonConnect;
  public Button                   ButtonBackToMain;
  public Button                   ButtonBackToPlayFromConnect;
  public Button                   ButtonBackToPlayFromReplay;

  public float                    CanvasGroupDisabledAlpha = 0.5f;
  public CanvasGroup              UIMain;
  public CanvasGroup              UIPlay;
  public CanvasGroup              UIConnect;
  public CanvasGroup              UIReplay;

  private NetworkSandbox          _clientSandbox;

  private void Awake()
  {
    Cursor.lockState = CursorLockMode.None;

    // if Unity is started in batch mode, we just start Netick as a server. 
    if (Application.isBatchMode)
      StartServer(false);
  }

  private void Start()
  {
    InitUI();
  }

  public void StartHost()
  {
    StartServer(true);
  }

  public void StartServer(bool isHost)
  {
    if (isHost == false)
      Application.targetFrameRate = DedicatedServerFPS;

    // if Netick is already running, we shut it down first.
    if (Network.IsRunning)
      Network.ShutdownImmediately();

    var sandbox                                                      = isHost ? Network.StartAsHost(Transport, Port, SandboxPrefab) : Network.StartAsServer(Transport, Port, SandboxPrefab);

    if (isHost)
      sandbox.GetComponent<GlobalInfo>().LocalPlayerName             = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");

    sandbox.GetComponent<GlobalInfo>().StartedThroughMainMenu        = true;
    sandbox.SwitchScene(FirstLevelIndex);
  }

  public void StartClientAndConnect()
  {
    ConnectionErrorText.text                                         = "Connecting...";

    if (_clientSandbox == null)
      _clientSandbox                                                 = Network.StartAsClient(Transport, SandboxPrefab);

    _clientSandbox.GetComponent<GlobalInfo>().LocalPlayerName        = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
    _clientSandbox.GetComponent<GlobalInfo>().StartedThroughMainMenu = true;

    RocketCarsRequestData req                                        = new RocketCarsRequestData
    {
      GameVersionHash                                                = Netick.Unity.Network.GameVersion,
    };

    ArraySegment<byte> reqAsBytes                                    = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref req, Marshal.SizeOf<RocketCarsRequestData>())).ToArray();
    _clientSandbox.Connect(Port, ServerIPAddressText.text == "" ? "localhost" : ServerIPAddressText.text, reqAsBytes);
  }

  public async void StartReplayClient()
  {
    ReplayErrorText.text   = "Loading...";
    var latestReplay       = await FileReplayTransport.GetLatestReplayFilePathAsync();

    if (latestReplay == null)
    {
      ReplayErrorText.text = "No replays found.";
      return;
    }

    if (Network.IsRunning)
      Network.ShutdownImmediately();

    _clientSandbox = Network.StartAsReplayClient(SandboxPrefab);
    _clientSandbox.StartReplayPlayback(); // default path
    _clientSandbox.GetComponent<GlobalInfo>().LocalPlayerName = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
    _clientSandbox.GetComponent<GlobalInfo>().StartedThroughMainMenu = true;
    _clientSandbox.Connect(Port, ServerIPAddressText.text);
  }

  public override void OnConnectFailed(NetworkSandbox sandbox, ConnectionFailedReason reason)
  {
    Cursor.lockState             = CursorLockMode.None;

    if (reason != ConnectionFailedReason.Refused)
      ConnectionErrorText.text   = $"Connecting failed: {reason}";
    else
    {
      if (sandbox.TryGetConnectionRefusalData(out ArraySegment<byte> data))
        ConnectionErrorText.text = $"Connecting failed: {System.Text.Encoding.ASCII.GetString(data)}";
      else
        ConnectionErrorText.text = "Connecting failed: Refused by server.";
    }
  }

  public void Quit()
  {
    Application.Quit();
  }

  private void InitUI()
  {
    ButtonPlay.onClick.AddListener(OnClickButtonPlay);
    ButtonHost.onClick.AddListener(OnClickButtonHost);
    ButtonJoin.onClick.AddListener(OnClickButtonJoin);
    ButtonReplay.onClick.AddListener(OnClickButtonReplay);
    ButtonConnect.onClick.AddListener(OnClickButtonConnect);
    ButtonBackToMain.onClick.AddListener(OnClickButtonBackToMain);
    ButtonBackToPlayFromConnect.onClick.AddListener(OnClickButtonBackToPlayFromConnect);
    ButtonBackToPlayFromReplay.onClick.AddListener(OnClickButtonBackToPlayFromReplay);
    SetCanvasGroupVisibility(UIPlay, false);
    SetCanvasGroupVisibility(UIConnect, false);
    SetCanvasGroupVisibility(UIReplay, false);
    UIReplay.gameObject.SetActive(false);
  }

  private void OnClickButtonPlay()
  {
    SetCanvasGroupVisibility(UIMain, false, 0.5f);
    SetCanvasGroupVisibility(UIPlay, true);
  }

  private void OnClickButtonJoin()
  {
    SetCanvasGroupVisibility(UIPlay, false, 0.5f);
    SetCanvasGroupVisibility(UIConnect, true);
  }

  private void OnClickButtonReplay()
  {
    SetCanvasGroupVisibility(UIPlay, false, 0.5f);
    SetCanvasGroupVisibility(UIReplay, true);
    UIReplay.gameObject.SetActive(true);
    StartReplayClient();
  }

  private void OnClickButtonBackToMain()
  {
    SetCanvasGroupVisibility(UIPlay, false);
    SetCanvasGroupVisibility(UIMain, true);
  }

  private void OnClickButtonBackToPlayFromConnect()
  {
    SetCanvasGroupVisibility(UIConnect, false);
    SetCanvasGroupVisibility(UIPlay, true);
  }

  private void OnClickButtonBackToPlayFromReplay()
  {
    SetCanvasGroupVisibility(UIReplay, false);
    SetCanvasGroupVisibility(UIPlay, true);
    UIReplay.gameObject.SetActive(false);
  }

  private void OnClickButtonConnect()
  {
    StartClientAndConnect();
  }

  private void OnClickButtonHost()
  {
    StartHost();
  }

  private void SetCanvasGroupVisibility(CanvasGroup canvasGroup, bool interactable, float alpha = -1)
  {
    if (alpha == -1)
      canvasGroup.alpha      = interactable ? 1f : 0f;
    else
      canvasGroup.alpha      = alpha;

    canvasGroup.interactable = interactable;
    canvasGroup.gameObject.SetActive(interactable || alpha >= 0.5f);
  }
}