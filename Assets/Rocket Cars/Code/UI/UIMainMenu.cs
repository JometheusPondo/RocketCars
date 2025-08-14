using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Netick;
using Netick.Unity;
using Network = Netick.Unity.Network;

public class UIMainMenu : NetworkEventsListener
{
  public NetworkTransportProvider Transport;
  public GameObject               SandboxPrefab;
  public TextMeshProUGUI          ConnectionErrorText;
  public TMP_InputField           ServerIPAddressText;
  public TMP_InputField           PlayerNameText;

  public int                      Port;
  public int                      FirstLevelIndex    = 1;
  public int                      DedicatedServerFPS = 450;
  public Transform                Arena;

  private NetworkSandbox          _clientSandbox;

  private void Awake()
  {
    Cursor.lockState = CursorLockMode.None;

    // if Unity is started in batch mode, we just start Netick as a server. 
    if (Application.isBatchMode)
      StartServer();
  }

  private void Update()
  {
    Arena.Rotate(Vector2.up, 50f * Time.deltaTime);
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