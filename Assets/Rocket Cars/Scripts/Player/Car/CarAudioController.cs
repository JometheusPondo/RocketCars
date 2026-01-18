using UnityEngine;
using System.Collections;
using Netick.Unity;
using Netick;

[ExecuteAfter(typeof(CarController))]
public class CarAudioController : NetworkBehaviour
{
  public AudioSource    EngineAudioSource;
  public AudioSource    RocketAudioSource;
  public AudioSource    CollisionAudioSource;
  public AudioSource    JumpAudioSource;
  public AudioSource    SkidAudioSource;

  [Header("Tire Skid")]
  public float          BasePitch                           = 1.0f;
  public float          PitchRange                          = 0.7f;
  public float          RandomJitterIntensity               = 0.05f; 
  public float          MaxSpeed                            = 30f;

  [Header("Engine")]
  public float          EngineAudioPitchLerpFactor          = 7f;
  public float          SkidLerpFactor                      = 7f;
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
    _carController.OnJumpEvent           += PlayJumpAudio;
  }

  private void PlayJumpAudio()
  {
    JumpAudioSource.NetworkPlay(Sandbox); // we use NetworkPlay instead of Play for sandbox-safety.
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
    float currentSpeed          = Mathf.Abs(_carController.SideSpeed);
    float speedFactor           = Mathf.InverseLerp(0f, MaxSpeed, currentSpeed);
    float targetPitch           = BasePitch + (speedFactor * PitchRange);
    float jitter                = (Mathf.PerlinNoise(Time.time * 5f, 0f) - 0.5f) * RandomJitterIntensity;

    SkidAudioSource.pitch       = targetPitch + jitter;

    if (_carController.IsSlipping)
    {
      if (!SkidAudioSource.isPlaying)
        SkidAudioSource.NetworkPlay(Sandbox);

      SkidAudioSource.volume    = Mathf.Lerp(0, 1, speedFactor);
    }
    else
      SkidAudioSource.volume    = Mathf.Lerp(SkidAudioSource.volume, _carController.IsSlipping ? 1f : 0, Time.deltaTime * SkidLerpFactor);


    float enginePitchModifier   = _carController.IsGrounded ? Mathf.Lerp(1f, 1.7f, Mathf.InverseLerp(0f, 30f, _carController.Rigidbody.velocity.magnitude)) : 1.9f;
    EngineAudioSource.pitch     = Mathf.Lerp(EngineAudioSource.pitch, 1f * enginePitchModifier, Time.deltaTime * EngineAudioPitchLerpFactor);

    var rocket                  = _carController.EnableRocket && _carController.LastInput.Rocket && _carController.FuelTickTime > 0f;
    RocketAudioSource.volume    = Mathf.Lerp(RocketAudioSource.volume, rocket ? 1f : 0, Time.deltaTime * (rocket ? RocketStartingAudioVolumeLerpFactor : RocketStoppingAudioVolumeLerpFactor));
  }

}
