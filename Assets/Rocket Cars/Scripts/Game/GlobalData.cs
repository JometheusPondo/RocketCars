using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Netick;
using Netick.Unity;

[ExecutionOrder(-100)]
public class GlobalData : NetworkBehaviour
{
  public bool       StartedThroughMainMenu;
  public string     LocalPlayerName;
  public bool       HideUI;
  public GameMode   GameMode;
  public ChatSystem Chat;
  public Camera     Camera;

  public new bool   IsReplay    => Sandbox.IsReplay;
  public bool       CanUseInput => Sandbox.IsRunning || (Sandbox.InputEnabled && !Chat.IsChatting);

  private void Awake()
  {
    Chat = GetComponent<ChatSystem>();
  }

  public override void NetworkAwake()
  {
    GetComponent<UIChatFeed>()?.Init(this);
  }
}
