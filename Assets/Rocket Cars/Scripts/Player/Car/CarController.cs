using Netick;
using Netick.Unity;
using System.Collections.Specialized;
using UnityEngine;
using UnityEngine.Events;
using static System.Net.Mime.MediaTypeNames;


/// Rocket League-accurate vehicle physics controller for Unity + Netick.
/// Physics model ported from RocketSim (https://github.com/ZealanL/RocketSim).
///
/// COORDINATE SYSTEMS:
///   Rocket League: X=Forward, Y=Right, Z=Up
///   Unity:         X=Right,   Y=Up,    Z=Forward
///   Conversion:    RLC.RLToUnity(rlVec) handles this.
///
/// SCALE:
///   The S field (UUToUnity) converts UU to Unity meters.
///   Default S=0.01 means 1 UU = 1 cm = 0.01 Unity meters.
///
/// SETUP:
///   1. Set S to match your car model scale. If your car mesh is ~2.4m long, S~=0.02.
///   2. Set Rigidbody mass to 180, drag/angularDrag to 0.
///   3. Disable Unity gravity (use custom gravity here).
///   4. Position CarWheel transforms at the visual wheel locations.
///   5. Update your Input provider to populate the new GameInput fields.

public class CarController : GoalReplayable
{
    // ========================================================================
    //  NETWORKED STATE
    // ========================================================================

    [Networked] public GameInput LastInput { get; set; }
    [Networked] public float Boost { get; set; }
    [Networked] public NetworkBool IsBoosting { get; set; }
    [Networked] public float BoostingTime { get; set; }
    [Networked] public NetworkBool IsOnGround { get; set; }
    [Networked] public int GroundedWheelsNum { get; set; }
    [Networked] public NetworkBool HasJumped { get; set; }
    [Networked] public NetworkBool IsJumping { get; set; }
    [Networked] public float JumpTime { get; set; }
    [Networked] public NetworkBool HasFlipped { get; set; }
    [Networked] public NetworkBool HasDoubleJumped { get; set; }
    [Networked] public NetworkBool IsFlipping { get; set; }
    [Networked] public float FlipTime { get; set; }
    [Networked] public Vector3 FlipRelTorque_RL { get; set; } 
    [Networked] public float HandbrakeVal { get; set; }
    [Networked] public float AirTime { get; set; }
    [Networked] public float AirTimeSinceJump { get; set; }
    [Networked] public NetworkBool IsAutoFlipping { get; set; }
    [Networked] public float AutoFlipTimer { get; set; }
    [Networked] public float AutoFlipTorqueScale { get; set; }
    [Networked] public NetworkBool IsSupersonic { get; set; }
    [Networked] public float SupersonicTime { get; set; }
    [Networked] public int JumpTrigger { get; set; } // For audio sync

    // ========================================================================
    //  CONFIGURATION
    // ========================================================================

    [Header("Scale -- CRITICAL: Match to your asset size")]
    [Tooltip("Unity meters per Rocket League Unreal Unit. Default 0.01 = real-world scale.")]
    public float S = 0.01f;

    [Header("Simulation Features")]
    public bool EnableJump = true;
    public bool EnableBoost = true;
    public bool EnableAirControl = true;
    public bool EnableAutoStabilization = true;

    [Header("References")]
    public Transform CenterOfMass;
    public Transform RocketForceTransform;
    public CarWheel[] Wheels; 
    public Transform CarBody;

    [Header("Visual -- Afterburner / Drift Particles")]
    public ParticleSystem[] AfterburnerParticleSystems;
    public ParticleSystem[] DriftParticleSystems;
    public float DriftMinVelocity = 5f;

    [Header("Visual -- Suspension Spring")]
    public float SpringStiffnessVisual = 65f;
    public float SpringDampingVisual = 0.996f;
    public float SpringSpeedFactor = 0.01f;
    public float MaxSuspensionCompression = 0.5f;
    public float MaxSuspensionRollAngle = 5f;
    public float MaxSuspensionPitchAngle = 5f;
    public Vector3 SuspensionCompressionDirection = Vector3.right;
    public Vector3 SuspensionRollAxis = Vector3.forward;
    public Vector3 SuspensionPitchAxis = Vector3.right;

    [Header("Visual -- Wheel Animation")]
    public Vector3 WheelSteerAxis = Vector3.up;
    public Vector3 WheelRollAxis = Vector3.forward;
    public float WheelMaxSteerAngle = 30;
    public float WheelMaxRollSpeed = 30;
    public float WheelRollSpeedFactor = 10;

    [Header("Networking")]
    public float InputDecayMaxLatency = 200f;
    public float InputDecayMinFactor = 0.1f;
    public int VelocityDecayMinLatency = 150;

    // Public accessors
    public Rigidbody Rigidbody { get; private set; }
    public NetworkRigidbody NetworkRigidbody { get; private set; }
    public bool IsSlipping { get; private set; }
    public float SideSpeed { get; private set; }

    public UnityAction<Collision> OnCollisionEnterEvent;
    public UnityAction OnJumpEvent;

    // ========================================================================
    //  PER-WHEEL PHYSICS STATE (not networked -- recomputed each tick)
    // ========================================================================

    private struct WheelPhysState
    {
        // Config
        public Vector3 connectionLocal; 
        public bool isFront;
        public float radius;           
        public float restLength;       
        public float forceScale;

        // Per-tick
        public bool inContact;
        public bool inContactWithWorld;
        public Vector3 contactPoint;
        public Vector3 contactNormal;
        public Vector3 hardPointWS;
        public float suspensionLength;
        public float suspensionRelVel;
        public float invContactDotSus;
        public float suspensionForce;
        public float steerAngle;
        public float engineForce;
        public float brake;
        public float latFriction;
        public float longFriction;
        public Vector3 frictionImpulse;
        public float forwardSpeed;
    }

    // ========================================================================
    //  PRIVATE STATE
    // ========================================================================

    private WheelPhysState[] _ws; // 4 wheels
    private int _envLayerMask;
    private BoxCollider _collider;
    private GameMode _gm;
    private Vector3 _authVel;
    private bool _worldContactHasContact;
    private Vector3 _worldContactNormal;
    private GameInput _lastControls;
    private float _maxSusTravel; // scaled

    // Visual suspension state
    private Vector3 _springPos;
    private Vector3 _springVelocity;
    private Vector3 _curCarBodyLocalPos;
    private Quaternion _curCarBodyLocalRot;
    private Vector3 _prevCarBodyLocalPos;
    private Quaternion _prevCarBodyLocalRot;
    private float _suspensionRotBlendFactor;
    private bool _resetSuspensionFlag;
    private float _currentWheelSteerAngle;
    private float _currentWheelRollAngle;

    // ========================================================================
    //  LIFECYCLE
    // ========================================================================

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        NetworkRigidbody = GetComponent<NetworkRigidbody>();
        _collider = GetComponent<BoxCollider>();
        _envLayerMask = 1 << LayerMask.NameToLayer("Env");
    }

    public override void NetworkStart()
    {
        base.NetworkStart();
        _gm = Sandbox.GetComponent<GlobalData>().GameMode;

        // Configure Rigidbody to match RL
        Rigidbody.mass = RLC.CAR_MASS_BT;
        Rigidbody.drag = 0f;
        Rigidbody.angularDrag = 0f;
        Rigidbody.useGravity = false;
        Rigidbody.interpolation = RigidbodyInterpolation.None; // Netick handles interpolation
        if (CenterOfMass != null)
            Rigidbody.centerOfMass = CenterOfMass.localPosition;

        Boost = RLC.BOOST_SPAWN_AMOUNT;

        InitWheels();

        if (IsReplay)
            Sandbox.Replay.Playback.OnSeeked += OnReplaySeeked;
    }

    public override void NetworkDestroy()
    {
        if (IsReplay)
            Sandbox.Replay.Playback.OnSeeked -= OnReplaySeeked;
    }

    private void InitWheels()
    {
        _maxSusTravel = RLC.MAX_SUSPENSION_TRAVEL * S;
        var cfg = RLC.OCTANE; // TODO: Make configurable

        _ws = new WheelPhysState[4];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            bool left = (i % 2) == 1;
            var pair = front ? cfg.frontWheels : cfg.backWheels;

            Vector3 offsetRL = pair.connectionOffset;
            if (left) offsetRL.y *= -1f; 

            _ws[i].connectionLocal = RLC.RLToUnity(offsetRL) * S;
            _ws[i].isFront = front;
            _ws[i].radius = pair.wheelRadius * S;
            _ws[i].restLength = (pair.suspensionRestLength - RLC.MAX_SUSPENSION_TRAVEL) * S;
            _ws[i].forceScale = front ? RLC.SUSPENSION_FORCE_SCALE_FRONT : RLC.SUSPENSION_FORCE_SCALE_BACK;
        }
    }

    // ========================================================================
    //  MAIN SIMULATION LOOP
    // ========================================================================

    public override void NetworkFixedUpdate()
    {
        if (_gm != null && _gm.DisableCarSimulation) return;

        float dt = Sandbox.FixedDeltaTime;

        if (FetchInput(out GameInput fetchedInput))
            LastInput = fetchedInput;
        if (_gm != null && _gm.DisableInputForAll)
            LastInput = default;

        var input = LastInput;
        input.Clamp();

        if (IsProxy)
            DecayInput(ref input);

        SimulateVehicle(input, dt);

        if (IsProxy)
        {
            if (!Rigidbody.isKinematic)
                DecayVelocity();
        }

        _lastControls = input;

        // --- Visual suspension ---
        if (!UnityEngine.Application.isBatchMode && !IsResimulating)
            AnimateSuspension(dt);
    }

    private void SimulateVehicle(GameInput input, float dt)
    {
        _worldContactHasContact = false;

        // ====== VEHICLE SIMULATION STEPS ======

        // Phase 1: Wheel raycasts & contact detection
        int numInContact = PerformWheelRaycasts();
        GroundedWheelsNum = numInContact;
        IsOnGround = numInContact >= 3;

        // Phase 2: Driving -- throttle, brake, steering, friction curves, sticky forces
        float forwardSpeed_UU = GetForwardSpeedUU();
        UpdateDriving(input, dt, numInContact, forwardSpeed_UU);

        // Phase 3: Air/flip mechanics
        bool jumpPressed = input.Jump && !_lastControls.Jump;

        if (numInContact < 3)
        {

            if (EnableAirControl)
                UpdateAirTorque(input, dt, numInContact == 0);

        }
        else
        {
            // Grounded (3+ wheels) — stop any active flip
            IsFlipping = false;
        }

        if (EnableJump)
        {
            UpdateJump(input, dt, jumpPressed);
            UpdateAutoFlip(dt, jumpPressed);
            UpdateDoubleJumpOrFlip(input, dt, jumpPressed, forwardSpeed_UU);
        }

        if (EnableAutoStabilization && input.Throttle != 0)
        {
            if ((numInContact > 0 && numInContact < 4) || _worldContactHasContact)
                UpdateAutoRoll(dt, numInContact);
        }


        // Phase 4: Apply suspension & friction forces
        ApplySuspensionForces(dt);
        ApplyWheelFrictionImpulses(dt);

        // Phase 5: Boost
        if (EnableBoost)
            UpdateBoost(input, dt, forwardSpeed_UU);

        // Phase 6: Gravity
        Rigidbody.AddForce(Vector3.down * Mathf.Abs(RLC.GRAVITY_Z) * S, ForceMode.Acceleration);

        // Phase 7: Air throttle (small forward accel when airborne with throttle)
        if (input.Throttle != 0 && !IsOnGround)
            Rigidbody.AddForce(transform.forward * input.Throttle * RLC.THROTTLE_AIR_ACCEL * S, ForceMode.Acceleration);

        // Phase 8: Velocity limits + state
        ClampVelocities();
        UpdateSupersonic(dt);
    }

    // ========================================================================
    //  SUSPENSION & WHEEL RAYCASTS
    // ========================================================================

    private int PerformWheelRaycasts()
    {
        int numInContact = 0;
        float susTravel = _maxSusTravel;

        for (int i = 0; i < 4; i++)
        {
            ref var w = ref _ws[i];

            // Compute world-space hard point (where suspension ray starts)
            w.hardPointWS = transform.TransformPoint(w.connectionLocal);
            Vector3 rayDir = -transform.up; 

            // Total ray length: restLength + travel + radius (matching Bullet's rayCast)
            float rayLen = w.restLength + susTravel + w.radius - (RLC.SUSPENSION_SUBTRACTION * S);

            bool hit = Sandbox.Physics.Raycast(w.hardPointWS, rayDir, out RaycastHit hitInfo, rayLen, _envLayerMask);

            w.inContact = hit;
            w.inContactWithWorld = hit; // Simplified -- in RocketSim this checks if it's a static object

            if (hit)
            {
                numInContact++;
                w.contactPoint = hitInfo.point;
                w.contactNormal = hitInfo.normal;

                // Suspension length = distance along up axis from hard point to contact, minus wheel radius
                float traceLen = Vector3.Dot(w.hardPointWS - w.contactPoint, transform.up);
                w.suspensionLength = traceLen - w.radius;

                // Clamp suspension length
                w.suspensionLength = Mathf.Clamp(
                    w.suspensionLength,
                    w.restLength - susTravel,
                    w.restLength + susTravel
                );

                // Contact alignment factor
                float denom = Vector3.Dot(w.contactNormal, transform.up);
                if (denom > 0.1f)
                {
                    w.invContactDotSus = 1f / denom;
                    Vector3 relPos = w.contactPoint - Rigidbody.worldCenterOfMass;
                    Vector3 velAtContact = Rigidbody.GetPointVelocity(w.contactPoint);
                    float projVel = Vector3.Dot(w.contactNormal, velAtContact);
                    w.suspensionRelVel = projVel * w.invContactDotSus;
                }
                else
                {
                    w.suspensionRelVel = 0f;
                    w.invContactDotSus = 10f;
                }
            }
            else
            {
                w.suspensionLength = w.restLength + susTravel;
                w.suspensionRelVel = 0f;
                w.contactNormal = transform.up;
                w.invContactDotSus = 1f;
                w.suspensionForce = 0f;
            }
        }

        return numInContact;
    }

    private void ApplySuspensionForces(float dt)
    {
        // Phase 1: Calculate suspension forces
        for (int i = 0; i < 4; i++)
        {
            ref var w = ref _ws[i];
            if (!w.inContact) { w.suspensionForce = 0; continue; }
   
            float springForce = (w.restLength - w.suspensionLength) * RLC.SUSPENSION_STIFFNESS * w.invContactDotSus;

            float dampScale = (w.suspensionRelVel < 0)
                ? RLC.WHEELS_DAMPING_COMPRESSION
                : RLC.WHEELS_DAMPING_RELAXATION;

            w.suspensionForce = springForce - (dampScale * w.suspensionRelVel);
            w.suspensionForce *= w.forceScale;


            if (w.suspensionForce < 0) w.suspensionForce = 0;
        }

        // Phase 2: Apply as impulses at vehicles center of mass
        for (int i = 0; i < 4; i++)
        {
            ref var w = ref _ws[i];
            if (w.suspensionForce <= 0) continue;

            Vector3 contactOffset = w.contactPoint - Rigidbody.worldCenterOfMass;
            Vector3 impulse = w.contactNormal * w.suspensionForce * dt;
            Rigidbody.AddForceAtPosition(impulse, w.contactPoint, ForceMode.Impulse);
        }
    }

    // ========================================================================
    //  DRIVING -- THROTTLE, BRAKE, STEERING, FRICTION
    // ========================================================================

    private void UpdateDriving(GameInput input, float dt, int numInContact, float forwardSpeed_UU)
    {
        float absSpeed = Mathf.Abs(forwardSpeed_UU);

        // Handbrake
        if (input.Handbrake)
            HandbrakeVal = Mathf.Min(HandbrakeVal + RLC.POWERSLIDE_RISE_RATE * dt, 1f);
        else
            HandbrakeVal = Mathf.Max(HandbrakeVal - RLC.POWERSLIDE_FALL_RATE * dt, 0f);

        // Throttle/Brake logic
        float realThrottle = input.Throttle;
        float realBrake = 0f;
        if (input.Boost && Boost > 0) realThrottle = 1f;

        float engineThrottle = realThrottle;

        if (!input.Handbrake)
        {
            float absThrottle = Mathf.Abs(realThrottle);
            if (absThrottle >= RLC.THROTTLE_DEADZONE)
            {
                if (absSpeed > RLC.STOPPING_FORWARD_VEL && Mathf.Sign(realThrottle) != Mathf.Sign(forwardSpeed_UU))
                {
                    realBrake = 1f;
                    if (absSpeed > RLC.BRAKING_NO_THROTTLE_SPEED_THRESH)
                        engineThrottle = 0f;
                }
            }
            else
            {
                engineThrottle = 0f;
                realBrake = (absSpeed < RLC.STOPPING_FORWARD_VEL) ? 1f : RLC.COASTING_BRAKE_FACTOR;
            }
        }

        // Drive torque scales down with speed
        float driveSpeedScale = RLC.DRIVE_SPEED_TORQUE_FACTOR.Evaluate(absSpeed);
        if (numInContact < 3) driveSpeedScale /= 4f;


        float engineAccel = engineThrottle * 400f * driveSpeedScale * S; 
        float brakeImpulse = realBrake * (14.25f + 1f / 3f) * S;           

        // Steering
        float steerAngle = RLC.STEER_ANGLE_FROM_SPEED.Evaluate(absSpeed);
        if (HandbrakeVal > 0)
        {
            float psAngle = RLC.POWERSLIDE_STEER_ANGLE_FROM_SPEED.Evaluate(absSpeed);
            steerAngle += (psAngle - steerAngle) * HandbrakeVal;
        }
        steerAngle *= input.Steer;

        // Assign per-wheel driving params
        for (int i = 0; i < 4; i++)
        {
            _ws[i].engineForce = engineAccel;
            _ws[i].brake = brakeImpulse;
            _ws[i].steerAngle = _ws[i].isFront ? steerAngle : 0f;
        }

        UpdateFrictionCurves(input, realThrottle, forwardSpeed_UU);

        // Sticky Forces 
        bool anyWorldContact = false;
        for (int i = 0; i < 4; i++) anyWorldContact |= _ws[i].inContactWithWorld;

        if (anyWorldContact)
        {
            Vector3 upDir = GetUpwardsDirFromWheelContacts();
            bool fullStick = (realThrottle != 0) || (absSpeed > RLC.STOPPING_FORWARD_VEL);
            float stickyScale = 0.5f;
            if (fullStick) stickyScale += 1f - Mathf.Abs(upDir.y); 

            // Sticky force counteracts gravity along the surface normal
            Rigidbody.AddForce(upDir * stickyScale * Mathf.Abs(RLC.GRAVITY_Z) * S, ForceMode.Acceleration);
        }
    }

    private void UpdateFrictionCurves(GameInput input, float realThrottle, float forwardSpeed_UU)
    {
        for (int i = 0; i < 4; i++)
        {
            ref var w = ref _ws[i];
            if (!w.inContact) continue;

            // Compute steering-rotated axle direction (local right, rotated by steer angle)
            Quaternion steerRot = Quaternion.AngleAxis(w.steerAngle * Mathf.Rad2Deg, transform.up);
            Vector3 axleDir = steerRot * transform.right;
            Vector3 surfNorm = w.contactNormal;

            // Project axle onto surface plane
            axleDir = (axleDir - surfNorm * Vector3.Dot(axleDir, surfNorm)).normalized;
            Vector3 fwdDir = Vector3.Cross(surfNorm, axleDir).normalized;

            // Velocity at wheel contact
            Vector3 wheelDelta = w.hardPointWS - Rigidbody.worldCenterOfMass;
            Vector3 crossVel = (Vector3.Cross(Rigidbody.angularVelocity, wheelDelta) + Rigidbody.velocity) / S; // Back to UU/s

            float baseFriction = Mathf.Abs(Vector3.Dot(crossVel, axleDir));
            float frictionInput = 0f;
            if (baseFriction > 5f)
                frictionInput = baseFriction / (Mathf.Abs(Vector3.Dot(crossVel, fwdDir)) + baseFriction);

            float latF = RLC.LAT_FRICTION.Evaluate(frictionInput);
            float longF = RLC.LONG_FRICTION.Evaluate(frictionInput);

            if (HandbrakeVal > 0)
            {
                latF *= (RLC.HANDBRAKE_LAT_FRICTION_FACTOR.Evaluate(frictionInput) - 1f) * HandbrakeVal + 1f;
                longF *= (RLC.HANDBRAKE_LONG_FRICTION_FACTOR.Evaluate(frictionInput) - 1f) * HandbrakeVal + 1f;
            }
            else
            {
                longF = 1f; // No longitudinal friction scaling when not powersliding
            }

            // Non-sticky friction scaling when coasting
            bool isSticky = realThrottle != 0;
            if (!isSticky)
            {
                float nsFactor = RLC.NON_STICKY_FRICTION_FACTOR.Evaluate(w.contactNormal.y); // .y = up component
                latF *= nsFactor;
                longF *= nsFactor;
            }

            w.latFriction = latF;
            w.longFriction = longF;
        }
    }

    private void ApplyWheelFrictionImpulses(float dt)
    {
        float frictionScale = Rigidbody.mass / 3f;

        for (int i = 0; i < 4; i++)
        {
            ref var w = ref _ws[i];
            if (!w.inContact) { w.frictionImpulse = Vector3.zero; continue; }

            // Compute steered axle & forward directions on the contact surface
            Quaternion steerRot = Quaternion.AngleAxis(w.steerAngle * Mathf.Rad2Deg, transform.up);
            Vector3 axleDir = steerRot * transform.right;
            Vector3 surfNorm = w.contactNormal;
            axleDir = (axleDir - surfNorm * Vector3.Dot(axleDir, surfNorm)).normalized;
            Vector3 fwdDir = Vector3.Cross(surfNorm, axleDir).normalized;

            // Lateral impulse
            Vector3 velAtContact = Rigidbody.GetPointVelocity(w.contactPoint);
            float lateralVel = Vector3.Dot(velAtContact, axleDir);
            float sideImpulse = -lateralVel * Rigidbody.mass;

            // --- Longitudinal impulse (engine or brake) ---
            float rollingFriction;
            if (w.engineForce == 0f)
            {
                if (w.brake > 0)
                {
                    float relVel = Vector3.Dot(velAtContact, fwdDir);
                    rollingFriction = Mathf.Clamp(-relVel * 113.74f, -w.brake * Rigidbody.mass, w.brake * Rigidbody.mass);
                }
                else
                {
                    rollingFriction = 0f;
                }
            }
            else
            {
                rollingFriction = -w.engineForce * Rigidbody.mass / frictionScale;
            }

            Vector3 totalFriction = (fwdDir * rollingFriction * w.longFriction)
                                  + (axleDir * sideImpulse * w.latFriction);
            w.frictionImpulse = totalFriction * frictionScale;

            // Apply at vehicles center of mass height
            if (!w.frictionImpulse.Equals(Vector3.zero))
            {
                Vector3 contactOffset = w.contactPoint - Rigidbody.worldCenterOfMass;
                float upDot = Vector3.Dot(transform.up, contactOffset);
                Vector3 wheelRelPos = contactOffset - transform.up * upDot;

                Rigidbody.AddForceAtPosition(w.frictionImpulse * dt, Rigidbody.worldCenterOfMass + wheelRelPos, ForceMode.Impulse);
            }
        }
    }

    // ========================================================================
    //  JUMP
    // ========================================================================

    private void UpdateJump(GameInput input, float dt, bool jumpPressed)
    {
        if (IsOnGround && !IsJumping)
        {
            if (HasJumped && JumpTime < RLC.JUMP_MIN_TIME + RLC.JUMP_RESET_TIME_PAD)
            {
                // Prevents false reset before leaving ground on short jumps
            }
            else
            {
                HasJumped = false;
                JumpTime = 0f;
            }
        }

        if (IsJumping)
        {
            if (JumpTime < RLC.JUMP_MIN_TIME || (input.Jump && JumpTime < RLC.JUMP_MAX_TIME))
                IsJumping = true;
            else
                IsJumping = false;
        }
        else if (IsOnGround && jumpPressed)
        {
            IsJumping = true;
            JumpTime = 0f;
            Rigidbody.AddForce(transform.up * RLC.JUMP_IMMEDIATE_FORCE * S, ForceMode.VelocityChange);
            JumpTrigger++;
        }

        if (IsJumping)
        {
            HasJumped = true;
            float accel = RLC.JUMP_ACCEL * S;
            if (JumpTime < RLC.JUMP_MIN_TIME)
                accel *= RLC.JUMP_PRE_MIN_ACCEL_SCALE;
            Rigidbody.AddForce(transform.up * accel, ForceMode.Acceleration);
        }

        if (IsJumping || HasJumped)
            JumpTime += dt;
    }

    // ========================================================================
    //  DOUBLE JUMP / FLIP (DODGE)
    // ========================================================================

    private void UpdateDoubleJumpOrFlip(GameInput input, float dt, bool jumpPressed, float forwardSpeed_UU)
    {
        if (IsOnGround)
        {
            // Kill residual roll spin on landing from a flip
            if (HasFlipped)
            {
                Vector3 angVel = Rigidbody.angularVelocity;
                float rollComponent = Vector3.Dot(angVel, transform.forward);
                Rigidbody.angularVelocity = angVel - transform.forward * rollComponent * 0.9f;
            }

            HasDoubleJumped = false;
            HasFlipped = false;
            AirTime = 0f;
            AirTimeSinceJump = 0f;
            FlipTime = 0f;
            return;
        }

        AirTime += dt;

        if (HasJumped && !IsJumping)
            AirTimeSinceJump += dt;
        else
            AirTimeSinceJump = 0f;

        // Attempt dodge or double jump
        if (jumpPressed && AirTimeSinceJump < RLC.DOUBLEJUMP_MAX_DELAY && !IsProxy)
        {
            float inputMag = Mathf.Abs(input.Yaw) + Mathf.Abs(input.Pitch) + Mathf.Abs(input.Roll);
            bool isFlipInput = inputMag >= RLC.OCTANE.dodgeDeadzone;

            bool canUse = !HasDoubleJumped && !HasFlipped;
            if (IsAutoFlipping) canUse = false;

            if (canUse)
            {
                if (isFlipInput)
                {
                    // === FLIP / DODGE ===
                    FlipTime = 0f;
                    HasFlipped = true;
                    IsFlipping = true;

                    float forwardSpeedRatio = Mathf.Abs(forwardSpeed_UU) / RLC.CAR_MAX_SPEED;

                    // Dodge direction
                    Vector3 dodgeDir_rl = new Vector3(input.Pitch, input.Yaw + input.Roll, 0f);
                    if (Mathf.Abs(input.Yaw + input.Roll) < 0.1f && Mathf.Abs(input.Pitch) < 0.1f)
                        dodgeDir_rl = Vector3.zero;
                    else
                        dodgeDir_rl = dodgeDir_rl.normalized;

                    // Flip torque direction
                    FlipRelTorque_RL = new Vector3(-dodgeDir_rl.y, dodgeDir_rl.x, 0f);

                    // Initial dodge velocity
                    if (Mathf.Abs(dodgeDir_rl.x) < 0.1f) dodgeDir_rl.x = 0f;
                    if (Mathf.Abs(dodgeDir_rl.y) < 0.1f) dodgeDir_rl.y = 0f;

                    if (dodgeDir_rl != Vector3.zero)
                    {
                        bool shouldDodgeBackward;
                        if (Mathf.Abs(forwardSpeed_UU) < 100f)
                            shouldDodgeBackward = dodgeDir_rl.x < 0f;
                        else
                            shouldDodgeBackward = (dodgeDir_rl.x >= 0f) != (forwardSpeed_UU >= 0f);

                        Vector3 initDodgeVel = dodgeDir_rl * RLC.FLIP_INITIAL_VEL_SCALE;

                        float maxScaleX = shouldDodgeBackward
                            ? RLC.FLIP_BACKWARD_IMPULSE_MAX_SPEED_SCALE
                            : RLC.FLIP_FORWARD_IMPULSE_MAX_SPEED_SCALE;

                        initDodgeVel.x *= ((maxScaleX - 1f) * forwardSpeedRatio) + 1f;
                        initDodgeVel.y *= ((RLC.FLIP_SIDE_IMPULSE_MAX_SPEED_SCALE - 1f) * forwardSpeedRatio) + 1f;

                        if (shouldDodgeBackward)
                            initDodgeVel.x *= RLC.FLIP_BACKWARD_IMPULSE_SCALE_X;

                        // Convert to world-space velocity
                        Vector3 fwd2D = transform.forward; fwd2D.y = 0; fwd2D.Normalize();
                        Vector3 right2D = new Vector3(fwd2D.z, 0f, -fwd2D.x);

                        Vector3 finalDV = (initDodgeVel.x * fwd2D + initDodgeVel.y * right2D) * S;
                        Rigidbody.AddForce(finalDV, ForceMode.VelocityChange);
                    }

                    JumpTrigger++;
                }
                else
                {
                    // === DOUBLE JUMP (straight up) ===
                    Rigidbody.AddForce(transform.up * RLC.JUMP_IMMEDIATE_FORCE * S, ForceMode.VelocityChange);
                    HasDoubleJumped = true;
                    JumpTrigger++;
                }
            }
        }

        // Active flip torque & Z-damping

        if (IsFlipping)
        {
            FlipTime += dt;
            if (FlipTime > RLC.FLIP_TORQUE_TIME)
                IsFlipping = false;


            if (FlipTime >= RLC.FLIP_Z_DAMP_START && (Rigidbody.velocity.y < 0 || FlipTime < RLC.FLIP_Z_DAMP_END))
            {
                Vector3 vel = Rigidbody.velocity;
                vel.y *= Mathf.Pow(1f - RLC.FLIP_Z_DAMP_120, dt / (1f / 120f));
                Rigidbody.velocity = vel;
            }
        }
        else if (HasFlipped)
        {
            FlipTime += dt;
        }
    }

    // ========================================================================
    //  AIR CONTROL & FLIP TORQUE
    // ========================================================================

    private void UpdateAirTorque(GameInput input, float dt, bool fullyAirborne)
    {
        // Flip torque

        if (IsFlipping)
        {
            IsFlipping = HasFlipped && FlipTime < RLC.FLIP_TORQUE_TIME;
        }

        bool doAirControl = false;

        if (IsFlipping)
        {
            Vector3 relTorque = FlipRelTorque_RL;
            if (relTorque != Vector3.zero)
            {
                // Flip cancel
                float pitchScale = 1f;
                if (relTorque.y != 0 && input.Pitch != 0)
                {
                    if (Mathf.Sign(relTorque.y) != Mathf.Sign(input.Pitch))
                    {
                        pitchScale = 1f - Mathf.Min(Mathf.Abs(input.Pitch), 1f);
                        doAirControl = true;
                    }
                }
                relTorque.y *= pitchScale;

                // Apply flip torque in RL local space, then convert to world
                // RL local: (X * FLIP_TORQUE_X, Y * FLIP_TORQUE_Y, 0)
                // X in RL = forward axis -> roll-like torque
                // Y in RL = right axis -> pitch-like torque

                Vector3 flipTorque_rl = new Vector3(
                    relTorque.x * RLC.FLIP_TORQUE_X,
                    relTorque.y * RLC.FLIP_TORQUE_Y,
                    0f
                );

                Vector3 flipTorque_unity = RLC.RLToUnity(flipTorque_rl);

                Rigidbody.angularVelocity += transform.TransformDirection(flipTorque_unity) * dt;
            }
            else
            {
                doAirControl = true;
            }
        }
        else
        {
            doAirControl = true;
        }

        doAirControl &= !IsAutoFlipping;
        doAirControl &= fullyAirborne;

        if (doAirControl)
        {

            float pitchTorqueScale = 1f;
            if (input.Pitch != 0 || input.Yaw != 0 || input.Roll != 0)
            {
                if (IsFlipping)
                {
                    pitchTorqueScale = 0f;
                }
                else if (HasFlipped && FlipTime < RLC.FLIP_TORQUE_TIME + RLC.FLIP_PITCHLOCK_EXTRA_TIME)
                {
                    pitchTorqueScale = 0f;
                }

                // Air torque in world-aligned local axes:

                Vector3 torque =
                    (transform.right * input.Pitch * pitchTorqueScale * RLC.CAR_AIR_CONTROL_TORQUE.x) +
                    (transform.up * input.Yaw * RLC.CAR_AIR_CONTROL_TORQUE.y) +
                    (-transform.forward * input.Roll * RLC.CAR_AIR_CONTROL_TORQUE.z);

                // Damping: opposes current angular velocity along each axis
                Vector3 angVel = Rigidbody.angularVelocity;
                float dampPitch = Vector3.Dot(transform.right, angVel) * RLC.CAR_AIR_CONTROL_DAMPING.x
                                  * (1f - Mathf.Abs(input.Pitch * pitchTorqueScale));
                float dampYaw = Vector3.Dot(transform.up, angVel) * RLC.CAR_AIR_CONTROL_DAMPING.y
                                  * (1f - Mathf.Abs(input.Yaw));
                float dampRoll = Vector3.Dot(-transform.forward, angVel) * RLC.CAR_AIR_CONTROL_DAMPING.z;

                Vector3 damping =
                    (transform.right * dampPitch) +
                    (transform.up * dampYaw) +
                    (-transform.forward * dampRoll);

                // Direct angular velocity modification (inertia-bypassing, matching RocketSim)
                Rigidbody.angularVelocity += (torque - damping) * RLC.CAR_TORQUE_SCALE * dt;
            }
            else
            {
                // No input -- still apply damping
                Vector3 angVel = Rigidbody.angularVelocity;
                float dampPitch = Vector3.Dot(-transform.right, angVel) * RLC.CAR_AIR_CONTROL_DAMPING.x;
                float dampYaw = Vector3.Dot(transform.up, angVel) * RLC.CAR_AIR_CONTROL_DAMPING.y;
                float dampRoll = Vector3.Dot(-transform.forward, angVel) * RLC.CAR_AIR_CONTROL_DAMPING.z;

                Vector3 damping =
                    (-transform.right * dampPitch) +
                    (transform.up * dampYaw) +
                    (-transform.forward * dampRoll);

                Rigidbody.angularVelocity -= damping * RLC.CAR_TORQUE_SCALE * dt;
            }
        }
    }

    // ========================================================================
    //  AUTO FLIP (TURTLE RECOVERY)
    // ========================================================================

    private void UpdateAutoFlip(float dt, bool jumpPressed)
    {
        if (jumpPressed && CheckWorldContact(out Vector3 autoFlipNormal) && autoFlipNormal.y > RLC.CAR_AUTOFLIP_NORMZ_THRESH)
        {
            // Check if car is heavily rolled
            float rollAngle = Mathf.Atan2(
                Vector3.Dot(Vector3.up, transform.right),
                Vector3.Dot(Vector3.up, transform.up)
            );
            float absRoll = Mathf.Abs(rollAngle);
            if (absRoll > RLC.CAR_AUTOFLIP_ROLL_THRESH)
            {
                AutoFlipTimer = RLC.CAR_AUTOFLIP_TIME * (absRoll / Mathf.PI);
                AutoFlipTorqueScale = (rollAngle > 0) ? 1f : -1f;
                IsAutoFlipping = true;

                Rigidbody.AddForce(-transform.up * RLC.CAR_AUTOFLIP_IMPULSE * S, ForceMode.VelocityChange);
            }
        }

        if (IsAutoFlipping)
        {
            if (AutoFlipTimer <= 0f)
            {
                IsAutoFlipping = false;
                AutoFlipTimer = 0f;
            }
            else
            {
                Rigidbody.angularVelocity += transform.forward * RLC.CAR_AUTOFLIP_TORQUE * AutoFlipTorqueScale * dt;
                AutoFlipTimer -= dt;
            }
        }
    }

    // ========================================================================
    //  AUTO ROLL (Partial ground contact stability)
    // ========================================================================

    private void UpdateAutoRoll(float dt, int numWheelsInContact)
    {
        Vector3 groundUpDir;
        if (numWheelsInContact > 0)
            groundUpDir = GetUpwardsDirFromWheelContacts();
        else
            groundUpDir = _worldContactNormal;

        Vector3 groundDownDir = -groundUpDir;
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        Vector3 crossRight = Vector3.Cross(groundUpDir, fwd);
        Vector3 crossForward = Vector3.Cross(groundDownDir, crossRight);

        float rightTorqueFactor = 1f - Mathf.Clamp01(Vector3.Dot(right, crossRight));
        float forwardTorqueFactor = 1f - Mathf.Clamp01(Vector3.Dot(fwd, crossForward));

        Vector3 torqueDirRight = fwd * (Vector3.Dot(right, groundUpDir) >= 0 ? -1f : 1f);
        Vector3 torqueDirForward = right * (Vector3.Dot(fwd, groundUpDir) >= 0 ? 1f : -1f);

        Vector3 torqueRight = torqueDirRight * rightTorqueFactor;
        Vector3 torqueForward = torqueDirForward * forwardTorqueFactor;

        Rigidbody.AddForce(groundDownDir * RLC.CAR_AUTOROLL_FORCE * S, ForceMode.Acceleration);

        // Apply auto-roll torque (inertia-bypassing, like RocketSim)

        Rigidbody.angularVelocity += (torqueForward + torqueRight) * RLC.CAR_AUTOROLL_TORQUE * dt;
    }

    // ========================================================================
    //  BOOST
    // ========================================================================

    private void UpdateBoost(GameInput input, float dt, float forwardSpeed_UU)
    {
        bool hasBoost = Boost > 0;

        if (hasBoost)
        {
            if (IsBoosting)
                IsBoosting = input.Boost || BoostingTime < RLC.BOOST_MIN_TIME;
            else
                IsBoosting = input.Boost;
        }
        else
        {
            IsBoosting = false;
        }

        BoostingTime = IsBoosting ? BoostingTime + dt : 0f;

        if (IsBoosting)
        {
            Boost = Mathf.Max(Boost - RLC.BOOST_USED_PER_SECOND * dt, 0f);
            float accel = (IsOnGround ? RLC.BOOST_ACCEL_GROUND : RLC.BOOST_ACCEL_AIR) * S;
            Rigidbody.AddForce(transform.forward * accel, ForceMode.Acceleration);
        }

        Boost = Mathf.Min(Boost, RLC.BOOST_MAX);
    }

    public void ReceiveFuel()
    {
        Boost = Mathf.Min(RLC.BOOST_MAX, Boost + RLC.BOOST_SPAWN_AMOUNT);
    }

    // ========================================================================
    //  VELOCITY MANAGEMENT
    // ========================================================================

    private void ClampVelocities()
    {
        if (Rigidbody.isKinematic) return;

        float maxSpeed = RLC.CAR_MAX_SPEED * S;
        float maxAngSpeed = RLC.CAR_MAX_ANG_SPEED;

        Vector3 vel = Rigidbody.velocity;
        if (vel.sqrMagnitude > maxSpeed * maxSpeed)
            Rigidbody.velocity = vel.normalized * maxSpeed;

        if (!IsFlipping)
        {
            Vector3 angVel = Rigidbody.angularVelocity;
            if (angVel.sqrMagnitude > maxAngSpeed * maxAngSpeed)
                Rigidbody.angularVelocity = angVel.normalized * maxAngSpeed;
        }
    }

    private void UpdateSupersonic(float dt)
    {
        float speedSq = (Rigidbody.velocity / S).sqrMagnitude; // In UU/s squared

        if (IsSupersonic && SupersonicTime < RLC.SUPERSONIC_MAINTAIN_MAX_TIME)
            IsSupersonic = speedSq >= RLC.SUPERSONIC_MAINTAIN_MIN_SPEED * RLC.SUPERSONIC_MAINTAIN_MIN_SPEED;
        else
            IsSupersonic = speedSq >= RLC.SUPERSONIC_START_SPEED * RLC.SUPERSONIC_START_SPEED;

        SupersonicTime = IsSupersonic ? SupersonicTime + dt : 0f;
    }

    // ========================================================================
    //  HELPERS
    // ========================================================================

    private float GetForwardSpeedUU()
    {
        return Vector3.Dot(Rigidbody.velocity, transform.forward) / S;
    }

    private Vector3 GetUpwardsDirFromWheelContacts()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < 4; i++)
            if (_ws[i].inContact)
                sum += _ws[i].contactNormal;
        return (sum == Vector3.zero) ? transform.up : sum.normalized;
    }

    private bool PerformEnvironmentCheck()
    {
        var cols = new Collider[1];
        return Sandbox.Physics.OverlapBox(transform.position, _collider.size * 0.65f, cols, transform.rotation, _envLayerMask) > 0;
    }

    // === NETWORKING HELPERS

    private void DecayInput(ref GameInput input)
    {
        float mult = Mathf.Max(InputDecayMinFactor,
            Mathf.InverseLerp(InputDecayMaxLatency, 0f,
                Sandbox.TickToTime(Sandbox.Tick - Sandbox.AuthoritativeTick) * 1000));
        input.Throttle *= mult;
        input.Steer *= mult;
        input.Pitch *= mult;
        input.Yaw *= mult;
        input.Roll *= mult;
    }

    private void DecayVelocity()
    {
        float mult = Mathf.InverseLerp(1000f, VelocityDecayMinLatency,
            Sandbox.TickToTime(Sandbox.Tick - Sandbox.AuthoritativeTick) * 1000);
        Rigidbody.velocity *= mult;
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (Object != null && !IsResimulating)
            OnCollisionEnterEvent?.Invoke(collision);

        // Track world normal contact for auto-flip/auto-roll
        if (collision.gameObject.layer == LayerMask.NameToLayer("Env"))
        {
            _worldContactHasContact = true;
            _worldContactNormal = collision.contacts[0].normal;
        }
    }


    public void SetCarActive(bool active)
    {
        Rigidbody.isKinematic = !active;
        if (!active)
            Rigidbody.position = Vector3.one * -1000f;
    }

    private bool CheckWorldContact(out Vector3 normal)
    {
        // Check wheel contacts first
        for (int i = 0; i < 4; i++)
        {
            if (_ws[i].inContactWithWorld)
            {
                normal = _ws[i].contactNormal;
                return true;
            }
        }

        // Fallback: raycast toward world down (works when inverted)
        float checkDist = 2f * S * 50f;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkDist, _envLayerMask))
        {
            normal = hit.normal;
            return true;
        }

        normal = Vector3.up;
        return false;
    }

    public override void ClearState(bool resetBoost = true)
    {
        LastInput = default;
        IsOnGround = false;
        GroundedWheelsNum = 0;
        HasJumped = false;
        IsJumping = false;
        JumpTime = 0f;
        HasFlipped = false;
        HasDoubleJumped = false;
        IsFlipping = false;
        FlipTime = 0f;
        FlipRelTorque_RL = Vector3.zero;
        HandbrakeVal = 0f;
        AirTime = 0f;
        AirTimeSinceJump = 0f;
        IsAutoFlipping = false;
        AutoFlipTimer = 0f;
        IsBoosting = false;
        BoostingTime = 0f;
        IsSupersonic = false;
        SupersonicTime = 0f;
        if (resetBoost) Boost = RLC.BOOST_SPAWN_AMOUNT;
        Rigidbody.velocity = Vector3.zero;
        Rigidbody.angularVelocity = Vector3.zero;
    }

    // ========================================================================
    //  RENDER (VISUAL / AUDIO) -- runs every frame, not every tick
    // ========================================================================

    public override void NetworkRender()
    {
        // Interpolate suspension
        if (CarBody != null)
        {
            CarBody.localPosition = Vector3.Lerp(_prevCarBodyLocalPos, _curCarBodyLocalPos, Sandbox.LocalInterpolation.Alpha);
            CarBody.localRotation = Quaternion.Slerp(_prevCarBodyLocalRot, _curCarBodyLocalRot, Sandbox.LocalInterpolation.Alpha);
        }

        AnimateWheels(Time.deltaTime);

        // Afterburner particles
        for (int i = 0; i < AfterburnerParticleSystems.Length; i++)
        {
            var em = AfterburnerParticleSystems[i].emission;
            em.enabled = IsBoosting;
        }

        // Drift particles
        float fwdSpd = Vector3.Dot(NetworkRigidbody.Velocity, transform.forward);
        float sideSpd = Vector3.Dot(NetworkRigidbody.Velocity, transform.right);
        SideSpeed = sideSpd;
        IsSlipping = IsOnGround
            && Mathf.Abs(sideSpd) > Mathf.Max(0.1f, 0.35f * Mathf.Abs(fwdSpd))
            && NetworkRigidbody.Velocity.magnitude >= DriftMinVelocity;

        for (int i = 0; i < DriftParticleSystems.Length; i++)
        {
            var em = DriftParticleSystems[i].emission;
            em.enabled = IsSlipping;
        }
    }

    private void AnimateWheels(float dt)
    {
        float s = LastInput.Steer >= 0.1f ? 1f : (LastInput.Steer <= -0.1f ? -1f : 0f);
        _currentWheelSteerAngle = Mathf.Lerp(_currentWheelSteerAngle, s * WheelMaxSteerAngle, dt * 20f);

        for (int i = 0; i < Wheels.Length && i < 4; i++)
        {
            var wheel = Wheels[i];
            if (wheel == null || wheel.Render == null) continue;

            float speed = _ws[i].inContact ? Vector3.Dot(Rigidbody.GetPointVelocity(_ws[i].contactPoint),
                transform.forward) / S : 0f;
            wheel.VisualSpeed = Mathf.Lerp(wheel.VisualSpeed, speed, 40f * dt);
            float rollSpeed = Mathf.Clamp(wheel.VisualSpeed, -WheelMaxRollSpeed, WheelMaxRollSpeed);
            _currentWheelRollAngle += rollSpeed * dt * WheelRollSpeedFactor;

            var yawRot = Quaternion.AngleAxis(wheel.IsFront ? _currentWheelSteerAngle : 0, WheelSteerAxis);
            var rollRot = Quaternion.AngleAxis(_currentWheelRollAngle, WheelRollAxis);
            wheel.Render.localRotation = yawRot * rollRot;
        }
    }


    private void AnimateSuspension(float dt)
    {
        if (CarBody == null || NetworkRigidbody == null) return;

        _prevCarBodyLocalPos = _curCarBodyLocalPos;
        _prevCarBodyLocalRot = _curCarBodyLocalRot;

        var rt = NetworkRigidbody.RenderTransform;
        var target = rt.position + ((rt.up + rt.forward * 0.05f) * 1f);

        if (Vector3.Distance(target, _springPos) > 10f || _resetSuspensionFlag)
            _springPos = target;

        _springVelocity = (SpringDampingVisual * _springVelocity) + (SpringStiffnessVisual * (target - _springPos) * dt);
        _springPos += _springVelocity * dt;
        _suspensionRotBlendFactor = Mathf.Lerp(_suspensionRotBlendFactor,
            (GroundedWheelsNum == 0 || _resetSuspensionFlag) ? 0f : 1f, dt * 10f);

        float maxSpd = 0.2f;
        var localVel = Vector3.ClampMagnitude(rt.InverseTransformVector(_springVelocity) * SpringSpeedFactor, maxSpd);
        float pitchA = Mathf.Lerp(-MaxSuspensionPitchAngle, MaxSuspensionPitchAngle, Mathf.InverseLerp(-maxSpd, maxSpd, localVel.z));
        float rollA = Mathf.Lerp(-MaxSuspensionRollAngle, MaxSuspensionRollAngle, Mathf.InverseLerp(-maxSpd, maxSpd, localVel.x));
        var yOff = Mathf.Lerp(-MaxSuspensionCompression, MaxSuspensionCompression, Mathf.InverseLerp(-maxSpd, maxSpd, localVel.y)) * SuspensionCompressionDirection;

        _curCarBodyLocalPos = Vector3.Lerp(default, Vector3.Lerp(CarBody.localPosition, yOff, dt * 20f), _suspensionRotBlendFactor);
        _curCarBodyLocalRot = Quaternion.Lerp(Quaternion.identity,
            Quaternion.AngleAxis(pitchA, SuspensionPitchAxis) * Quaternion.AngleAxis(rollA, SuspensionRollAxis),
            _suspensionRotBlendFactor);

        _resetSuspensionFlag = false;
    }

    private void OnReplaySeeked(Tick before, Tick current)
    {
        _resetSuspensionFlag = true;
    }

    [JetBrains.Annotations.UsedImplicitly]
    [OnChanged(nameof(JumpTrigger))]
    private void OnJumpTriggerChanged(OnChangedData dat)
    {
        if (!dat.IsCatchingUp)
            OnJumpEvent?.Invoke();
    }

    private void OnDrawGizmos()
    {
        if (Sandbox != null && !Sandbox.IsVisible || _collider == null) return;

        Gizmos.matrix = _collider.transform.localToWorldMatrix;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(_collider.center, _collider.size);

        // Draw wheel rays
        if (_ws != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            for (int i = 0; i < 4; i++)
            {
                Gizmos.color = _ws[i].inContact ? Color.green : Color.yellow;
                Vector3 start = transform.TransformPoint(_ws[i].connectionLocal);
                Vector3 end = start - transform.up * (_ws[i].restLength + _maxSusTravel + _ws[i].radius);
                Gizmos.DrawLine(start, end);
                if (_ws[i].inContact)
                    Gizmos.DrawWireSphere(_ws[i].contactPoint, _ws[i].radius * 0.3f);
            }
        }
    }
}