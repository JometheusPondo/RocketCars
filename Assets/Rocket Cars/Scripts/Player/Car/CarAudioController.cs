using UnityEngine;
using System.Collections;
using Netick.Unity;
using Netick;

public class CarAudioController : NetworkBehaviour
{
  public AudioSource    EngineAudioSource;
  public AudioSource    RocketAudioSource;
  public AudioSource    CollisionAudioSource;
  public AudioSource    JumpAudioSource;

  [Header("Engine")]
  public float          EngineAudioPitchLerpFactor          = 7f;
  public float          MaxCarSpeed                         = 30;

  [Header("Rocket")]
  public float          RocketStartingAudioVolumeLerpFactor = 1.2f;
  public float          RocketStoppingAudioVolumeLerpFactor = 1.2f;

  private CarController _carController;
  private Ball          _ball;

  public override void NetworkStart()
  {
    _ball                                 = Sandbox.FindObjectOfType<Ball>();
    _carController                        = Object.GetComponent<CarController>();
    _carController.OnCollisionEnterEvent += PlayCollisionAudio;
    _carController.OnJumpAudioEvent      += PlayJumpAudio;
  }

  private void PlayJumpAudio()
  {
    JumpAudioSource.NetworkPlay(Sandbox); // we use NetworkPlayOneShot instead of PlayOneShot for sandbox-safety.
  }

  private void PlayCollisionAudio(Collision collision)
  {
    // we don't play collision audio with the ball.
    if (collision.gameObject == _ball.gameObject)
      return;

    CollisionAudioSource.volume = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0f, 5f, collision.relativeVelocity.magnitude));
    CollisionAudioSource.pitch  = Random.Range(0.9f, 1.2f);
    CollisionAudioSource.NetworkPlay(Sandbox); // we use NetworkPlay instead of Play for sandbox-safety.
  }

  public override void NetworkRender()
  {
    float pitchModifier         = _carController.IsGrounded ? Mathf.Lerp(1f, 1.7f, Mathf.InverseLerp(0f, 30f, _carController.Rigidbody.velocity.magnitude)) : 1.9f;
    EngineAudioSource.pitch     = Mathf.Lerp(EngineAudioSource.pitch, 1f * pitchModifier, Time.deltaTime * EngineAudioPitchLerpFactor);

    var isBoosting              = _carController.LastInput.Rocket && _carController.FuelTickTime > 0f;
    RocketAudioSource.volume    = Mathf.Lerp(RocketAudioSource.volume, isBoosting ? 1f : 0, Time.deltaTime * (isBoosting ? RocketStartingAudioVolumeLerpFactor : RocketStoppingAudioVolumeLerpFactor));
  }

}
