using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Netick;
using Netick.Unity;
using Network = Netick.Unity.Network;


/// <summary>
/// This is the Game Mode script, the parent script of all game modes. 
/// </summary>
[ExecutionOrder(-30)]
public abstract class GameMode : NetworkBehaviour
{
  // Networked State ********************
  [Networked (size: 32)]
  public NetworkUnorderedList<NetworkBehaviourRef<Player>>  ActivePlayers           = new(32);
  [Networked] public NetworkBool                            DisableInputForEveryone { get; set; }  // when true, the car will no longer accept user inputs, but can still be simulated.
  [Networked] public NetworkBool                            DisableCarCamera        { get; set; }  // when true, the camera will no longer follow the car.
  [Networked] public NetworkBool                            DisableCarSimulation    { get; set; }  // when true the cars will no longer be physically simulated.

  public GlobalInfo                                         GlobalInfo              { get; private set; }
  [HideInInspector]
  public bool                                               Paused                  = false;
  [HideInInspector]
  public NetworkPlayerId                                    ReplaySelectedPlayer    = NetworkPlayerId.Invalid;

  public override void NetworkAwake()
  {
    GlobalInfo                               = Sandbox.GetComponent<GlobalInfo>();
    GlobalInfo.GameMode                      = this;
    GlobalInfo.Camera                        = Sandbox.FindObjectOfType<Camera>();
    Sandbox.Events.OnDisconnectedFromServer += OnDisconnectedFromServer;     // subscribing to OnDisconnectedFromServer Netick event.
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
    ReactToReplayControls();

    // collecting user input.
    if (DisableInputForEveryone || Paused || !Sandbox.InputEnabled)
      return;

    var input       = Sandbox.GetInput<GameInput>();
    input.Movement += new Vector3(Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f), Mathf.Clamp(Input.GetAxis("Vertical"), -1f, 1f), Mathf.Clamp(Input.GetAxis("Roll"), -1f, 1f));
    input.Rocket   |= Input.GetButton("Rocket");
    input.Jump     |= Input.GetButtonDown("Jump");
    Sandbox.SetInput(input);
  }

  public virtual void ReactToReplayControls()
  {
    if (!Sandbox.IsReplay)
      return;

    if (!Sandbox.ContainsPlayer(ReplaySelectedPlayer))
      ReplaySelectedPlayer = NetworkPlayerId.Invalid;

    var players = Sandbox.Players;

    if (Input.GetKeyDown(KeyCode.F))
    {
      if (ReplaySelectedPlayer.IsValid)
        ReplaySelectedPlayer = NetworkPlayerId.Invalid;
      else if (players.Count > 0)
      {
        if (Sandbox.TryGetPlayerObject<Player>(players[0], out var player0) && player0.IsReady) // handle case where server/host car is not spawned
          ReplaySelectedPlayer = players[0];
        if (players.Count > 1)
        {
          if (Sandbox.TryGetPlayerObject<Player>(players[1], out var player1) && player1.IsReady)
            ReplaySelectedPlayer = players[1];
        }
      }
    }
    else if (Input.inputString.Length > 0 && char.IsDigit(Input.inputString[0]))
    {
      int numValue = int.Parse(Input.inputString[0].ToString());
      if (numValue < players.Count)
        ReplaySelectedPlayer = players[numValue];
    }
  }
}