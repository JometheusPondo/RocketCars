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
  public string                   FirstLevelName     = "SoccerLevel1";

  [Header("Dedicated Server")]
  public int                      DedicatedServerFPS = 45;

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
    Time.timeScale   = 1f;
    Cursor.lockState = CursorLockMode.None;

    var multiserverArg = GetValueForArg("-multiserver");
    var multiclientArg = GetValueForArg("-multiclient");
    var portoffsetArg  = GetValueForArg("-portoffset");

    if (Application.isBatchMode && Network.IsRunning == false)
    {
      if (multiserverArg != null)
      {
        int.TryParse(multiserverArg, out int num);
        int.TryParse(portoffsetArg, out int portoffset);
        StartMultiServer(num, portoffset);
      }
      else if (multiclientArg != null)
      {
        int.TryParse(multiclientArg, out int num);
        int.TryParse(portoffsetArg, out int portoffset);
        StartMultiClient(num, portoffset);
      }
      else
        StartServer(false);
    }
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

    var sandbox = isHost ? Network.StartAsHost(Transport, Port, SandboxPrefab) : Network.StartAsServer(Transport, Port, SandboxPrefab);

    if (isHost)
      sandbox.GetComponent<GlobalData>().LocalPlayerName = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");

    sandbox.GetComponent<GlobalData>().StartedThroughMainMenu = true;
    sandbox.SwitchScene(FirstLevelName);
  }

  public void StartMultiServer(int count, int portOffset)
  {
    Application.targetFrameRate = DedicatedServerFPS;
    var commands   = new List<SandboxLaunchData>(count);
    int basePort   = Port + portOffset;

    for (int i = 0; i < count; i++)
    {
      commands.Add(new SandboxLaunchData()
      {
        Port              = (basePort + i),
        StartMode         = NetickStartMode.Server,
        SandboxPrefab     = SandboxPrefab,
        TransportProvider = Transport
      });
    }

    var srvs = Netick.Unity.Network.Launch(commands);

    for (int i = 0; i < srvs.Count; i++)
    {
      srvs[i].GetComponent<GlobalData>().StartedThroughMainMenu = true;
      srvs[i].SwitchScene(FirstLevelName);
    }
  }

  public void StartMultiClient(int count, int portOffset)
  {
    Application.targetFrameRate = DedicatedServerFPS;
    var commands    = new List<SandboxLaunchData>(count);
    int basePort    = Port + portOffset;
    int numPlayers  = Resources.Load<NetickConfig>("netickConfig").MaxPlayers;

    for (int i = 0; i < count; i++)
    {
      commands.Add(new SandboxLaunchData()
      {
        StartMode         = NetickStartMode.Client,
        SandboxPrefab     = SandboxPrefab,
        TransportProvider = Transport
      });
    }

    var clis                      = Netick.Unity.Network.Launch(commands);
    RocketCarsRequestData req     = new RocketCarsRequestData { GameVersionHash = Netick.Unity.Network.GameVersion, };
    ArraySegment<byte> reqAsBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref req, Marshal.SizeOf<RocketCarsRequestData>())).ToArray();

    for (int i = 0; i < clis.Count; i++)
    {
      int targetPort              = basePort + (i / numPlayers);
      clis[i].GetComponent<GlobalData>().StartedThroughMainMenu = true;
      clis[i].Connect(targetPort, "108.61.170.135", reqAsBytes);
    }
  }

  public void StartClientAndConnect()
  {
    ConnectionErrorText.text                                         = "Connecting...";

    if (_clientSandbox == null)
      _clientSandbox                                                 = Network.StartAsClient(Transport, SandboxPrefab);

    _clientSandbox.GetComponent<GlobalData>().LocalPlayerName        = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
    _clientSandbox.GetComponent<GlobalData>().StartedThroughMainMenu = true;

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
    _clientSandbox.GetComponent<GlobalData>().LocalPlayerName = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
    _clientSandbox.GetComponent<GlobalData>().StartedThroughMainMenu = true;
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

  public string GetValueForArg(string argName)
  {
    string[] args = System.Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length; i++)
    {
      // Check if this arg matches the key and has a value following it
      if (args[i] == argName && args.Length > i + 1)
      {
        return args[i + 1];
      }
    }
    return null;
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