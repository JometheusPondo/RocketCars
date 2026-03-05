using UnityEngine;


/// Piecewise linear curve matching RocketSim's LinearPieceCurve.
/// Input -> Output via linear interpolation between defined points.
/// Extrapolates flat beyond defined range.

[System.Serializable]
public struct PiecewiseCurve
{
    public Vector2[] points; // (input, output) pairs, must be sorted by input ascending

    public PiecewiseCurve(params Vector2[] pts) { points = pts; }

    public float Evaluate(float input)
    {
        if (points == null || points.Length == 0) return 0f;
        if (points.Length == 1) return points[0].y;
        if (input <= points[0].x) return points[0].y;
        if (input >= points[points.Length - 1].x) return points[points.Length - 1].y;

        for (int i = 0; i < points.Length - 1; i++)
        {
            if (input >= points[i].x && input <= points[i + 1].x)
            {
                float t = (input - points[i].x) / (points[i + 1].x - points[i].x);
                return Mathf.Lerp(points[i].y, points[i + 1].y, t);
            }
        }
        return points[points.Length - 1].y;
    }
}


/// Wheel pair configuration ported from RocketSim's WheelPairConfig.

[System.Serializable]
public struct RLWheelPairConfig
{
    public float wheelRadius;          
    public float suspensionRestLength;  

    /// Where the suspension ray starts, in local coords (X=fwd, Y=right, Z=up).

    public Vector3 connectionOffset;    
}


/// Car body configuration ported from RocketSim's CarConfig.


[System.Serializable]
public struct RLCarConfig
{
    public Vector3 hitboxSize;       
    public Vector3 hitboxPosOffset;  
    public RLWheelPairConfig frontWheels;
    public RLWheelPairConfig backWheels;
    public float dodgeDeadzone;      
}


/// All physics constants from RocketSim's RLConst.h and related files.


public static class RLC
{
    // ===================== GENERAL PHYSICS =====================

    public const float GRAVITY_Z = -650f;         
    public const float CAR_MASS_BT = 180f;

    // ===================== BALL =====================
    public const float BALL_RADIUS = 91.25f;   
    public const float BALL_RESTITUTION = 0.6f;
    public const float BALL_FRICTION = 0.285f;
    public const float BALL_FRICTION_Y = 2.0f;     
    public const float BALL_MAX_SPEED = 6000f;    
    public const float BALL_MAX_ANG_SPEED = 6f;       
    public const float BALL_DRAG = 0.0003f; 
    public const float BALL_MASS = 30f;      

    // ===================== SPEED LIMITS =====================

    public const float CAR_MAX_SPEED = 2300f;         
    public const float CAR_MAX_ANG_SPEED = 5.5f;          

    // ===================== SUPERSONIC =====================

    public const float SUPERSONIC_START_SPEED = 2200f;
    public const float SUPERSONIC_MAINTAIN_MIN_SPEED = 2100f;
    public const float SUPERSONIC_MAINTAIN_MAX_TIME = 1f;

    // ===================== THROTTLE / BRAKE =====================

    public const float THROTTLE_TORQUE_AMOUNT = CAR_MASS_BT * 400f;   
    public const float BRAKE_TORQUE_AMOUNT = CAR_MASS_BT * (14.25f + 1f / 3f);
    public const float STOPPING_FORWARD_VEL = 50f;   
    public const float COASTING_BRAKE_FACTOR = 0.15f;
    public const float BRAKING_NO_THROTTLE_SPEED_THRESH = 0.01f;
    public const float THROTTLE_DEADZONE = 0.001f;
    public const float THROTTLE_AIR_ACCEL = 200f / 3f; 

    // ===================== BOOST =====================

    public const float BOOST_MAX = 100f;
    public const float BOOST_USED_PER_SECOND = BOOST_MAX / 3f;
    public const float BOOST_MIN_TIME = 0.1f;
    public const float BOOST_ACCEL_GROUND = 2975f / 3f;  // ~991.67 UU/s^2
    public const float BOOST_ACCEL_AIR = 3175f / 3f;  // ~1058.33 UU/s^2
    public const float BOOST_SPAWN_AMOUNT = BOOST_MAX / 3f;

    // ===================== STEERING =====================

    // Steer angle (radians) from forward speed

    public static readonly PiecewiseCurve STEER_ANGLE_FROM_SPEED = new PiecewiseCurve(
        new Vector2(0, 0.53356f),
        new Vector2(500, 0.31930f),
        new Vector2(1000, 0.18203f),
        new Vector2(1500, 0.10570f),
        new Vector2(1750, 0.08507f),
        new Vector2(3000, 0.03454f)
    );

    // Extended steer angle during powerslide

    public static readonly PiecewiseCurve POWERSLIDE_STEER_ANGLE_FROM_SPEED = new PiecewiseCurve(
        new Vector2(0, 0.39235f),
        new Vector2(2500, 0.12610f)
    );

    // ===================== POWERSLIDE =====================

    public const float POWERSLIDE_RISE_RATE = 5f;
    public const float POWERSLIDE_FALL_RATE = 2f;

    // ===================== DRIVE TORQUE =====================

    // Torque factor from speed -- drops to 0 at 1410 UU/s
    public static readonly PiecewiseCurve DRIVE_SPEED_TORQUE_FACTOR = new PiecewiseCurve(
        new Vector2(0, 1.0f),
        new Vector2(1400, 0.1f),
        new Vector2(1410, 0.0f)
    );

    // ===================== FRICTION CURVES =====================

    // Lateral friction from slip ratio (1.0 at no slip -> 0.2 at full slip)
    public static readonly PiecewiseCurve LAT_FRICTION = new PiecewiseCurve(
        new Vector2(0, 1.0f),
        new Vector2(1, 0.2f)
    );

    // Longitudinal friction (empty curve = always 1.0 unless powersliding)
    public static readonly PiecewiseCurve LONG_FRICTION = new PiecewiseCurve(
        new Vector2(0, 1.0f) // Effectively constant 1.0
    );

    // Powerslide lateral friction factor
    public static readonly PiecewiseCurve HANDBRAKE_LAT_FRICTION_FACTOR = new PiecewiseCurve(
        new Vector2(0, 0.1f)
    );

    // Powerslide longitudinal friction factor
    public static readonly PiecewiseCurve HANDBRAKE_LONG_FRICTION_FACTOR = new PiecewiseCurve(
        new Vector2(0, 0.5f),
        new Vector2(1, 0.9f)
    );

    // Non-sticky friction factor (scales friction when coasting on angled surfaces)
    public static readonly PiecewiseCurve NON_STICKY_FRICTION_FACTOR = new PiecewiseCurve(
        new Vector2(0f, 0.1f),
        new Vector2(0.7075f, 0.5f),
        new Vector2(1f, 1.0f)
    );

    // ===================== SUSPENSION =====================

    public const float SUSPENSION_STIFFNESS = 500f;
    public const float WHEELS_DAMPING_COMPRESSION = 25f;
    public const float WHEELS_DAMPING_RELAXATION = 40f;
    public const float MAX_SUSPENSION_TRAVEL = 22f;     
    public const float SUSPENSION_SUBTRACTION = 0.05f;   
    public const float SUSPENSION_FORCE_SCALE_FRONT = 36f - 0.25f;      
    public const float SUSPENSION_FORCE_SCALE_BACK = 54f + 0.5f + 0.015f; 

    // ===================== JUMP =====================

    public const float JUMP_IMMEDIATE_FORCE = 875f / 3f;   // ~291.67 UU/s 
    public const float JUMP_ACCEL = 4375f / 3f;   // ~1458.33 UU/s^2 
    public const float JUMP_MIN_TIME = 0.025f;
    public const float JUMP_MAX_TIME = 0.2f;
    public const float JUMP_RESET_TIME_PAD = 1f / 40f;
    public const float JUMP_PRE_MIN_ACCEL_SCALE = 0.62f;

    // ===================== DOUBLE JUMP / FLIP (DODGE) =====================

    public const float DOUBLEJUMP_MAX_DELAY = 1.25f;  
    public const float FLIP_INITIAL_VEL_SCALE = 500f;   
    public const float FLIP_TORQUE_TIME = 0.7f;  
    public const float FLIP_TORQUE_X = 180f;   
    public const float FLIP_TORQUE_Y = 224f;   
    public const float FLIP_Z_DAMP_120 = 0.35f;
    public const float FLIP_Z_DAMP_START = 0.15f;
    public const float FLIP_Z_DAMP_END = 0.21f;
    public const float FLIP_PITCHLOCK_EXTRA_TIME = 0.4f;
    public const float FLIP_FORWARD_IMPULSE_MAX_SPEED_SCALE = 1.0f;
    public const float FLIP_SIDE_IMPULSE_MAX_SPEED_SCALE = 1.9f;
    public const float FLIP_BACKWARD_IMPULSE_MAX_SPEED_SCALE = 2.5f;
    public const float FLIP_BACKWARD_IMPULSE_SCALE_X = 16f / 15f;

    // ===================== AIR CONTROL =====================

    // Torque magnitudes for pitch, yaw, roll

    public static readonly Vector3 CAR_AIR_CONTROL_TORQUE = new Vector3(130f, 95f, 400f);  
    public static readonly Vector3 CAR_AIR_CONTROL_DAMPING = new Vector3(30f, 20f, 50f);   
    public const float CAR_TORQUE_SCALE = 0.08f;

    // ===================== AUTO FLIP (TURTLE RECOVERY) =====================

    public const float CAR_AUTOFLIP_IMPULSE = 200f;    
    public const float CAR_AUTOFLIP_TORQUE = 25f;
    public const float CAR_AUTOFLIP_TIME = 0.4f;
    public const float CAR_AUTOFLIP_NORMZ_THRESH = 0.7071f; // sqrt(0.5)
    public const float CAR_AUTOFLIP_ROLL_THRESH = 1.5f;    

    // ===================== AUTO ROLL =====================

    public const float CAR_AUTOROLL_FORCE = 100f;  
    public const float CAR_AUTOROLL_TORQUE = 80f;

    // ===================== COLLISION =====================

    public const float CAR_COLLISION_FRICTION = 0.3f;
    public const float CAR_COLLISION_RESTITUTION = 0.1f;

    // ===================== CAR CONFIGS (OCTANE) =====================
    // From RocketSim/src/Sim/Car/CarConfig/CarConfig.cpp
    // All values in RL coords: X=forward, Y=right, Z=up, in UU
    //
    // NOTE: DIRECT PORT, needs tuning to Unity car models. OCTANE, DOMINUS
    // are just testing stand-ins

    public static readonly RLCarConfig OCTANE = new RLCarConfig
    {
        hitboxSize = new Vector3(120.507f, 86.6994f, 38.6591f),
        hitboxPosOffset = new Vector3(13.8757f, 0f, 20.755f),
        frontWheels = new RLWheelPairConfig
        {
            wheelRadius = 12.50f,
            suspensionRestLength = 38.755f,
            connectionOffset = new Vector3(51.25f, 25.90f, 20.755f)
        },
        backWheels = new RLWheelPairConfig
        {
            wheelRadius = 15.00f,
            suspensionRestLength = 37.055f,
            connectionOffset = new Vector3(-33.75f, 29.50f, 20.755f)
        },
        dodgeDeadzone = 0.5f
    };

    public static readonly RLCarConfig DOMINUS = new RLCarConfig
    {
        hitboxSize = new Vector3(130.427f, 85.7799f, 33.8f),
        hitboxPosOffset = new Vector3(9f, 0f, 15.75f),
        frontWheels = new RLWheelPairConfig
        {
            wheelRadius = 12.00f,
            suspensionRestLength = 33.95f,
            connectionOffset = new Vector3(50.30f, 31.10f, 15.75f)
        },
        backWheels = new RLWheelPairConfig
        {
            wheelRadius = 13.50f,
            suspensionRestLength = 33.85f,
            connectionOffset = new Vector3(-34.75f, 33.00f, 15.75f)
        },
        dodgeDeadzone = 0.5f
    };

    // ===================== HELPERS =====================


    // Convert RL local coordinates (X=forward, Y=right, Z=up)
    // to Unity local coordinates (X=right, Y=up, Z=forward).

    public static Vector3 RLToUnity(Vector3 rl) => new Vector3(rl.y, rl.z, rl.x);


    // Convert Unity local coordinates to RL local coordinates.

    public static Vector3 UnityToRL(Vector3 u) => new Vector3(u.z, u.x, u.y);
}