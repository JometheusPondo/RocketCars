using Netick;
using Netick.Unity;
using System;
using UnityEngine;

public class CarCameraController : NetworkBehaviour
{
  public Transform      CarRenderTransform;

  [Header("General")]
  public bool           LookAtBall         = true;
  public Vector3        CameraOffset;
  public float          LerpFactor = 20f;
  public AudioClip      ChangeCameraAudioClip;

  [Header("Shake")]
  public float          MinShakeSpeed  = 5f;
  public float          MaxShakeSpeed  = 60f;
  public float          ShakeFrequency = 35f;
  public float          MaxShakeAmount = 0.15f;

  [Header("Dynamic FOV")]
  public float          MaxSpeedFOV    = 105f;
  public float          FOVWeight      = 0.5f;

  // private
  private Camera        _camera;
  private CarController _carController;
  private Player        _player;
  private GameMode      _gm;
  private Ball          _ball;
  private Transform     _ballRenderTransform;
  private bool          _prevDisableCarCamera;

  private Vector3       _curDir;
  private Vector3       _curPos, _prevPos;
  private Quaternion    _curRot, _prevRot;
  private Vector3       _curShakeOffset , _prevShakeOffset;
  private float         _curFOV, _prevFOV;
  private float         _baseFov;

  public override void NetworkStart()
  {
    _player              = GetComponent<Player>();
    _carController       = GetComponent<CarController>();
    _gm                  = Sandbox.FindObjectOfType<GameMode>();
    _camera              = Sandbox.FindObjectOfType<Camera>();
    _ball                = Sandbox.FindObjectOfType<Ball>();
    _ballRenderTransform = _ball.transform.GetChild(0);

    _curFOV              = _camera.fieldOfView;
    _prevFOV             = _camera.fieldOfView;
    _baseFov             = _camera.fieldOfView;
  }
  
  private void ResetCamera()
  {
    FollowCarAndLookAtBall(Sandbox.FixedDeltaTime, true);
    _prevPos         = _curPos;
    _prevRot         = _curRot;
    _curShakeOffset  = Vector3.zero;
    _prevShakeOffset = Vector3.zero;
  }

  public override void NetworkRender()
  {
    var plrId = _gm.GlobalInfo.IsReplay ? _gm.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId;

    if (InputSourcePlayerId != plrId || !_player.IsReady)
      return;

    if (_gm.DisableCarCamera != _prevDisableCarCamera)
      ResetCamera();

    _prevDisableCarCamera = _gm.DisableCarCamera;

    if (_gm.DisableCarCamera)
      return;

    bool toggleInput = _gm.GlobalInfo.IsReplay ? Input.GetKeyDown(KeyCode.LeftShift) : Sandbox.InputEnabled && Input.GetButtonDown("Camera Toggle");

    if (toggleInput)
    {
      LookAtBall = !LookAtBall;
      if (Sandbox.IsVisible)
        AudioSource.PlayClipAtPoint(ChangeCameraAudioClip, transform.position);
    }

    // interpolate everything
    var alpha                  = Sandbox.LocalInterpolation.Alpha;
    var basePos                = Vector3.   Lerp(_prevPos,  _curPos, alpha);
    var baseRot                = Quaternion.Slerp(_prevRot, _curRot, alpha);
    var shakePos               = Vector3.   Lerp(_prevShakeOffset, _curShakeOffset, alpha);
    _camera.transform.position = basePos + (baseRot * shakePos);
    _camera.transform.rotation = baseRot;
    _camera.fieldOfView        = Mathf.Lerp(_prevFOV, _curFOV, alpha);
  }

  public override void NetworkFixedUpdate()
  {
    if (IsResimulating)
      return;

    var plrId        = _gm.GlobalInfo.IsReplay ? _gm.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId;

    if (InputSourcePlayerId != plrId || !_player.IsReady)
      return;

    // store previous states for interpolation
    _prevPos         = _curPos;
    _prevRot         = _curRot;
    _prevFOV         = _curFOV;
    _prevShakeOffset = _curShakeOffset;

    float dt         = Sandbox.FixedDeltaTime;
    CalculateShake(dt);
    CalculateFOV(dt);
    CalculateCameraPositionAndRotation(dt);
  }

  private void CalculateCameraPositionAndRotation(float dt)
  {
    if (LookAtBall)
      FollowCarAndLookAtBall(dt);
    else
      FollowCarAndLookAtCar(dt);
  }

  private void FollowCarAndLookAtCar(float deltaTime)
  {
    var forward       = _carController.Rigidbody.velocity;

    if (_carController.IsGrounded)
      forward         = Input.GetMouseButton(2) ? -transform.forward : transform.forward;

    forward.y         = 0;
    forward.Normalize();

    if (forward == Vector3.zero) 
      forward         = _curDir;

    var dot           = Mathf.Abs(Vector3.Dot(_curDir.normalized, forward));
    var j             = Math.Max(0.2f, Mathf.Pow(dot, 10)) * LerpFactor * deltaTime;
    _curDir           = Vector3.Lerp(_curDir, forward, j);

    var pos           = CarRenderTransform.parent.position + (_curDir * CameraOffset.z) + (Vector3.up * CameraOffset.y);
    var rot           = Quaternion.LookRotation(_curDir, Vector3.up);

    _curPos           = Vector3.Lerp(_curPos, pos, LerpFactor * deltaTime);
    _curRot           = Quaternion.Slerp(_curRot, rot, LerpFactor * deltaTime);
  }

  private void FollowCarAndLookAtBall(float deltaTime, bool reset = false)
  {
    var ballPosNoY    = _ballRenderTransform.parent.position;
    ballPosNoY.y      = CarRenderTransform.parent.position.y;

    var carPos        = CarRenderTransform.parent.position;
    var carToBall     = (ballPosNoY - carPos).normalized;

    var t             = reset ? 1f : LerpFactor * deltaTime;
    var dot           = Mathf.Abs(Vector3.Dot(_curDir.normalized, carToBall));
    var j             = Math.Max(0.2f, Mathf.Pow(dot, 10)) * t;
    _curDir           = Vector3.Lerp(_curDir, carToBall, j);

    var pos           = carPos + (carToBall * CameraOffset.z) + (Vector3.up * CameraOffset.y);
    var rot           = Quaternion.LookRotation(_curDir, Vector3.up);

    _curPos           = Vector3.Lerp(_curPos, pos, LerpFactor * deltaTime);
    _curRot           = Quaternion.Slerp(_curRot, rot, LerpFactor * deltaTime);
  }

  private void CalculateShake(float dt)
  {
    float speed       = _carController.Rigidbody.velocity.magnitude;
    float speedFactor = Mathf.InverseLerp(MinShakeSpeed, MaxShakeSpeed, speed);

    if (speedFactor > 0.01f)
    {
      float seed      = Sandbox.NetworkTime * ShakeFrequency;
      float x         = (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 2f;
      float y         = (Mathf.PerlinNoise(0f, seed) - 0.5f) * 2f;
      _curShakeOffset = new Vector3(x, y, 0) * (MaxShakeAmount * speedFactor);
    }
    else
    {
      _curShakeOffset = Vector3.MoveTowards(_curShakeOffset, Vector3.zero, dt);
    }
  }

  private void CalculateFOV(float dt)
  {
    var speed         = _carController.Rigidbody.velocity.magnitude;
    var targetFOV     = Mathf.Lerp(_baseFov, MaxSpeedFOV, speed / MaxShakeSpeed);
    _curFOV           = Mathf.Lerp(_curFOV, targetFOV, dt * 5f);
  }

}