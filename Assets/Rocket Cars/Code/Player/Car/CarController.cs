using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Netick.Unity;
using Netick;
using UnityEngine.UIElements;

/// <summary>
/// A simple physics controller for the car vehicle. Implements a custom vehicle model. Inspired by https://youtu.be/ueEmiDM94IE?t=863.
/// NOTE: We use the word 'force' here even though we mean acceleration. Force = acceleration when we ignore mass, given F = M * A.
/// </summary>
public class CarController : Replayable
{
  // Networked State ********************
  [Networked] public NetworkBool AirBoostUsed             { get; set; }
  [Networked] public NetworkBool AirPitchFlag             { get; set; } // used to prevent the player from directly pitching when the move forward is still pressed when jumping.
  [Networked] public Tick        JumpTickTimer            { get; set; } // time in ticks.
  [Networked] public Tick        AirBoostTickTimer        { get; set; } // time in ticks.
  [Networked] public Tick        FuelTickTime             { get; set; } // time in ticks.
  [Networked] public int         JumpCounts               { get; set; } // count of jumps, used to sync jump audio.
  [Networked] public NetworkBool IsGrounded               { get; set; } // is the car touching the ground.
  [Networked] public GameInput   LastInput                { get; set; } // We sync the last input for the player. So we can use it to predict remote players cars.

  // Public Events
  public UnityAction<Collision>  OnCollisionEnterEvent;
  public UnityAction             OnJumpAudioEvent;

  public Rigidbody               Rigidbody                { get; private set; }
  public NetworkRigidbody        NetworkRigidbody         { get; private set; }

  [Header("Simulation Features")]
  public bool                    EnableJump               = true;
  public bool                    EnableRocket             = true;
  public bool                    EnableAirControl         = true;
  public bool                    EnableAutoStabilization  = true;

  [Header("General Physics")]
  public float                   EngineForce              = 6;
  public float                   FrictionMultiplier       = 1f;
  public float                   MaxFrictionSpeed         = 20f;
  public float                   MinSteerSpeed            = 25;
  public float                   MaxSteerAngle            = 30;
  public float                   MinSteerAngle            = 10;
  public float                   GravityForce             = 6;

  public float                   BreakForce;
  public float                   StabilizationForce       = 14;
  public float                   VelocityBendingFactor    = 0.5f;
  public float                   LinearDrag               = 1f;
  public float                   AngularDrag              = 0.5f;
  public float                   WheelRadius              = 3.5f;
  public Transform               CenterOfMass;
  public AnimationCurve          SteerCurve;
  public AnimationCurve          FrictionCurve;
  public CarWheel[]              Wheels;

  [Header("Rocket")]
  public Transform               RocketForcePosition;
  public float                   RocketForce              = 10;
  public float                   MaxFuel                  = 10f;
  public float                   TimeAddedPerFuel         = 2f;

  [Header("Jumping")] 
  public float                   JumpForce;

  [Header("Air Control")]
  public Vector3                 AirSteerForce;
  public float                   AirBoostLinearForce;
  public float                   AirBoostTorque;
  public float                   AirborneLinearForce      = 6;

  [Header("Ball Impact")]
  public float                   BallImpactMultiplier;

  [Header("Visual")]
  public Transform               CarBody;
  public GameObject              RedCarModel;
  public GameObject              BlueCarModel;
  public ParticleSystem[]        AfterburnerParticleSystems;

  [Header("Visual Steering")]
  public float                   VisualSteerAngle         = 30;
  public Vector3                 VisualSteerAxis          = Vector3.right;
  private float                  _currentSteerAngle;

  [Header("Visual Suspension")]
  public Vector3                 SuspensionAxis           = Vector3.right;
  public Transform               SuspensionVisualizer ;
  public float                   K;
  public float                   D;
  public float                   MaxSuspensionDistance    = 0.5f;
  public float                   MaxRollAngle             = 5;
  public float                   MaxPitchAngle            = 5;
  public AnimationCurve          PitchAngleCurve;
  private Vector3                _suspensionPos ;
  private Vector3                _v;

  private int                    _envLayerMask;
  private int                    _ballLayerMask;
  private BoxCollider            _collider;
  private GameMode               _gm;
  private int                    _localJumpCounts         = -1;

  private void Awake()
  {
    Rigidbody                    = GetComponent<Rigidbody>();
    NetworkRigidbody             = GetComponent<NetworkRigidbody>();
    _collider                    = GetComponent<BoxCollider>();
    _envLayerMask                = (1 << LayerMask.NameToLayer("Env"));
    _ballLayerMask               = (1 << LayerMask.NameToLayer("Ball"));  
  }

  public void SetCarActive(bool active)
  {
    Rigidbody.isKinematic        = !active;
    if (active == false)
      Rigidbody.position         = Vector3.one * -1000f;
  }

  public override void NetworkStart()
  {
    base.NetworkStart();
    _gm                          = Sandbox.GetComponent<GlobalInfo>().GameMode;
  }

  // This script is divided into two main sections: Simulation and Render.
  // Simulation part is where we do per-tick logic to handle car physics.
  // Render part is where we do per-frame logic to handle visual-only things such as particle effects and suspension.

  // Note: I haven't added code for roll rotation of the wheels, this is mostly due to an issue with the model used. When using a different model, implementing the visual-only wheel rotation should be simple.

  // -------------------- Simulation (Physics) Section
  public override void NetworkFixedUpdate()
  {
    if (_gm.DisableCarSimulation)
      return;

    if (FetchInput(out GameInput input))
      LastInput          = input;

    SimulateVehicle(_gm.DisableInputForEveryone ? default : LastInput);
  }

  private void SimulateVehicle(GameInput input)
  {
    // clamp movement inputs
    input.Movement             = new Vector3(Mathf.Clamp(input.Movement.x, -1f, 1f), Mathf.Clamp(input.Movement.y, -1f, 1f), Mathf.Clamp(input.Movement.z, -1f, 1f));

    Rigidbody.centerOfMass     = CenterOfMass.localPosition;
    CalculateGroundCollision(out bool isGrounded, out var surfaceNormal);
    IsGrounded                 = isGrounded;
  
    // * linear motion and ground steering
    SimulateWheels(input.Movement.x, input.Movement.y * EngineForce, out var groundedWheels);

    // * auto stabilizing the car when flipping
    if (EnableAutoStabilization)
      SimulateAutoStabilization(input.Movement, surfaceNormal, IsGrounded, groundedWheels);

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
    int              numOfGroundedWheels = 0;
    Span<RaycastHit> hits                = stackalloc RaycastHit[Wheels.Length];
    var steerAngle                       = Mathf.Lerp(MinSteerAngle , MaxSteerAngle, SteerCurve.Evaluate(Mathf.InverseLerp(0f, MinSteerSpeed, Rigidbody.velocity.magnitude)));

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

    // only simulate lateralFriction when there are 3 or more grounded wheels.
    bool simulateFriction = numOfGroundedWheels >= 3;
    for (int i = 0; i < Wheels.Length; i++)
      if (hits[i].collider != null)
        SimulateWheel(Wheels[i], hits[i], simulateFriction, engineForce);

    if (numOfGroundedWheels >= 3)
      BendVelocity();

    groundedWheels = numOfGroundedWheels;
  }

  /// <summary>
  /// Simulates a simple car wheel.
  /// </summary>
  private void SimulateWheel(CarWheel wheel, RaycastHit hitInfo, bool simulateFriction, float engineForce)
  {
    var wheelTransform        = wheel.transform;
    var sideAxis              = wheelTransform.TransformVector(wheel.SideNormal); 
    var forwardAxis           = wheelTransform.forward;
    var vel                   = Vector3.ClampMagnitude(Rigidbody.GetPointVelocity(hitInfo.point), MaxFrictionSpeed);
    var groundDot             = Vector3.Dot(hitInfo.normal, wheelTransform.up);
    var forwardDot            = Vector3.Dot(forwardAxis * Mathf.Sign(engineForce), vel);

    if (forwardDot < 0 && (engineForce < 0)) // is breaking
      engineForce             = engineForce + (- BreakForce);

    var forwardSpeedAbs       = Mathf.Abs(Vector3.Dot(forwardAxis, vel));
    var sideSpeed             = Mathf.Abs(Vector3.Dot(sideAxis,    vel));
    var speedRatio            = vel.sqrMagnitude > 0.0001f ? sideSpeed / (sideSpeed + forwardSpeedAbs) : 0f;
    var lateralFriction       = FrictionMultiplier * FrictionCurve.Evaluate(speedRatio); 
    var lateralFrictionForce  = lateralFriction  * - sideAxis * Vector3.Dot(sideAxis, vel) * groundDot * groundDot * (simulateFriction ? 1f : 0f);
    var forwardForce          = forwardAxis * engineForce * groundDot * groundDot * (!wheel.IsFront ? 1f : 0f);
    Rigidbody.AddForceAtPosition(forwardForce, hitInfo.point, ForceMode.Acceleration);          // Motor Force
    if (forwardSpeedAbs == 0 && sideSpeed == 0)
      return;
    Rigidbody.AddForceAtPosition(lateralFrictionForce, hitInfo.point, ForceMode.Acceleration);  // Friction Force
    wheel.Speed               = forwardForce.magnitude;   
  }

  /// <summary>
  /// Simulates a force using the forward vector of the vehicle transform.
  /// </summary>
  /// <param name="boostInput"></param>
  private void SimulateRocket(bool boostInput)
  {
    var boostForce             = boostInput ? (FuelTickTime > 0 ? (RocketForce) : 0f) : 0;

    if (boostForce > 0)
    {
      boostForce = IsGrounded ? boostForce * 0.3f : boostForce;
      var rocketDir            = transform.forward;

      // adding rocket force
      Rigidbody.AddForceAtPosition(rocketDir * boostForce, RocketForcePosition.position, ForceMode.Acceleration);
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
        Rigidbody.AddForce(Vector3.up * JumpForce, ForceMode.VelocityChange);
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
  /// <param name="movement"></param>
  /// <param name="isGrounded"></param>
  private void SimulateAirControl(Vector3 movement, bool isGrounded)
  {
    if (!isGrounded)
    {
      Rigidbody.AddForce(transform.forward * AirborneLinearForce * movement.y, ForceMode.Acceleration);

      // we use a flag (AirPitchFlag) to let the player not pitch-rotate when holding the move forward button, and only pitch when they release it and press it again.
      if (AirPitchFlag == false && movement.y <= 0.5f)
        AirPitchFlag = true;
      var axis       = new Vector3(movement.y * AirSteerForce.x, movement.x * AirSteerForce.y, movement.z * AirSteerForce.z);

      if (AirPitchFlag == false)
        axis.x       = 0;

      Rigidbody.AddRelativeTorque(axis, ForceMode.Acceleration);
    }
  }

  /// <summary>
  /// Stabilizes the vehicle when upside down.
  /// </summary>
  /// <param name="movement"></param>
  /// <param name="surfaceNormal"></param>
  /// <param name="isGrounded"></param>
  /// <param name="numberOfGroundedWheels"></param>
  private void SimulateAutoStabilization(Vector3 movement, Vector3 surfaceNormal, bool isGrounded, int numberOfGroundedWheels)
  {
    var surfaceVsVehicleDown = Mathf.Max(0f, Vector3.Dot(surfaceNormal, -transform.up));
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

  private void CalculateGroundCollision(out bool isGrounded, out Vector3 surfaceNormal)
  {
    isGrounded               = Sandbox.Physics.Raycast(transform.position, Vector3.down, out var hitInfo, _collider.bounds.size.y / 1.5f, _envLayerMask);
    surfaceNormal            = hitInfo.normal;
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
    var boostForce = EnableRocket && LastInput.Rocket ? (FuelTickTime > 0 ? (RocketForce) : 0f) : 0;

    for (int i = 0; i < AfterburnerParticleSystems.Length; i++)
    {
      var emission                             = AfterburnerParticleSystems[i].emission;
      emission.enabled                         = boostForce > 0;
    }

    // wheel steering
    var s                                      = LastInput.Movement.x >= 0.1f ? 1f : (LastInput.Movement.x <= -0.1f ? -1f : 0);
    _currentSteerAngle                         = Mathf.Lerp(_currentSteerAngle, s * VisualSteerAngle, Time.deltaTime * 20f);

    for (int i = 0; i < Wheels.Length; i++)
    {
      if (Wheels[i].IsFront)
        Wheels[i].Render.localEulerAngles      = VisualSteerAxis * _currentSteerAngle;
    }

    RenderVisualSuspension();
    TryPlayJumpAudio();
  }

  private void TryPlayJumpAudio()
  {
    if (_localJumpCounts == -1)
      _localJumpCounts = JumpCounts;
    else if (_localJumpCounts != JumpCounts)
    {
      OnJumpAudioEvent();
      _localJumpCounts = JumpCounts;
    }
  }

  /// <summary>
  /// Simulates a visual-only suspension effect using a damped spring.
  /// </summary>
  private void RenderVisualSuspension()
  {
    var target                         = NetworkRigidbody.RenderTransform.position + ((NetworkRigidbody.RenderTransform.up + (NetworkRigidbody.RenderTransform.forward * 0.05f)) * 1f);

    if (Vector3.Distance(target, _suspensionPos) > 10f)
      _suspensionPos                   = target;
    
    var deltaTime                      = Time.smoothDeltaTime;
    _v                                 = (D * _v) + (deltaTime * K * (target - _suspensionPos));
    _suspensionPos                     = _suspensionPos + (deltaTime * _v);

    var localVel                       = CarBody.transform.InverseTransformVector(_v);
    var maxSpeed                       = 0.2f;
    localVel                           = Vector3.ClampMagnitude(localVel, maxSpeed);
    var pitchAngle                     = MaxPitchAngle * PitchAngleCurve.Evaluate(Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.y));
    var rollAngle                      = Mathf.Lerp(-MaxRollAngle, MaxRollAngle, Mathf.InverseLerp(-maxSpeed, maxSpeed, -localVel.z));
    CarBody.transform.localEulerAngles = new Vector3(0, rollAngle, pitchAngle);

    if (IsGrounded)
    {
      var p                            = SuspensionAxis * Mathf.Lerp(-MaxSuspensionDistance, MaxSuspensionDistance, Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.x));
      CarBody.transform.localPosition  = Vector3.Lerp(CarBody.transform.localPosition, p, Time.smoothDeltaTime * 20f);
    }

    else
    {
      CarBody.transform.localPosition  = Vector3.Lerp(CarBody.transform.localPosition, SuspensionAxis * MaxSuspensionDistance * 1.4f, Time.smoothDeltaTime * 10f);
    }

    if (SuspensionVisualizer != null)
      SuspensionVisualizer.position    = _suspensionPos;
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
