using Netick;
using Netick.Samples;
using Netick.Unity;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using Network = Netick.Unity.Network;

public struct RocketCarsRequestData
{
  public int GameVersionHash;
}

/// <summary>
/// This is the Game Mode script, the parent script of all game modes. 
/// </summary>
[ExecutionOrder(-30)]
public abstract class GameMode : NetworkBehaviour
{
  // Networked State ********************
  [Networked] public NetworkBool             DisableInputForAll      { get; set; }  // when true, the car will no longer accept user inputs, but can still be simulated.
  [Networked] public NetworkBool             DisableCarCamera        { get; set; }  // when true, the camera will no longer follow the car.
  [Networked] public NetworkBool             DisableCarSimulation    { get; set; }  // when true the cars will no longer be physically simulated.
  [HideInInspector] public GlobalInfo        GlobalInfo              { get; private set; }

  [HideInInspector] public bool              Paused                  = false;

  public GameObject                          UIParent;

  public NetworkPlayerId                     SpectatedPlayer         = NetworkPlayerId.Invalid;

  // const
  public static readonly byte[]              BadRequestError = System.Text.Encoding.ASCII.GetBytes("You sent a bad request!");
  public static readonly byte[]              BadVersionError = System.Text.Encoding.ASCII.GetBytes("Your game is running a differnt build version than the server!");

  public override void NetworkAwake()
  {
    GlobalInfo                               = Sandbox.GetComponent<GlobalInfo>();
    GlobalInfo.Camera                        = Sandbox.FindObjectOfType<Camera>();
    GlobalInfo.GameMode                      = this;
    Sandbox.Events.OnDisconnectedFromServer += OnDisconnectedFromServer;
    Sandbox.Events.OnConnectRequest         += OnConnectRequest;
  }

  public void OnConnectRequest(NetworkSandbox sandbox, NetworkConnectionRequest request)
  {
    if (!GlobalInfo.StartedThroughMainMenu) // accept all connections if not started through menu scene, this means the game was started in a map directly for testing reasons.
      return;

    if (request.DataLength < Marshal.SizeOf<RocketCarsRequestData>())
      request.Refuse(BadRequestError);

    RocketCarsRequestData dataStruct = MemoryMarshal.Read<RocketCarsRequestData>(request.Data);

    if (dataStruct.GameVersionHash != Netick.Unity.Network.GameVersion)
      request.Refuse(BadVersionError);
  }

  public void OnDisconnectedFromServer(NetworkSandbox sandbox, NetworkConnection server, TransportDisconnectReason transportDisconnectReason)
  {
    // if started in client mode (single-peer mode), when we are disconnected from the server we shut down Netick and switch to the menu scene.
    if (Network.Instance != null && Network.StartMode == StartMode.Client)
    {
      // shutting down Netick.
      Netick.Unity.Network.Shutdown();
      // we use the regular Unity API for scene management instead of Netick scene management API, because we just shut down Netick and it's no longer in control of the game.
      SceneManager.LoadScene(0);
    }
  }

  public override void NetworkUpdate()
  {
    if (GlobalInfo.IsReplay && Input.GetKeyDown(KeyCode.BackQuote))
    {
      Cursor.lockState  = GlobalInfo.HideUI  ? CursorLockMode.None : CursorLockMode.Locked;
      GlobalInfo.HideUI = !GlobalInfo.HideUI;
      UpdateUIState();
    }

    if (GlobalInfo.IsReplay)
      ReactToSpectateControls();

    if (Paused || !Sandbox.InputEnabled)
      return;
    // collecting network user input.
    var input       = Sandbox.GetInput<GameInput>();
    input.Movement += new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Roll"));
    input.Rocket   |= Input.GetButton("Rocket");
    input.Jump     |= Input.GetButtonDown("Jump");
    input.Drift    |= Input.GetButton("Drift");
    Sandbox.SetInput(input);
  }

  public virtual void ReactToSpectateControls()
  {
    if (!Sandbox.ContainsPlayer(SpectatedPlayer))
      SpectatedPlayer = NetworkPlayerId.Invalid;

    var players = Sandbox.Players;

    if (Input.GetKeyDown(KeyCode.F))
    {
      if (SpectatedPlayer.IsValid)
        SpectatedPlayer = NetworkPlayerId.Invalid;
      else if (players.Count > 0)
      {
        if (Sandbox.TryGetPlayerObject<Player>(players[0], out var player0) && player0.IsReady) // handle case where server/host car is not spawned
          SpectatedPlayer = players[0];
        if (players.Count > 1)
        {
          if (Sandbox.TryGetPlayerObject<Player>(players[1], out var player1) && player1.IsReady)
            SpectatedPlayer = players[1];
        }
      }
    }
    else if (Input.inputString.Length > 0 && char.IsDigit(Input.inputString[0]))
    {
      int numValue = (int.Parse(Input.inputString[0].ToString()))-1;
      if (numValue < players.Count && numValue >= 0)
        SpectatedPlayer = players[numValue];
    }
  }

  public void UpdateUIState()
  {
    UIParent.SetActive(!GlobalInfo.HideUI);
    GetComponent<NetworkInfo>().enabled    = !GlobalInfo.HideUI;
    GetComponent<GameStarter>().enabled    = !GlobalInfo.HideUI;
    GetComponent<ReplayTimeline>().enabled = !GlobalInfo.HideUI;
  }
}