using JetBrains.Annotations;
using Netick;
using Netick.Unity;
using System.Collections;
using UnityEngine;

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
  public GameObject                  RedCarPrefab;
  public GameObject                  BlueCarPrefab;
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
  [OnChanged(nameof(Team))][UsedImplicitly]
  private void OnTeamChanged(OnChangedData dat)
  {
    var carPrefab = Team == Team.Red ? RedCarPrefab : BlueCarPrefab;
    Car.CarBody.GetComponentInChildren<Renderer>().materials = carPrefab.GetComponent<CarController>().CarBody.GetComponentInChildren<Renderer>().sharedMaterials;
  }
}

