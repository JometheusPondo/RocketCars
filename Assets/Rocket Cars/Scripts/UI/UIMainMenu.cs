using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Netick;
using Netick.Unity;
using Network = Netick.Unity.Network;
using UnityEngine.UI;

public class UIMainMenu : NetworkEventsListener
{
  public NetworkTransportProvider Transport;
  public GameObject               SandboxPrefab;
  public int                      Port;
  public int                      FirstLevelIndex    = 1;
  public int                      DedicatedServerFPS = 450;

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
      StartServer();
  }

  private void Start()
  {
    InitUI();
  }

  public void StartHost()
  {
    // if Netick is already running, we shut it down first.
    if (Network.IsRunning)
      Network.ShutdownImmediately();

    var sandbox = Network.StartAsHost(Transport, Port, SandboxPrefab);
    sandbox.GetComponent<GlobalInfo>().PlayerName = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
    sandbox.SwitchScene(FirstLevelIndex);
  }

  public void StartServer()
  {
    Application.targetFrameRate = DedicatedServerFPS;
    // if Netick is already running, we shut it down first.
    if (Network.IsRunning)
      Network.ShutdownImmediately();

    var sandbox                                                      = Network.StartAsServer(Transport, Port, SandboxPrefab);
    sandbox.SwitchScene(FirstLevelIndex);
  }

  public void StartClientAndConnect()
  {
    ConnectionErrorText.text                                         = "Connecting...";

    if (_clientSandbox == null)
      _clientSandbox                                                 = Network.StartAsClient(Transport, Port, SandboxPrefab);

    _clientSandbox.GetComponent<GlobalInfo>().PlayerName             = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
    _clientSandbox.GetComponent<GlobalInfo>().StartedThroughMainMenu = true;
    _clientSandbox.Connect(Port, ServerIPAddressText.text);
  }

  public async void StartReplayClient()
  {
    ReplayErrorText.text = "Loading...";

    var latestReplay = await FileReplayTransport.GetLatestReplayFilePathAsync();

    if (latestReplay == null)
    {
      ReplayErrorText.text = "No replays found.";
      return;
    }

    if (Network.IsRunning)
      Network.ShutdownImmediately();

    _clientSandbox = Network.StartAsReplayClient(SandboxPrefab);
    _clientSandbox.StartReplayPlayback(); // default path
    _clientSandbox.GetComponent<GlobalInfo>().PlayerName = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
    _clientSandbox.GetComponent<GlobalInfo>().StartedThroughMainMenu = true;
    _clientSandbox.Connect(Port, ServerIPAddressText.text);
  }

  public override void OnConnectFailed(NetworkSandbox sandbox, ConnectionFailedReason reason)
  {
    Cursor.lockState         = CursorLockMode.None;
    ConnectionErrorText.text = $"Connecting failed: {reason}";
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
  }
}