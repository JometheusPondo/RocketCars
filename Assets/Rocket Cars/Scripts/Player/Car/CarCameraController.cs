using Netick;
using Netick.Unity;
using System;
using UnityEngine;

[ExecuteAfter(typeof(CarController))]
public class CarCameraController : NetworkBehaviour
{
  public Transform       CarRenderTransform;

  [Header("General")]
  public bool            LookAtBall              = true;
  public Vector3         CameraOffset;
  public float           LerpFactor              = 20f;
  public AudioClip       ChangeCameraAudioClip;

  [Header("Speed Shake")]
  public float           MinShakeSpeed           = 5f;
  public float           MaxShakeSpeed           = 60f;
  public float           ShakeFrequency          = 35f;
  public float           ShakeIntensity          = 2.15f;
  public float           MaxRotationTilt         = 0.5f;
  public float           MaxRotationRoll         = 1.5f; 

  [Header("Drift Shake Settings")] 
  public float           DriftMaxShakeSpeed      = 40f;
  public float           DriftShakeIntensity     = 2.0f;
  public float           DriftShakeFreq          = 40f;

  [Header("Collision Shake")]
  public float           ImpactIntensityMult     = 0.3f;  
  public float           BallIAndCarmpactIntensityMult = 0.6f;  
  public float           CollisionDecay          = 8f;    

  [Header("Dynamic FOV")]
  public float           MaxSpeedFOV             = 90f;
  public float           FOVWeight               = 0.5f;
  public float           FOVModifier             = 1.0f;

  // private
  private Camera         _camera;
  private CarController  _carController;
  private Player         _player;
  private GameMode       _gm;
  private Ball           _ball;
  private Transform      _ballRenderTransform;
  private bool           _prevDisableCarCamera;
  private Vector3        _curDir;
  private Vector3        _curPos, _prevPos;
  private Quaternion     _curRot, _prevRot;
  private Quaternion     _curShakeRot, _prevShakeRot;
  private Quaternion     _curDriftRot, _prevDriftRot;
  private float          _collisionAmount;
  private float          _curFOV, _prevFOV;
  private float          _baseFov;
  private int            _envLayer;
  public override void NetworkStart()
  {
    _envLayer            = LayerMask.NameToLayer("Env");
    _player              = GetComponent<Player>();
    _carController       = GetComponent<CarController>();
    _gm                  = Sandbox.GetComponent<GlobalData>().GameMode;
    _camera              = Sandbox.GetComponent<GlobalData>().Camera;
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
  }

  public override void NetworkRender()
  {
    var plrId = _gm.GlobalData.IsReplay ? _gm.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId;

    if (InputSourcePlayerId != plrId || !_player.IsReady)
      return;

    if (_gm.DisableCarCamera != _prevDisableCarCamera)
      ResetCamera();

    _prevDisableCarCamera = _gm.DisableCarCamera;

    if (_gm.DisableCarCamera)
      return;

    bool toggleInput = _gm.GlobalData.IsReplay ? Input.GetKeyDown(KeyCode.LeftShift) : _gm.GlobalData.CanUseInput && Input.GetButtonDown("Camera Toggle");

    if (toggleInput)
    {
      LookAtBall = !LookAtBall;
      if (Sandbox.IsVisible)
        AudioSource.PlayClipAtPoint(ChangeCameraAudioClip, transform.position);
    }

    // interpolate everything
    var alpha                  = Sandbox.LocalInterpolation.Alpha;
    var basePos                = Vector3.Lerp(_prevPos, _curPos, alpha);
    var baseRot                = Quaternion.Slerp(_prevRot, _curRot, alpha);
    var shakeRot               = Quaternion.Slerp(_prevShakeRot, _curShakeRot, alpha);
    var driftRot               = Quaternion.Slerp(_prevDriftRot, _curDriftRot, alpha);
    _camera.transform.position = basePos;
    _camera.transform.rotation = baseRot * shakeRot * driftRot;
    _camera.fieldOfView        = FOVModifier * Mathf.Lerp(_prevFOV, _curFOV, alpha);
  }

  public override void NetworkFixedUpdate()
  {
    var plrId        = _gm.GlobalData.IsReplay ? _gm.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId;
    float dt         = Sandbox.FixedDeltaTime;

    if (IsResimulating || InputSourcePlayerId != plrId || !_player.IsReady)
      return;

    // store previous states for interpolation
    _prevPos         = _curPos;
    _prevRot         = _curRot;
    _prevFOV         = _curFOV;
    _prevShakeRot    = _curShakeRot; 
    _prevDriftRot    = _curDriftRot;
    CalculateShake(dt);
    CalculateDriftAndCollisionShake(dt);
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
    var forward           = _carController.NetworkRigidbody.Velocity;

    if (_carController.IsOnGround)
      forward             = Input.GetMouseButton(2) ? -transform.forward : transform.forward;

    forward.y             = 0;
    forward.Normalize();

    if (forward == Vector3.zero) 
      forward             = _curDir;

    var dot               = Mathf.Abs(Vector3.Dot(_curDir.normalized, forward));
    var j                 = Math.Max(0.2f, Mathf.Pow(dot, 10)) * LerpFactor * deltaTime;
    _curDir               = Vector3.Lerp(_curDir, forward, j);

    var pos               = CarRenderTransform.parent.position + (_curDir * CameraOffset.z) + (Vector3.up * CameraOffset.y);
    var rot               = Quaternion.LookRotation(_curDir, Vector3.up);

    _curPos               = Vector3.Lerp(_curPos, pos, LerpFactor * deltaTime);
    _curRot               = Quaternion.Slerp(_curRot, rot, LerpFactor * deltaTime);
  }

    private void FollowCarAndLookAtBall(float deltaTime, bool reset = false)
    {
        var ballPos = _ballRenderTransform.parent.position;
        var carPos = CarRenderTransform.parent.position;

        var focusLocation = carPos + (CarRenderTransform.parent.forward * 0.3f);

        var carToBall = ballPos - focusLocation;
        var flatDir = new Vector3(carToBall.x, 0f, carToBall.z);
        flatDir.Normalize();

        var t = reset ? 1f : LerpFactor * deltaTime;
        var dot = Mathf.Abs(Vector3.Dot(_curDir.normalized, flatDir));
        var j = Math.Max(0.2f, Mathf.Pow(dot, 10)) * t;
        _curDir = Vector3.Lerp(_curDir, flatDir, j);

        var pos = focusLocation
                                  - (_curDir * Mathf.Abs(CameraOffset.z))
                                  + (Vector3.up * CameraOffset.y);

        var rot = Quaternion.LookRotation(_curDir, Vector3.up);

        _curPos = Vector3.Lerp(_curPos, pos, LerpFactor * deltaTime);
        _curRot = Quaternion.Slerp(_curRot, rot, LerpFactor * deltaTime);
    }

    private void CalculateShake(float dt)
  {
    float speed           = _carController.NetworkRigidbody.Velocity.magnitude;
    float speedFactor     = Mathf.InverseLerp(MinShakeSpeed, MaxShakeSpeed, speed);

    if (speedFactor > 0.01f)
    {
      float seed          = (float)(Sandbox.NetworkTimeAsDouble % 10.0) * ShakeFrequency;
      float p             = (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 2f;
      float y             = (Mathf.PerlinNoise(0f, seed) - 0.5f) * 2f;
      float r             = (Mathf.PerlinNoise(seed, seed) - 0.5f) * 2f;
      _curShakeRot        = Quaternion.Euler(p * ShakeIntensity * speedFactor, y * ShakeIntensity * speedFactor,r * ShakeIntensity * speedFactor);
    }
    else
    {
      _curShakeRot        = Quaternion.Slerp(_curShakeRot, Quaternion.identity, dt * 5f);
    }
  }

  private void CalculateDriftAndCollisionShake(float dt)
  {
    _collisionAmount      = Mathf.Lerp(_collisionAmount, 0, dt * CollisionDecay);
    float sideSpeedFactor = Mathf.InverseLerp(0f, DriftMaxShakeSpeed, Mathf.Abs(_carController.SideSpeed));
    float driftIntensity  = (_carController.IsSlipping && sideSpeedFactor > 0.05f) ? (sideSpeedFactor * DriftShakeIntensity) : 0f;
    float totalIntensity  = driftIntensity + _collisionAmount;

    if (totalIntensity > 0.01f)
    {
      float seed          = (float)(Sandbox.NetworkTimeAsDouble % 10.0) * DriftShakeFreq;
      float p             = (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 0.5f;
      float y             = (Mathf.PerlinNoise(0f, seed) - 0.5f) * 1.0f;
      float r             = (Mathf.PerlinNoise(seed, seed) - 0.5f) * 2.0f;

      _curDriftRot        = Quaternion.Euler(p * totalIntensity, y * totalIntensity, r * totalIntensity);
    }
    else
    {
      _curDriftRot        = Quaternion.Slerp(_curDriftRot, Quaternion.identity, dt * 5f);
    }
  }

  private void CalculateFOV(float dt)
  {
    var speed             = _carController.Rigidbody.velocity.magnitude;
    var targetFOV         = Mathf.Lerp(_baseFov, MaxSpeedFOV, speed / MaxShakeSpeed);
    _curFOV               = Mathf.Lerp(_curFOV, targetFOV, dt * 5f);
  }

  private void OnCollisionEnter(Collision collision)
  {
    if (Sandbox == null || IsResimulating)
      return;

    var impactForce      = collision.relativeVelocity.magnitude;
    var plrId            = _gm.GlobalData.IsReplay ? _gm.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId;

    if (impactForce > 1f && plrId == InputSourcePlayerId)
    {
      if (collision.gameObject.layer != _envLayer)
        _collisionAmount = Mathf.Clamp(_collisionAmount + (impactForce * BallIAndCarmpactIntensityMult), 0, 15f);
      else
        _collisionAmount = Mathf.Clamp(_collisionAmount + (Mathf.Min(impactForce, 10f) * ImpactIntensityMult), 0, 15f);
    }
  }



}