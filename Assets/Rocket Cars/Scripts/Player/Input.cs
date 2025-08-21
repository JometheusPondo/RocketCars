using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;

[Networked]
public struct GameInput : INetworkInput
{
  [Networked] // adding [Networked] to a struct field and making it a property allows Netick to provide extra compression to it.
  public Vector3     Movement { get; set; }
  public NetworkBool Jump;
  public NetworkBool Rocket;
  public NetworkBool Drift;
}

