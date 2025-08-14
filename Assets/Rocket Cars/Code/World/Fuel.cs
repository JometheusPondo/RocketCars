using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick.Unity;
using Netick;

public class Fuel : Replayable
{
  // Networked State ********************
  [Networked] public NetworkBool    IsActive            { get; set; } = true;

  public GameObject                 Render;

  public float                      AutoRegenerateTime  = 3f;

  private float                     _time               = 0f;
  private AudioSource               _audioSource;

  private void Awake()
  {
    _audioSource = GetComponent<AudioSource>();
  }

  public override void NetworkFixedUpdate()
  {
    // we make sure this code only runs in the server, or when we are not replaying.
    if (!IsServer || _replaySystem.IsReplaying)
      return;

    if (!IsActive)
      if (Time.time - _time > AutoRegenerateTime)
        IsActive = true;
  }

  private void OnTriggerEnter(Collider other)
  {
    var player  = other.gameObject.GetComponent<Player>();

    // we only apply fuel in the server, therefore fueling is not predicted.
    if (player != null && IsServer)
    {
      _time     = Time.time;
      IsActive  = false;
      player.Car.ReceiveFuel();
    }
  }

  [OnChanged(nameof(IsActive))]
  private void OnIsVisibleChanged(OnChangedData dat)
  {
    // play pick up audio.
    if (IsActive == false)
      _audioSource.NetworkPlayOneShot(Sandbox, _audioSource.clip);
    Render.SetActive(IsActive);
  }
}
