using Netick;
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
  public float                   MaxWheelVsGroundContactPointSpeed     = 20f;
  public AnimationCurve          FrictionCurve;


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
  public float                   AirborneLinearForce                   = 6;

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

  // private
  private Vector3                _springPos;
  private Vector3                _springVelocity;
  private float                  _currentWheelSteerAngle;
  private float                  _currentWheelRollAngle;
  private int                    _localJumpCounts                      = -1;
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
    IsGrounded                 = PerformGroundRaycast();
  
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

    // only simulate lateralFriction when there are 3 or more grounded wheels.
    bool simulateFriction = numOfGroundedWheels >= 2;
    for (int i = 0; i < Wheels.Length; i++)
    {
      if (hits[i].collider != null)
        SimulateWheel(Wheels[i], hits[i], simulateFriction, engineForce);
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
  private void SimulateWheel(CarWheel wheel, RaycastHit hitInfo, bool simulateFriction, float engineForce)
  {
    var wheelTransform        = wheel.transform;
    var sideAxis              = wheelTransform.TransformVector(wheel.SideNormal); 
    var forwardAxis           = wheelTransform.forward;
    var vel                   = Vector3.ClampMagnitude(Rigidbody.GetPointVelocity(hitInfo.point), MaxWheelVsGroundContactPointSpeed);
    var groundDot             = Vector3.Dot(hitInfo.normal, wheelTransform.up);
    var forwardDot            = Vector3.Dot(forwardAxis * Mathf.Sign(engineForce), vel);

    if (forwardDot < 0 && (engineForce < 0)) // is breaking
      engineForce             = engineForce + (- BreakForce);

    var forwardSpeedAbs       = Mathf.Abs(Vector3.Dot(forwardAxis, vel));
    var sideSpeed             = Mathf.Abs(Vector3.Dot(sideAxis,    vel));
    var speedRatio            = vel.sqrMagnitude > 0.0001f ? sideSpeed / (sideSpeed + forwardSpeedAbs) : 0f;
    var lateralFriction       = FrictionMultiplier * (FrictionCurve.Evaluate(1f - speedRatio)); 
    var lateralFrictionForce  = lateralFriction  * - sideAxis * Vector3.Dot(sideAxis, vel) * groundDot * groundDot * (simulateFriction ? 1f : 0f);
    var forwardForce          = forwardAxis * engineForce * groundDot * groundDot * (!wheel.IsFront ? 1f : 0f);
    Rigidbody.AddForceAtPosition(forwardForce, hitInfo.point, ForceMode.Acceleration);          // Motor Force
    if (forwardSpeedAbs == 0 && sideSpeed == 0)
      return;
    Rigidbody.AddForceAtPosition(lateralFrictionForce, hitInfo.point, ForceMode.Acceleration);  // Friction Force
    wheel.Speed               = Vector3.Dot(vel, forwardAxis) + (!wheel.IsFront ? engineForce * 100 : 0);   
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
      Rigidbody.AddForce(transform.forward * AirborneLinearForce * movement.y, ForceMode.Acceleration);

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
    for (int i = 0; i < AfterburnerParticleSystems.Length; i++)
    {
      var emission                         = AfterburnerParticleSystems[i].emission;
      var rocketForce                      = EnableRocket && LastInput.Rocket ? (FuelTickTime > 0 ? (RocketForce) : 0f) : 0;
      emission.enabled                     = rocketForce > 0;
    }

    AnimateWheels();
    AnimateSuspension();
    TryInvokeJumpEvent();
  }

  private void AnimateWheels()
  {
    var deltaTime                          = Time.deltaTime;
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
  private void AnimateSuspension()
  {
    var deltaTime                          = Time.deltaTime;
    var carBody                            = CarBody;
    var target                             = NetworkRigidbody.RenderTransform.position + ((NetworkRigidbody.RenderTransform.up + (NetworkRigidbody.RenderTransform.forward * 0.05f)) * 1f);

    if (Vector3.Distance(target, _springPos) > 10f)
      _springPos                           = target;

    _springVelocity                        = (SpringDamping * _springVelocity) + (deltaTime * SpringStiffness * (target - _springPos));
    _springPos                             = _springPos + (deltaTime * _springVelocity);

    if (GroundedWheelsNum == 0)
    {
      _springVelocity                      = Vector3.Lerp(_springVelocity, default, Time.smoothDeltaTime * 20f);
      _springPos                           = Vector3.Lerp(_springPos, target, Time.smoothDeltaTime * 20f);
    }

    var maxSpeed                           = 0.2f;
    var localVel                           = Vector3.ClampMagnitude(NetworkRigidbody.RenderTransform.InverseTransformVector(_springVelocity) * SpringSpeedFactor, maxSpeed);
    var pitchAngle                         = Mathf.Lerp(-MaxSuspensionPitchAngle,  MaxSuspensionPitchAngle,  Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.z));
    var rollAngle                          = Mathf.Lerp(-MaxSuspensionRollAngle,   MaxSuspensionRollAngle,   Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.x));
    var yOffset                            = Mathf.Lerp(-MaxSuspensionCompression, MaxSuspensionCompression, Mathf.InverseLerp(-maxSpeed, maxSpeed, localVel.y)) * SuspensionCompressionDirection;

    carBody.transform.localPosition        = Vector3.Lerp(carBody.transform.localPosition, yOffset, Time.smoothDeltaTime * 20f);
    carBody.transform.localEulerAngles     = (SuspensionPitchAxis * pitchAngle) + (SuspensionRollAxis * rollAngle);

    if (SuspensionVisualizer != null)
      SuspensionVisualizer.position        = _springPos;
  }

  private void TryInvokeJumpEvent()
  {
    if (_localJumpCounts == -1)
      _localJumpCounts = JumpCounts;
    else if (_localJumpCounts != JumpCounts)
    {
      OnJumpEvent();
      _localJumpCounts = JumpCounts;
    }
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
