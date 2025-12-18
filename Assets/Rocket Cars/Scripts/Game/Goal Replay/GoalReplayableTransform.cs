using System.Collections.Generic;
using System;
using Unity;
using UnityEngine;
using Netick;
using Netick.Unity;

public unsafe class GoalReplayableTransform : GoalReplayable
{
  private Rigidbody        _rb;
  private NetworkTransform _nt;
  private bool             _isKinematic;
  public override void NetworkStart()
  {
    base.NetworkStart();
    _rb                   = GetComponent<Rigidbody>();
    _nt                   = GetComponent<NetworkTransform>();

    if (_rb != null)
      _isKinematic        = _rb.isKinematic;
  }

  public override void OnReplayStarted()
  {
    base.OnReplayStarted();

    if (_rb != null)
    {
      _isKinematic            = _rb.isKinematic;
      _rb.isKinematic         = true;
    }

    // when we are replaying the game in the client, we switch interpolation source to RemoteSnapshot to have smooth rendering of the cars and ball since they will only be controlled remotely by the server during replay.
    if (_nt != null && IsClient)
      _nt.InterpolationSource = InterpolationSource.RemoteSnapshot;
  }

  public override void OnReplayStopped()
  {
    base.OnReplayStopped();

    if (_rb != null)
      _rb.isKinematic         = _isKinematic;

    // we change it back to Auto when replay finishes.
    if (_nt != null && IsClient)
      _nt.InterpolationSource = InterpolationSource.Auto;
  }
}

