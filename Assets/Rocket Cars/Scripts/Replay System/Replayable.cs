using System.Collections.Generic;
using System;
using Unity;
using UnityEngine;
using Netick;
using Netick.Unity;

/// <summary>
/// Add this script to any object you want to make replayable.
/// </summary>
public unsafe class Replayable : NetworkBehaviour
{
  public NetworkBehaviour       ReplayableBehaviour;

  protected ReplaySystem       _replaySystem;
  private IntPtr[]             _historyBuffer;
  private ParticleSystem[]     _particleSystems;

  public override void NetworkStart()
  {
    _particleSystems      = GetComponentsInChildren<ParticleSystem>();
    _replaySystem         = Sandbox.FindObjectOfType<ReplaySystem>();

    if (IsServer)
    {
      _historyBuffer      = new IntPtr[_replaySystem.MaxRecordedTicks];
      // allocate the buffer elements
      for (int i = 0; i < _historyBuffer.Length; i++)
        _historyBuffer[i] = new IntPtr(Netick.MemoryAllocation.Malloc(ReplayableBehaviour.StateSize));
    }

    // register with replay system.
    _replaySystem.TrackObject(this);
  }

  public override void NetworkDestroy()
  {
    // unregister with replay system.
    _replaySystem.UntrackObject(this);
  }

  ~Replayable()
  {
    if (Sandbox != null && IsClient)
      return;

    // free buffer elements memory
    for (int i = 0; i < _historyBuffer.Length; i++)
      Netick.MemoryAllocation.Free(_historyBuffer[i].ToPointer());
  }

  public virtual void OnReplayStarted()
  {
    foreach (var ps in _particleSystems) ps.Clear();   // clear all particles.
  }

  public virtual void OnReplayStopped()
  {
    foreach (var ps in _particleSystems)  ps.Clear();  // clear all particles.
  }

  /// <summary>
  /// Store a snapshot of the current networked state of the object into the buffer.
  /// </summary>
  /// <param name="index"></param>
  public virtual void StoreToSnapshot  (int index) => ReplayableBehaviour.CopyStateTo((byte*) _historyBuffer[index]);
  /// <summary>
  /// Apply a snapshot from the buffer to the object.
  /// </summary>
  /// <param name="index"></param>
  public virtual void ApplyFromSnapshot(int index) => ReplayableBehaviour.SetStateFrom((byte*)_historyBuffer[index]);

}

