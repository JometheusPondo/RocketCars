using UnityEngine;
using System.Collections;
using Netick.Unity;
using Netick;

public enum Team
{
  Blue,
  Red
}

public class Player : NetworkBehaviour
{
  // Networked State ********************
  [Networked] public NetworkString16 Name    { get; set; }
  [Networked] public NetworkBool     IsReady { get; set; } 
  [Networked] public Team            Team    { get; set; }
  [Networked] public int             Goals   { get; set; }
  
  [HideInInspector]
  public Transform                   Spawn;
  [HideInInspector]
  public CarController               Car;
  
  void Awake()
  {
    Car = GetComponent<CarController>();
  }

  public void SetTeam(Team team)
  {
    Team = team;
  }

  public override void OnInputSourceLeft()
  {
    // reset state
    IsReady = false;
    Goals   = default;
    Car.ClearState(false);
  }

  // set the active car model based on the team.
  [OnChanged(nameof(Team))]
  private void OnTeamChanged(OnChangedData dat)
  {
    Car.RedCarModel. SetActive(Team == Team.Red);
    Car.BlueCarModel.SetActive(Team != Team.Red);
  }

  // we use IsReady as a way to know when a player has entered the game and is ready.
  [OnChanged(nameof(IsReady))]
  private void OnIsReadyChanged(OnChangedData dat)
  {
    if (IsReady) Sandbox.GetComponent<GlobalInfo>().GameMode.OnPlayerAdded(this); 
    else         Sandbox.GetComponent<GlobalInfo>().GameMode.OnPlayerRemoved(this);
  }

}

