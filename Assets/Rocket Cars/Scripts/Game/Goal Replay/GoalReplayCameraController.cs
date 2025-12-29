using JetBrains.Annotations;
using Netick;
using Netick.Unity;
using System.Collections;
using System.Collections.Generic;
using Unity;
using UnityEngine;

public class GoalReplayCameraController : MonoBehaviour
{
  public Ball                    Ball;
  public Transform               RedTeamGoalbox;
  public Transform               BlueTeamGoalbox;

  [Header("Cinematic Settings")]
  public Vector3                 DroneOffset        = new Vector3(0, 5, -10); 
  public float                   MinCameraHeight    = 1.5f; 
  public float                   PositionSmoothTime = 0.3f;
  public float                   RotationSmoothTime = 0.1f;

  // private
  private Soccer                 _soccer;
  private GoalReplay             _goalReplay;
  private Camera                 _camera;
  private float                  _originalFOV;
  private bool                   _wasGoalReplay;
  private Vector3                _currentDroneVelocity;

  public void Awake()
  {
    _soccer                      = GetComponent<Soccer>();
    _goalReplay                  = GetComponent<GoalReplay>();
  }

  public void Render()
  {
    if (_camera == null)
    {
      _camera                    = _soccer.Sandbox.FindObjectOfType<Camera>();
      _originalFOV               = _camera.fieldOfView;
    }

    if ((_soccer.Sandbox.ContainsPlayer(_soccer.SpectatedPlayer) || !_soccer.GlobalInfo.IsReplay) && _soccer.GameState == Soccer.State.GoalReplay)
      UpdateCamera(!_wasGoalReplay);
    else
      _camera.fieldOfView        = _originalFOV;

    _wasGoalReplay               = _soccer.GameState == Soccer.State.GoalReplay;
  }

  public void UpdateCamera(bool reset)
  {
    Transform scorer             = _soccer.LastGoalScorer.GetBehaviour(_soccer.Sandbox).Car.NetworkRigidbody.RenderTransform;
    Transform ball               = Ball.NetworkRigidbody.RenderTransform;
    var lastGoalTargetPosition   = (_soccer.LastGoalTarget == Team.Blue) ? BlueTeamGoalbox.position : RedTeamGoalbox.position;
    var untilReplayFinish        = _soccer.Sandbox.TickToTime(_soccer.Sandbox.AuthoritativeTick - _soccer.TransitionTick);
    float focusWeight            = untilReplayFinish > (_goalReplay.MaxReplayTime - _soccer.DelayUntilReplay) ? 0.15f : 0.3f;
    Vector3 focusPoint           = Vector3.Lerp(scorer.position, ball.position, focusWeight);
    Vector3 directionToBall      = (ball.position - scorer.position).normalized;
    float distToBall             = Vector3.Distance(_camera.transform.position, ball.position);
    Vector3 sideViewOffset       = Vector3.Cross(directionToBall, Vector3.up) * 12f;
    Vector3 desiredPos;
    desiredPos                   = focusPoint - (directionToBall * 15f) + sideViewOffset + (Vector3.up * 5f);
    desiredPos.y                 = Mathf.Max(desiredPos.y, MinCameraHeight);
    _camera.transform.position   = Vector3.SmoothDamp(_camera.transform.position,desiredPos, ref _currentDroneVelocity,0.4f,Mathf.Infinity,Time.unscaledDeltaTime);

    if (_camera.transform.position.y < MinCameraHeight)
      _camera.transform.position = new Vector3(_camera.transform.position.x, MinCameraHeight, _camera.transform.position.z);

    float targetFOV              = Mathf.Clamp(70f - (distToBall * 0.5f), 30f, 90f);
    _camera.fieldOfView          = Mathf.Lerp(_camera.fieldOfView, targetFOV, Time.unscaledDeltaTime * 2f);
    Quaternion targetRot         = Quaternion.LookRotation(focusPoint - _camera.transform.position);
    _camera.transform.rotation   = Quaternion.Slerp(_camera.transform.rotation, targetRot, Time.unscaledDeltaTime * 5f);
  }
}
