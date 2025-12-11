using JetBrains.Annotations;
using Netick;
using Netick.Samples;
using Netick.Unity;
using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A simple physics controller for the car vehicle. Implements a custom vehicle model. Inspired by https://youtu.be/ueEmiDM94IE?t=863.
/// NOTE: We use the word 'force' here even though we mean acceleration. Force = acceleration when we ignore mass, given F = M * A.
/// </summary>
public class CarController : Replayable
{
  // Networked State ********************
  [Networked] public GameInput   LastInput                             { get; set; } // we sync the last input used for the vehicle. So we can use it to predict cars of remote players.
  [Networked] public Tick        JumpTickTimer                         { get; set; } // time in ticks.
  [Networked] public Tick        AirBoostTickTimer                     { get; set; } // time in ticks.
  [Networked] public Tick        FuelTickTime                          { get; set; } // time in ticks.
  [Networked] public int         GroundedWheelsNum                     { get; set; } // num of grounded wheels.
  [Networked] public NetworkBool IsGrounded                            { get; set; } // is the car touching the ground.
  [Networked] public NetworkBool AirBoostUsed                          { get; set; } // did the car use the boost/double jump.
  [Networked] public NetworkBool AirPitchFlag                          { get; set; } // used to prevent the player from directly pitching when the move forward is still pressed when jumping.
  [Networked] public int         JumpCounts                            { get; set; } // count of jumps, used to sync jump audio.

  public Rigidbody               Rigidbody                             { get; private set; }
  public NetworkRigidbody        NetworkRigidbody                      { get; private set; }

  // Public Events
  public UnityAction<Collision>  OnCollisionEnterEvent;
  public UnityAction             OnJumpEvent;

  [Header("Simulation Features")]
  public bool                    EnableJump                            = true;
  public bool                    EnableRocket                          = true;
  public bool                    EnableAirControl                      = true;
  public bool                    EnableAutoStabilization               = true;

  [Header("General")]
  public float                   EngineForce                           = 6;
  public float                   BreakForce                            = 10f;
  public float                   GravityForce                          = 6;
  public float                   StabilizationForce                    = 14;
  public float                   VelocityBendingFactor                 = 0.5f;
  public float                   LinearDrag                            = 1f;
  public float                   AngularDrag                           = 0.5f;
  public Transform               CenterOfMass;
  public float                   WheelRadius                           = 3.5f;
  public CarWheel[]              Wheels;

  [Header("Grounded Steering")]
  public float                   MinSteerAngle                         = 10;
  public float                   MaxSteerAngle                         = 30; 
  public float                   SteerAngleMaxReductionVehicleVelocity = 25;
  public AnimationCurve          SteerAngleReductionCurve;
  public float                   FrictionMultiplier                    = 1f;

  [Header("Rocket")]
  public Transform               RocketForceTransform;
  public float                   RocketForce                           = 10;
  public float                   MaxFuel                               = 10f;
  public float                   TimeAddedPerFuel                      = 2f;

  [Header("Jumping")] 
  public float                   JumpForce                             = 5f;

  [Header("Air Control")]
  public Vector3                 AirSteerTorque;
  public float                   AirBoostLinearForce;
  public float                   AirBoostTorque;

  [Space(20)]
  [Header("Visual")]
  public Transform               CarBody;
  public ParticleSystem[]        AfterburnerParticleSystems;

  [Header("Wheel Rolling & Steering")]
  public Vector3                 WheelSteerAxis                        = Vector3.up;
  public Vector3                 WheelRollAxis                         = Vector3.forward;
  public float                   WheelMaxSteerAngle                    = 30;
  public float                   WheelMaxRollSpeed                     = 30;
  public float                   WheelRollSpeedFactor                  = 10;

  [Header("Suspension")]
  public Vector3                 SuspensionCompressionDirection        = Vector3.right;
  public Vector3                 SuspensionRollAxis                    = Vector3.forward;
  public Vector3                 SuspensionPitchAxis                   = Vector3.right;
  public float                   SpringStiffness                       = 65f;
  public float                   SpringDamping                         = 0.996f;
  public float                   SpringSpeedFactor                     = 0.01f;
  public float                   MaxSuspensionCompression              = 0.5f;
  public float                   MaxSuspensionRollAngle                = 5;
  public float                   MaxSuspensionPitchAngle               = 5;
  public Transform               SuspensionVisualizer;

  [Header("Networking")]
  public int                     InputReductionMaxLatency              = 350; // max latency in ms after which input reduction will be at its maximum.

  // private
  private Vector3                _curCarBodyLocalPos;
  private Quaternion             _curCarBodyLocalRot;
  private Vector3                _prevCarBodyLocalPos;
  private Quaternion             _prevCarBodyLocalRot;
  private Vector3                _springPos;
  private Vector3                _springVelocity;
  private float                  _suspensionRotBlendFactor;
  private float                  _currentWheelSteerAngle;
  private float                  _currentWheelRollAngle;
  private bool                   _resetSuspensionFlag;
  private int                    _numOfGroundedWheels;
  private int                    _envLayerMask;
  private int                    _ballLayerMask;
  private BoxCollider            _collider;
  private GameMode               _gm;
  
  private void Awake()
  {
    Rigidbody                    = GetComponent<Rigidbody>();
    NetworkRigidbody             = GetComponent<NetworkRigidbody>();
    _collider                    = GetComponent<BoxCollider>();
    _envLayerMask                = (1 << LayerMask.NameToLayer("Env"));
    _ballLayerMask               = (1 << LayerMask.NameToLayer("Ball"));  
  }

  public override void NetworkStart()
  {
    base.NetworkStart();
    _gm                          = Sandbox.GetComponent<GlobalInfo>().GameMode;
    if (IsReplay)
      Sandbox.Replay.Playback.OnSeeked += (t1, t2) => {_resetSuspensionFlag = true; };
  }

  public void SetCarActive(bool active)
  {
    Rigidbody.isKinematic = !active;
    if (active == false)
      Rigidbody.position = Vector3.one * -1000f;
  }

  // This script is divided into two main sections: Simulation and Render.
  // Simulation part is where we do per-tick logic to handle car physics.
  // Render part is where we do per-frame logic to handle visual-only things such as particle effects and suspension.

  // -------------------- Simulation (Physics) Section
  public override void NetworkFixedUpdate()
  {
    if (!_gm.DisableCarSimulation)
    {
      if (FetchInput(out GameInput input))
        LastInput = input;

      SimulateVehicle(_gm.DisableInputForEveryone ? default : LastInput);
    }

    if (!Application.isBatchMode && !IsResimulating) // when not in batch/headless mode, and not during resims
      AnimateSuspension(Sandbox.FixedDeltaTime); // we animate suspension with FixedDeltaTime to make it consistent regardless of framerate. And we interpolate the results in NetworkRender.
  }

  private void SimulateVehicle(GameInput input)
  {
    ClampInput(ref input);  // clamp inputs   
    ReduceInput(ref input); // reduce input based on latency for remote players - later predicted ticks get smaller input than earlier ticks.

    Rigidbody.centerOfMass  = CenterOfMass.localPosition;
    IsGrounded              = PerformGroundRaycast();
  
    // * linear motion and ground steering
    SimulateWheels(input.Movement.x, input.Movement.y * EngineForce, out var groundedWheels);

    // * auto stabilizing the car when flipping
    if (EnableAutoStabilization)
      SimulateAutoStabilization(input.Movement, IsGrounded, groundedWheels);

    // * rocket
    if (EnableRocket)
      SimulateRocket(input.Rocket);

    // * jumping and air boosting
    if (EnableJump)
      SimulateJumpAndAirBoost(input.Movement, input.Jump, IsGrounded);

    // * air control
    if (EnableAirControl)
      SimulateAirControl(input.Movement, IsGrounded);

    // * gravity
    if (AirBoostTickTimer <= 0)
     Rigidbody.AddForce(Vector3.down * GravityForce, ForceMode.Acceleration);

    // * drag
    if (!Rigidbody.isKinematic)
    {
      // linear drag
      Rigidbody.velocity        *= Mathf.Exp(-LinearDrag  * Sandbox.FixedDeltaTime);
      // angular drag
      Rigidbody.angularVelocity *= Mathf.Exp(-AngularDrag * Sandbox.FixedDeltaTime);
    }
  }

  private void SimulateWheels(float steer, float engineForce, out int groundedWheels)
  {
    Span<RaycastHit> hits                = stackalloc RaycastHit[Wheels.Length];
    var steerAngle                       = Mathf.Lerp(MinSteerAngle , MaxSteerAngle, SteerAngleReductionCurve.Evaluate(Mathf.InverseLerp(0f, SteerAngleMaxReductionVehicleVelocity, Rigidbody.velocity.magnitude)));
    int numOfGroundedWheels              = 0;

    for (int i = 0; i < Wheels.Length; i++)
    {
      var wheelTransform                 = Wheels[i].transform;
      if (Wheels[i].IsFront)
        wheelTransform.localRotation     = Quaternion.AngleAxis(steer * steerAngle, Vector3.up);
      bool isGrounded                    = Sandbox.Physics.Raycast(wheelTransform.position, -wheelTransform.transform.up, out var hitInfo, WheelRadius, _envLayerMask);
      hits[i]                            = hitInfo;
      Wheels[i].IsGrounded               = isGrounded;

      if (isGrounded)
        numOfGroundedWheels++;
    }

    GroundedWheelsNum                    = numOfGroundedWheels;

    for (int i = 0; i < Wheels.Length; i++)
    {
      if (hits[i].collider != null)
        SimulateWheel(Wheels[i], hits[i], engineForce);
      else
        Wheels[i].Speed = 0;
    }

    if (numOfGroundedWheels >= 1)
      Rigidbody.AddForceAtPosition(-transform.up * StabilizationForce, transform.position + (transform.up * 1.5f), ForceMode.Acceleration);

    if (numOfGroundedWheels >= 3)
      BendVelocity();

    groundedWheels = numOfGroundedWheels;
  }

  /// <summary>
  /// Simulates a simple car wheel.
  /// </summary>
  private void SimulateWheel(CarWheel wheel, RaycastHit hitInfo, float engineForce)
  {
    var wheelTransform        = wheel.transform;
    var sideAxis              = wheelTransform.TransformVector(wheel.SideNormal); 
    var forwardAxis           = wheelTransform.forward;
    var wheelVel              = Rigidbody.GetPointVelocity(hitInfo.point);
    var groundDot             = Vector3.Dot(hitInfo.normal, wheelTransform.up);
    var forwardDot            = Vector3.Dot(forwardAxis * Mathf.Sign(engineForce), wheelVel);

    if (forwardDot < 0 && (engineForce < 0)) // is breaking
      engineForce             = engineForce + (- BreakForce);

    var forwardSpeed          = Mathf.Abs(Vector3.Dot(forwardAxis, wheelVel));
    var sideSpeed             = Mathf.Abs(Vector3.Dot(sideAxis,    wheelVel));
    var speedRatio            = sideSpeed / (sideSpeed + forwardSpeed);
    var lateralFrictionForce  = FrictionMultiplier * Mathf.Sqrt(1f - speedRatio) * -sideAxis * Vector3.Dot(sideAxis, wheelVel) * groundDot * groundDot;
    var forwardForce          = forwardAxis * engineForce * groundDot * groundDot * (!wheel.IsFront ? 1f : 0f);
    Rigidbody.AddForceAtPosition(forwardForce, hitInfo.point, ForceMode.Acceleration);            // Motor Force

    if (forwardSpeed != 0 || sideSpeed != 0)
      Rigidbody.AddForceAtPosition(lateralFrictionForce, hitInfo.point, ForceMode.Acceleration);  // Friction Force
    wheel.Speed               = Vector3.Dot(wheelVel, forwardAxis) + (!wheel.IsFront ? engineForce * 100 : 0);   
  }

  /// <summary>
  /// Simulates a force using the forward vector of the vehicle transform.
  /// </summary>
  private void SimulateRocket(bool boostInput)
  {
    var boostForce             = boostInput ? (FuelTickTime > 0 ? (RocketForce) : 0f) : 0;

    if (boostForce > 0)
    {
      boostForce               = IsGrounded ? boostForce * 0.3f : boostForce;
      var rocketDir            = transform.forward;

      // adding rocket force
      Rigidbody.AddForceAtPosition(rocketDir * boostForce, RocketForceTransform.position, ForceMode.Acceleration);
      FuelTickTime             = Mathf.Max(0, FuelTickTime - 1);
    }
  }

  private void SimulateJumpAndAirBoost(Vector3 movement, bool jumpInput, bool isGrounded)
  {
    if (!IsProxy) // proxies don't predict air boost - proxies are remote players, meaning everyone who's not the local player.
    {
      if (JumpTickTimer != 0 && !AirBoostUsed)
      {
        if (jumpInput)
        {
          var linear        = new Vector3(movement.x, 0.2f, movement.y);
          Rigidbody.AddRelativeForce(linear * AirBoostLinearForce, ForceMode.VelocityChange);
          AirBoostUsed      = true;
          AirBoostTickTimer = Sandbox.TimeToTick(1f);
          JumpCounts++;
        }
      }
    }

    if (AirBoostTickTimer > 0)
    {
      var rotational        = new Vector3(movement.y, 0, movement.x);
      Rigidbody.AddRelativeTorque(rotational * AirBoostTorque, ForceMode.Acceleration);
      AirBoostTickTimer    -= 1;
    }

    if (JumpTickTimer == 0)
    {
      // jump
      if (jumpInput && isGrounded)
      {
        AirPitchFlag        = false;
        Rigidbody.AddForce(transform.up * JumpForce, ForceMode.VelocityChange);
        JumpTickTimer       = Sandbox.TimeToTick(1f);
        JumpCounts++;
      }
    }
    else
    {
      JumpTickTimer         = Mathf.Max(0, JumpTickTimer - 1);
      if (JumpTickTimer == 0)
        AirBoostUsed        = false;
    }
  }

  /// <summary>
  /// Simulates pitch, yaw, and roll torques when flying.
  /// </summary>
  private void SimulateAirControl(Vector3 movement, bool isGrounded)
  {
    if (!isGrounded)
    {
      // we use a flag (AirPitchFlag) to let the player not pitch-rotate when holding the move forward button, and only pitch when they release it and press it again.
      if (AirPitchFlag == false && movement.y <= 0.5f)
        AirPitchFlag = true;
      var axis       = new Vector3(movement.y * AirSteerTorque.x, movement.x * AirSteerTorque.y, movement.z * AirSteerTorque.z);

      if (AirPitchFlag == false)
        axis.x       = 0;

      Rigidbody.AddRelativeTorque(axis, ForceMode.Acceleration);
    }
  }

  /// <summary>
  /// Stabilizes the vehicle when upside down.
  /// </summary>
  private void SimulateAutoStabilization(Vector3 movement, bool isGrounded, int numberOfGroundedWheels)
  {
    var surfaceVsVehicleDown = Mathf.Max(0f, Vector3.Dot(Vector3.up, -transform.up));
    if (numberOfGroundedWheels < 3 && isGrounded)
    {
      if (surfaceVsVehicleDown > 0.5f)
      {
        float throttleFactor = MathF.Abs(movement.y) > 0.1f ? 1f : 0f;
        float sign           = Vector3.Dot(Vector3.up, transform.right) > 0 ? -1 : 1;
        Rigidbody.AddRelativeTorque(throttleFactor * sign * Vector3.forward * StabilizationForce, ForceMode.VelocityChange);
      }
    }
  }

  private void BendVelocity()
  {
    if (Rigidbody.isKinematic)
      return;

    // we bend velocity towards the desired direction to make changing directions faster.
    // we only bend the xy plane velocity, we don't bend the velocity in the upward/downward direction.
    var currentVel           = Rigidbody.velocity;
    var xyVel                = new Vector2(currentVel.x, currentVel.z);
    var forward              = transform.forward;
    forward.y                = 0;
    forward                  = forward * xyVel.magnitude;

    var targetVel            = new Vector3(forward.x, currentVel.y, forward.z); // we don't bend the y axis.

    // we only bend towards velocities that don't differ greatly from the current velocity of the vehicle.
    var t                    = Math.Max(0f, Vector3.Dot(currentVel.normalized, targetVel.normalized)) * VelocityBendingFactor * Sandbox.FixedDeltaTime;
    Rigidbody.velocity       = Vector3.Lerp(Rigidbody.velocity, targetVel, t);
  }

  private bool PerformGroundRaycast()
  {
    bool didHit              = Sandbox.Physics.Raycast(transform.position, -transform.up, out var hitInfo, _collider.bounds.size.y / 1.5f, _envLayerMask);
    if (!didHit)
      didHit                 = Sandbox.Physics.Raycast(transform.position, transform.up, out hitInfo, _collider.bounds.size.y / 1.5f, _envLayerMask);
    return didHit;
  }

  private void ClampInput(ref GameInput input)
  {
    input.Movement           = new Vector3(Mathf.Clamp(input.Movement.x, -1f, 1f), Mathf.Clamp(input.Movement.y, -1f, 1f), Mathf.Clamp(input.Movement.z, -1f, 1f));
  }

  private void ReduceInput(ref GameInput input)
  {
    var latencyMultiplier    = 1f;

    if (IsClient && !IsInputSource)
    {
      var maxLatency         = InputReductionMaxLatency;
      var minFactor          = 0.2f;  // controls max input reduction after exceeding `maxLatency` - should be a configurable setting in inspector
      latencyMultiplier      = Mathf.Max(minFactor, Mathf.InverseLerp(maxLatency, 0f, Sandbox.TickToTime(Sandbox.Tick - Sandbox.AuthoritativeTick) * 1000));
    }

    input.Movement           = input.Movement * latencyMultiplier;
  }

  public void ReceiveFuel()
  {
    FuelTickTime             = Sandbox.TimeToTick(Mathf.Min(MaxFuel, Sandbox.TickToTime(FuelTickTime) + TimeAddedPerFuel));
  }

  public void OnCollisionEnter(Collision collision)
  {
    if (Object != null && !IsResimulating)
      OnCollisionEnterEvent?.Invoke(collision);
  }

  // -------------------- Render (Visuals/Audio) Section
  public override void NetworkRender()
  {
    // interpolating suspension results.
    CarBody.transform.localPosition        = Vector3.   Lerp (_prevCarBodyLocalPos, _curCarBodyLocalPos, Sandbox.LocalInterpolation.Alpha);
    CarBody.transform.localRotation        = Quaternion.Slerp(_prevCarBodyLocalRot, _curCarBodyLocalRot, Sandbox.LocalInterpolation.Alpha);
    AnimateWheels(Time.deltaTime);
    for (int i = 0; i < AfterburnerParticleSystems.Length; i++)
    {
      var emission                         = AfterburnerParticleSystems[i].emission;
      var rocketForce                      = EnableRocket && LastInput.Rocket ? (FuelTickTime > 0 ? (RocketForce) : 0f) : 0;
      emission.enabled                     = rocketForce > 0;
    }
  }

  private void AnimateWheels(float deltaTime)
  {
    var s                                  = LastInput.Movement.x >= 0.1f ? 1f : (LastInput.Movement.x <= -0.1f ? -1f : 0);
    _currentWheelSteerAngle                = Mathf.Lerp(_currentWheelSteerAngle, s * WheelMaxSteerAngle, deltaTime * 20f);

    for (int i = 0; i < Wheels.Length; i++)
    {
      var wheel = Wheels[i];
      Wheels[i].VisualSpeed                = Mathf.Lerp (Wheels[i].VisualSpeed, Wheels[i].Speed, 40f * deltaTime);
      var rollSpeed                        = Mathf.Clamp(Wheels[i].VisualSpeed, -WheelMaxRollSpeed, WheelMaxRollSpeed);
      _currentWheelRollAngle              += rollSpeed * deltaTime * WheelRollSpeedFactor;
      var yawRot                           = Quaternion.AngleAxis(Wheels[i].IsFront ? _currentWheelSteerAngle : 0 ,WheelSteerAxis);
      var rollRot                          = Quaternion.AngleAxis(_currentWheelRollAngle, WheelRollAxis);
      wheel.Render.transform.localRotation = yawRot * rollRot;
    }
  }

  /// <summary>
  /// Simulates a visual-only suspension effect using a damped spring.
  /// </summary>
  private void AnimateSuspension(float deltaTime)
  {
    _prevCarBodyLocalPos                   = _curCarBodyLocalPos;
    _prevCarBodyLocalRot                   = _curCarBodyLocalRot;
    var target                             = NetworkRigidbody.RenderTransform.position + ((NetworkRigidbody.RenderTransform.up + (NetworkRigidbody.RenderTransform.forward * 0.05f)) * 1f);

    if (Vector3.Distance(target, _springPos) > 10f || _resetSuspensionFlag)
      _springPos                           = target;

    _springVelocity                        = (SpringDamping * _springVelocity) + (SpringStiffness * (target - _springPos) * deltaTime);
    _springPos                             = _springPos + ( _springVelocity * deltaTime);
    _suspensionRotBlendFactor              = Mathf.Lerp(_suspensionRotBlendFactor, (GroundedWheelsNum == 0 || _resetSuspensionFlag) ? 0f : 1f, deltaTime * 10f);
    var maxSpeed                           = 0.2f;
    var localVel                           = Vector3.ClampMagnitude(NetworkRigidbody.RenderTransform.InverseTransformVector(_springVelocity) * SpringSpeedFactor, maxSpeed);
    var pitchAngle                         = Mathf.Lerp(-MaxSuspensionPitchAngle,  MaxSuspensionPitchAngle,  Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.z));
    var rollAngle                          = Mathf.Lerp(-MaxSuspensionRollAngle,   MaxSuspensionRollAngle,   Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.x));
    var yOffset                            = Mathf.Lerp(-MaxSuspensionCompression, MaxSuspensionCompression, Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.y)) * SuspensionCompressionDirection;
    _curCarBodyLocalPos                    = Vector3.Lerp(default, Vector3.Lerp(CarBody.transform.localPosition, yOffset, deltaTime * 20f), _suspensionRotBlendFactor);
    _curCarBodyLocalRot                    = Quaternion.Lerp(Quaternion.identity, Quaternion.AngleAxis(pitchAngle, SuspensionPitchAxis) * Quaternion.AngleAxis(rollAngle, SuspensionRollAxis), _suspensionRotBlendFactor);

    if (SuspensionVisualizer != null)
      SuspensionVisualizer.position        = _springPos;

    _resetSuspensionFlag = false;
  }

  [OnChanged(nameof(JumpCounts))][UsedImplicitly]
  void OnJumpCountsChanged(OnChangedData dat)
  {
    if (!dat.IsCatchingUp)
      OnJumpEvent();
  }

  private void OnDrawGizmos()
  {
    if (Sandbox != null && !Sandbox.IsVisible)
      return;

    if (_collider != null)
    {
      Gizmos.matrix = _collider.transform.localToWorldMatrix;
      Gizmos.color  = Color.red;
      Gizmos.DrawWireCube(_collider.center, _collider.size);
    }
  }
}
