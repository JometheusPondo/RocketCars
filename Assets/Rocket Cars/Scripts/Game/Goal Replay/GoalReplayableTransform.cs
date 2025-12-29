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

    if (IsServer)
      _nt.Teleport(transform.position);

    if (_nt != null && IsClient)
      _nt.InterpolationSource = InterpolationSource.RemoteSnapshot;
  }

  public override void OnReplayStopped()
  {
    base.OnReplayStopped();

    if (_rb != null)
      _rb.isKinematic         = _isKinematic;

    if (_nt != null && IsClient)
      _nt.InterpolationSource = InterpolationSource.Auto;
  }
}

