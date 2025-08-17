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
  public TextMeshProUGUI          ConnectionErrorText;
  public TMP_InputField           ServerIPAddressText;
  public TMP_InputField           PlayerNameText;

    public Button ButtonPlay;
    public Button ButtonHost;
    public Button ButtonJoin;
    public Button ButtonConnect;
    public Button ButtonBackToMain;
    public Button ButtonBackToPlay;

    public float CanvasGroupDisabledAlpha = 0.5f;
    public CanvasGroup UIMain;
    public CanvasGroup UIPlay;
    public CanvasGroup UIConnect;

  public int                      Port;
  public int                      FirstLevelIndex    = 1;
  public int                      DedicatedServerFPS = 450;

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
        ButtonPlay.onClick.AddListener(OnClickButtonPlay);
        ButtonHost.onClick.AddListener(OnClickButtonHost);
        ButtonJoin.onClick.AddListener(OnClickButtonJoin);
        ButtonConnect.onClick.AddListener(OnClickButtonConnect);
        ButtonBackToMain.onClick.AddListener(OnClickButtonBackToMain);
        ButtonBackToPlay.onClick.AddListener(OnClickButtonBackToPlay);

        UIPlay.alpha = 0f;
        UIPlay.interactable = false;
        UIConnect.alpha = 0f;
        UIConnect.interactable = false;
    }

    private void OnClickButtonPlay()
    {
        UIMain.alpha = CanvasGroupDisabledAlpha;
        UIMain.interactable = false;

        UIPlay.alpha = 1f;
        UIPlay.interactable = true;
    }
    private void OnClickButtonHost()
    {
        StartHost();
    }
    private void OnClickButtonJoin()
    {
        UIPlay.alpha = CanvasGroupDisabledAlpha;
        UIPlay.interactable = false;

        UIConnect.alpha = 1f;
        UIConnect.interactable = true;
    }
    private void OnClickButtonConnect()
    {
        StartClientAndConnect();
    }
    private void OnClickButtonBackToMain()
    {
        UIPlay.alpha = 0f;
        UIPlay.interactable = false;

        UIMain.alpha = 1f;
        UIMain.interactable = true;
    }
    private void OnClickButtonBackToPlay()
    {
        UIConnect.alpha = 0f;
        UIConnect.interactable = false;

        UIPlay.alpha = 1f;
        UIPlay.interactable = true;
    }
    public void StartHost()
  {
    // if Netick is already running, we shut it down first.
    if (Network.IsRunning)
      Network.ShutdownImmediately();

    var sandbox                                                      = Network.StartAsHost(Transport, Port, SandboxPrefab);
    sandbox.GetComponent<GlobalInfo>().PlayerName                    = (PlayerNameText.text != "" ? PlayerNameText.text : "Unnamed");
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

  public override void OnConnectFailed(NetworkSandbox sandbox, ConnectionFailedReason reason)
  {
    Cursor.lockState                                                 = CursorLockMode.None;
    ConnectionErrorText.text                                         = $"Connecting failed: {reason}";
  }

  public void Quit()
  {
    Application.Quit();
  }

}