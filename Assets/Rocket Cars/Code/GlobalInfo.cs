using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Netick;
using Netick.Unity;

public class GlobalInfo : NetworkBehaviour
{
  public string    PlayerName;
  public GameMode  GameMode ;
  public bool      StartedThroughMainMenu;
}
