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

    public float S = 0.0125f; // match CarController

    private float _radius;
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

        // Velocity damping
        Rigidbody.velocity *= (1f - RLC.BALL_DRAG);

        // Clamp speeds
        float maxSpeed = RLC.BALL_MAX_SPEED * S;
        if (Rigidbody.velocity.sqrMagnitude > maxSpeed * maxSpeed)
            Rigidbody.velocity = Rigidbody.velocity.normalized * maxSpeed;

        if (Rigidbody.angularVelocity.sqrMagnitude > RLC.BALL_MAX_ANG_SPEED * RLC.BALL_MAX_ANG_SPEED)
            Rigidbody.angularVelocity = Rigidbody.angularVelocity.normalized * RLC.BALL_MAX_ANG_SPEED;
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
        _audioSource.volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0f, 5f, collision.relativeVelocity.magnitude));
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