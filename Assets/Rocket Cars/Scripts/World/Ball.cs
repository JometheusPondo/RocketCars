using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick.Unity;

public class Ball : NetworkBehaviour
{
  public float           GravityForce = 10f;
  public Player          LastTouchedPlayer;
  public Vector3         InitialPosition;
  
  [HideInInspector]
  public Rigidbody       Rigidbody { get; private set; }

  private AudioSource    _audioSource;

  void Awake()
  {
    _audioSource    = GetComponent<AudioSource>();
    Rigidbody       = gameObject.GetComponent<Rigidbody>();
    InitialPosition = transform.position;
  }

  public override void NetworkFixedUpdate()
  {
    Rigidbody.AddForce(Vector3.down * GravityForce, ForceMode.Acceleration);
  }

  public void OnCollisionEnter(Collision collision)
  {
    var player          = collision.gameObject.GetComponentInParent<Player>();

    if (player != null)
      LastTouchedPlayer = player;

    if (Object != null && !IsResimulating)
      PlayCollisionAudio(collision);
  }

  private void PlayCollisionAudio(Collision collision)
  {
    _audioSource.volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0f, 5f, collision.relativeVelocity.magnitude));
    _audioSource.pitch  = Random.Range(0.8f, 1.3f);
    _audioSource.NetworkPlay(Sandbox); // we use NetworkPlay instead of Play Play sandbox-safety.
  }
}
