using JetBrains.Annotations;
using Netick;
using Netick.Unity;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-side goal replay system.
/// Note that this is temp and in the future Netick will include a built-in system for mid-game replays.
/// </summary>
[ExecuteAfter(typeof(PhysicsSimulationStep))]
public class GoalReplay : NetworkBehaviour
{
  // Networked State ********************
  [Networked] public NetworkBool    IsReplaying              { get; set; }

  public bool                       IsRecording              { get; set; } = false;
  public float                      MaxReplayTime            = 10f;
  public int                        MaxRecordedTicks         { get; private set; }
  public float                      TimeUntilReplayFinish    => Sandbox.TickToTime(_recordedTicks - (_replayCurrentTick - _replayStartTick));
  public GlobalInfo                 GlobalInfo               { get; private set; }
  
  private List<GoalReplayable>      _trackedObjects          = new (128);
  private int                       _recordedTicks;
  private Tick                      _replayCurrentTick;
  private Tick                      _replayStartTick;
 
  public override void NetworkAwake()
  {
    GlobalInfo                      = Sandbox.GetComponent<GlobalInfo>();
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
  public void TrackObject(GoalReplayable obj)
  {
    _trackedObjects.Add(obj);
  }

  /// <summary>
  /// Unregisters an object from the replay system.
  /// </summary>
  /// <param name="obj"></param>
  public void UntrackObject(GoalReplayable obj)
  {
    _trackedObjects.Remove(obj);
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

  [OnChanged(nameof(IsReplaying))][UsedImplicitly]
  private void OnIsReplayingChanged(OnChangedData dat)
  {
    // invoke replay start/stop callbacks. 
    if (IsReplaying == true)
    {
      foreach (var obj in _trackedObjects)
        obj.OnReplayStarted();
    }
    else
    {
      foreach (var obj in _trackedObjects)
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
    for (int i = 0; i < _trackedObjects.Count; i++)
      _trackedObjects[i].ApplyFromSnapshot(bufferIndex);

    _replayCurrentTick++;

    // check if replay finished.
    if (_replayCurrentTick - _replayStartTick >= _recordedTicks)
      StopReplaying();
  }

  private void Record()
  {
    // recording the current states of the replayable objects.
    int bufferIndex = Sandbox.Tick % MaxRecordedTicks;
    for (int i = 0; i < _trackedObjects.Count; i++)
      _trackedObjects[i].StoreToSnapshot(bufferIndex);
    _recordedTicks  = Mathf.Clamp(_recordedTicks + 1, 0, MaxRecordedTicks);
  }
}