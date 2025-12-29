using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Netick;
using Netick.Unity;

public class GlobalInfo : NetworkBehaviour
{
  public bool      StartedThroughMainMenu;
  public string    LocalPlayerName;
  public bool      HideUI;
  public GameMode  GameMode;
  public Camera    Camera;

  public new bool  IsReplay => Sandbox.IsReplay;
}
