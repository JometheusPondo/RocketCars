using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Netick;
using Netick.Unity;


/// <summary>
/// This is the Game Mode script, the parent script of all game modes. 
/// </summary>
[ExecutionOrder(-30)]
public abstract class GameMode : NetworkBehaviour
{
  // Networked State ********************
  [Networked] public NetworkBool    DisableInputForEveryone { get; set; }          // when true, the car will no longer accept user inputs, but can still be simulated.
  [Networked] public NetworkBool    DisableCarCamera        { get; set; } = false; // when true, the camera will no longer follow the car.
  [Networked] public NetworkBool    DisableCarSimulation    { get; set; } = false; // when true the cars will no longer be physically simulated.

  public List<Player>               CurrentPlayers          { get; private set; } = new(6);

  public GlobalInfo                 GlobalInfo              { get; private set; }
  public bool                       Paused                  = false;

  // Public Events
  public event UnityAction<Player>  OnPlayerAddedEvent;
  public event UnityAction<Player>  OnPlayerRemovedEvent;

  public override void NetworkAwake()
  {
    GlobalInfo                              = Sandbox.GetComponent<GlobalInfo>();
    GlobalInfo.GameMode                     = this;
    Sandbox.Events.OnDisconnectedFromServer += OnDisconnectedFromServer;     // subscribing to OnDisconnectedFromServer Netick event.
  }

  public void OnDisconnectedFromServer(NetworkSandbox sandbox, NetworkConnection server, TransportDisconnectReason transportDisconnectReason)
  {
    // if started in client mode (single-peer mode), when we are disconnected from the server we shut down Netick and switch to the menu scene.
    if (Sandbox.StartMode == NetickStartMode.Client)
    {
      // shutting down Netick.
      Netick.Unity.Network.Shutdown();
      // we use the regular Unity API for scene management instead of Netick scene management API, because we just shut down Netick and it's no longer in control of the game.
      SceneManager.LoadScene(0);
    }
  }

  // Collecting user input.
  public override void NetworkUpdate()
  {
    if (DisableInputForEveryone || Paused || !Sandbox.InputEnabled)
      return;

    var input       = Sandbox.GetInput<GameInput>();
    input.Movement += new Vector3(Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f), Mathf.Clamp(Input.GetAxis("Vertical"), -1f, 1f), Mathf.Clamp(Input.GetAxis("Roll"), -1f, 1f));
    input.Rocket   |= Input.GetButton("Rocket");
    input.Jump     |= Input.GetButtonDown("Jump");
    Sandbox.SetInput(input);
  }

  public virtual void OnPlayerAdded(Player player)
  {
    CurrentPlayers.Add(player);
    OnPlayerAddedEvent?.Invoke(player);
  }

  public virtual void OnPlayerRemoved(Player player)
  {
    OnPlayerRemovedEvent?.Invoke(player);
    CurrentPlayers.Remove(player);
  }
}