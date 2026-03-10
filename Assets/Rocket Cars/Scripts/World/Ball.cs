using Netick;
using Netick.Unity;
using System;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [HideInInspector] public Vector3 InitialPosition;
    [HideInInspector] public Rigidbody Rigidbody { get; private set; }
    [HideInInspector] public NetworkRigidbody NetworkRigidbody { get; private set; }
    public Player LastTouchedPlayer;

    public float S = 0.01f; // match CarController

    private float _radius;
    private Vector3 _extraImpulseCache = Vector3.zero;
    private AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        Rigidbody = GetComponent<Rigidbody>();
        NetworkRigidbody = GetComponent<NetworkRigidbody>();
        InitialPosition = transform.position;
        _radius = RLC.BALL_RADIUS * S;


    }

    public override void NetworkFixedUpdate()
    {
        // Gravity
        Rigidbody.AddForce(Vector3.down * Mathf.Abs(RLC.GRAVITY_Z) * S, ForceMode.Acceleration);

        // Bullet linear damping: velocity *= (1 - drag) ^ dt
        float dt = Sandbox.FixedDeltaTime;
        float dampFactor = Mathf.Pow(1f - RLC.BALL_DRAG, dt);
        Rigidbody.velocity *= dampFactor;

        // Clamp speeds
        float maxSpeed = RLC.BALL_MAX_SPEED * S;
        if (Rigidbody.velocity.sqrMagnitude > maxSpeed * maxSpeed)
            Rigidbody.velocity = Rigidbody.velocity.normalized * maxSpeed;

        if (Rigidbody.angularVelocity.sqrMagnitude > RLC.BALL_MAX_ANG_SPEED * RLC.BALL_MAX_ANG_SPEED)
            Rigidbody.angularVelocity = Rigidbody.angularVelocity.normalized * RLC.BALL_MAX_ANG_SPEED;

        // Apply cached extra impulse from car hit
        if (_extraImpulseCache.sqrMagnitude > 0.001f)
        {
            Rigidbody.velocity += _extraImpulseCache;
            _extraImpulseCache = Vector3.zero;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        if (player == null) return;

        LastTouchedPlayer = player;

        Rigidbody carRb = other.GetComponentInParent<Rigidbody>();
        CarController car = other.GetComponentInParent<CarController>();
        if (carRb == null) return;

        // --- Base collision: restitution 0.0, friction 2.0 ---
        Vector3 n = (transform.position - carRb.worldCenterOfMass).normalized;

        Vector3 contactPoint = transform.position - n * _radius;
        Vector3 contactOffset = contactPoint - carRb.worldCenterOfMass;
        Vector3 carVelAtContact = carRb.velocity + Vector3.Cross(carRb.angularVelocity, contactOffset);

        Vector3 vRel = Rigidbody.velocity - carVelAtContact;
        float vRelN = Vector3.Dot(vRel, n);

        if (vRelN > 0f) return; // already separating

        Vector3 vPerp = vRelN * n;
        Vector3 vPara = vRel - vPerp;

        Vector3 vSpin = _radius * Vector3.Cross(n, Rigidbody.angularVelocity);
        Vector3 slip = vPara + vSpin;

        // Restitution is 0.0 for car-ball — Bullet just stops the approach, no bounce
        Vector3 deltaVPerp = -(1f + RLC.CARBALL_COLLISION_RESTITUTION) * vPerp;

        // Friction is 2.0 for car-ball — very high, kills tangential slip aggressively
        Vector3 deltaVPara = Vector3.zero;
        float slipMag = slip.magnitude;
        if (slipMag > 0.001f)
        {
            float ratio = vPerp.magnitude / slipMag;
            float frictionScale = Mathf.Min(1f, RLC.BALL_FRICTION_Y * ratio);
            deltaVPara = -frictionScale * RLC.CARBALL_COLLISION_FRICTION * slip;
        }

        Rigidbody.velocity += deltaVPerp + deltaVPara;

        // Angular velocity update
        Rigidbody.angularVelocity += (0.0003f / _radius) * Vector3.Cross(deltaVPara, n);

        // --- Extra impulse (RocketSim _OnHit) ---
        ApplyExtraCarBallImpulse(car, carRb);

        if (Object != null && !IsResimulating)
            PlayCollisionAudio(null);
    }

    private void ApplyExtraCarBallImpulse(CarController car, Rigidbody carRb)
    {
        Vector3 ballPos = transform.position / S; // to UU
        Vector3 carPos = carRb.worldCenterOfMass / S;
        Vector3 ballVel = Rigidbody.velocity / S;
        Vector3 carVel = carRb.velocity / S;
        Vector3 carForward = carRb.transform.forward;

        Vector3 relPos = ballPos - carPos;
        Vector3 relVel = ballVel - carVel;

        float relSpeed = Mathf.Min(relVel.magnitude, RLC.BALL_CAR_EXTRA_IMPULSE_MAXDELTAVEL);

        if (relSpeed <= 0f) return;

        // Scale Z down — makes hits more horizontal, less poppy
        // In Unity Y is up, in UE Z is up
        Vector3 hitDir = new Vector3(
            relPos.x,
            relPos.y * RLC.BALL_CAR_EXTRA_IMPULSE_Z_SCALE,
            relPos.z
        ).normalized;

        // Reduce forward component — makes side hits stronger relative to head-on
        Vector3 forwardAdjust = carForward * Vector3.Dot(hitDir, carForward) * (1f - RLC.BALL_CAR_EXTRA_IMPULSE_FORWARD_SCALE);
        hitDir = (hitDir - forwardAdjust).normalized;

        // Scale by curve — stronger at low speed, weaker at high
        float factor = RLC.BALL_CAR_EXTRA_IMPULSE_FACTOR.Evaluate(relSpeed);

        Vector3 addedVel = hitDir * relSpeed * factor;

        // Cache — applied at end of tick (matching RocketSim)
        _extraImpulseCache += addedVel * S; // back to Unity units
    }

    public void OnCollisionEnter(Collision collision)
    {
        var player = collision.gameObject.GetComponentInParent<Player>();
        if (player != null)
            LastTouchedPlayer = player;

        if (Object != null && !IsResimulating)
            PlayCollisionAudio(collision);
    }

    private void ApplyBounce(Collision collision)
    {
        if (collision.contactCount == 0) return;

        var player = collision.gameObject.GetComponentInParent<Player>();
        if (player == null) return;

        Rigidbody carRb = collision.gameObject.GetComponentInParent<Rigidbody>();
        Vector3 n = (collision.contacts[0].point - carRb.worldCenterOfMass).normalized;

        Vector3 contactOffset = collision.contacts[0].point - carRb.worldCenterOfMass;
        Vector3 carVelAtContact = carRb.velocity + Vector3.Cross(carRb.angularVelocity, contactOffset);

        Vector3 v = Rigidbody.velocity - carVelAtContact;
        Vector3 w = Rigidbody.angularVelocity;

        Vector3 vPerp = Vector3.Dot(v, n) * n;
        Vector3 vPara = v - vPerp;

        Vector3 vSpin = _radius * Vector3.Cross(n, w);
        Vector3 slip = vPara + vSpin;

        Vector3 deltaVPerp = -(1f + RLC.BALL_RESTITUTION) * vPerp;

        Vector3 deltaVPara = Vector3.zero;
        float slipMag = slip.magnitude;
        if (slipMag > 0.001f)
        {
            float ratio = vPerp.magnitude / slipMag;
            float frictionScale = Mathf.Min(1f, RLC.BALL_FRICTION_Y * ratio);
            deltaVPara = -frictionScale * RLC.BALL_FRICTION * slip;
        }

        Rigidbody.velocity = Rigidbody.velocity + deltaVPerp + deltaVPara;
        Rigidbody.angularVelocity = w + (RLC.BALL_DRAG / _radius) * Vector3.Cross(deltaVPara, n);
    }

    private void PlayCollisionAudio(Collision collision)
    {
        float impactVel = collision != null
                        ? collision.relativeVelocity.magnitude
                        : Rigidbody.velocity.magnitude * 0.5f;

        _audioSource.volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0f, 5f, impactVel));
        _audioSource.pitch = UnityEngine.Random.Range(0.8f, 1.3f);
        _audioSource.NetworkPlay(Sandbox);
    }

    public void TossToward(Vector3 targetPos, Vector3 targetVel, float tossSpeed = 15f, float loftHeight = 3f)
    {
        Vector3 futurePos = targetPos + targetVel * 1f;
        futurePos.y = targetPos.y;

        Vector3 dir = (futurePos - transform.position).normalized;
        Rigidbody.velocity = dir * tossSpeed + Vector3.up * loftHeight;
        Rigidbody.angularVelocity = Vector3.zero;
    }
}