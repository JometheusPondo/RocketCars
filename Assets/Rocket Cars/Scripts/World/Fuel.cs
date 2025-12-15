using JetBrains.Annotations;
using Netick;
using Netick.Unity;
using UnityEngine;

public class Fuel : Replayable
{
  // Networked State ********************
  [Networked] public NetworkBool    IsActive             { get; set; } = true;

  public GameObject                 Render;
  public float                      AutoRegenerateTime   = 3f;
  public float                      RotateAnimationSpeed = 180f;
     
  private float                     _time                = 0f;
  private AudioSource               _audioSource;

  private void Awake()
  {
    _audioSource = GetComponent<AudioSource>();
  }

  public override void NetworkFixedUpdate()
  {
    // we make sure this code only runs in the server, or when we are not replaying.
    if (!IsServer || _goalReplaySystem.IsReplaying)
      return;

    if (!IsActive)
      if (Time.time - _time > AutoRegenerateTime)
        IsActive = true;
  }

  private void OnTriggerEnter(Collider other)
  {
    if (!IsActive) 
      return;

    var player  = other.gameObject.GetComponent<Player>();

    // we only apply fuel in the server, therefore fueling is not predicted.
    if (player != null && IsServer)
    {
      _time     = Time.time;
      IsActive  = false;
      player.Car.ReceiveFuel();
    }
  }

  [OnChanged(nameof(IsActive))][UsedImplicitly]
  private void OnIsVisibleChanged(OnChangedData dat)
  {
    // play pick up audio.
    if (IsActive == false && !dat.IsCatchingUp)
      _audioSource.NetworkPlayOneShot(Sandbox, _audioSource.clip);
    Render.SetActive(IsActive);
  }

  public override void NetworkRender()
  {
    Render.transform.Rotate(0f, RotateAnimationSpeed * Time.deltaTime, 0f);
  }
}
