using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;

/// <summary>
/// Server-side replay system.
/// </summary>
[ExecuteAfter(typeof(PhysicsSimulationStep))]
public class ReplaySystem : NetworkBehaviour
{
  // Networked State ********************
  [Networked] public NetworkBool    IsReplaying              { get; set; }

  public bool                       IsRecording              { get; set; }= false;
  public float                      MaxReplayTime            = 10f;
  public int                        MaxRecordedTicks         { get; private set; }
  public float                      TimeUntilReplayFinish    => Sandbox.TickToTime(_recordedTicks - (_replayCurrentTick - _replayStartTick));

  private List<Replayable>          _recordedObjects         = new (128);
  private int                       _recordedTicks;
  private Tick                      _replayCurrentTick;
  private Tick                      _replayStartTick;
 
  public override void NetworkAwake()
  {
    // since the tickrate is the number of ticks in a second, we multiply it by the replay time to find the number of ticks to record.
    MaxRecordedTicks                = (int)(MaxReplayTime * Sandbox.Config.TickRate);
  }

  public override void NetworkStart()
  {
    StartRecording();
  }

  /// <summary>
  /// Registers an object to be tracked by the replay system for record/replay.
  /// </summary>
  /// <param name="obj"></param>
  public void TrackObject(Replayable obj)
  {
    _recordedObjects.Add(obj);
  }

  /// <summary>
  /// Unregisters an object from the replay system.
  /// </summary>
  /// <param name="obj"></param>
  public void UntrackObject(Replayable obj)
  {
    _recordedObjects.Remove(obj);
  }

  public void StartRecording()
  {
    StopReplaying();
    _recordedTicks           = 0;
    IsRecording              = true;
  }

  public void StopRecording()
  {
    IsRecording              = false;
  }

  public void StopReplaying()
  {
    IsReplaying              = false;
  }

  public void StartReplaying()
  {
    IsRecording              = false;
    IsReplaying              = true;
    _replayStartTick         = Sandbox.Tick - _recordedTicks;
    _replayCurrentTick       = Sandbox.Tick - _recordedTicks;
  }

  [OnChanged(nameof(IsReplaying))]
  private void OnIsReplayingChanged(OnChangedData dat)
  {
    // invoke replay start/stop callbacks. 
    if (IsReplaying == true)
    {
      foreach (var obj in _recordedObjects)
        obj.OnReplayStarted();
    }
    else
    {
      foreach (var obj in _recordedObjects)
        obj.OnReplayStopped();
    }
  }

  public override void NetworkFixedUpdate()
  {
    if (IsClient)
      return;
    if      (IsRecording)
      Record();
    else if (IsReplaying)
      Replay();
  }

  private void Replay()
  {
    // applying the recorded snapshots on the replayable objects.
    int bufferIndex = _replayCurrentTick % MaxRecordedTicks;
    for (int i = 0; i < _recordedObjects.Count; i++)
      _recordedObjects[i].ApplyFromSnapshot(bufferIndex);

    _replayCurrentTick++;

    // check if replay finished.
    if (_replayCurrentTick - _replayStartTick >= _recordedTicks)
      StopReplaying();
  }

  private void Record()
  {
    // recording the current states of the replayable objects.
    int bufferIndex = Sandbox.Tick % MaxRecordedTicks;
    for (int i = 0; i < _recordedObjects.Count; i++)
      _recordedObjects[i].StoreToSnapshot(bufferIndex);
    _recordedTicks  = Mathf.Clamp(_recordedTicks + 1, 0, MaxRecordedTicks);
  }
}