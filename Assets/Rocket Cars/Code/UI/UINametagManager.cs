using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Netick;
using Netick.Unity;

[ExecuteBefore(typeof(GameMode))]
public class UINametagManager : NetworkBehaviour
{
  public GameObject                      NametagsParent;
  public UINametag                       NametagPrefab;
  public float                           NametagHeight = 5f;

  private Dictionary<Player, UINametag>  _nametags     = new(6);
  private Stack<UINametag>               _nametagPool  = new(6);

  private Soccer                         _soccer;
  private Camera                         _camera;

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _camera                       = Sandbox.FindObjectOfType<Camera>();
    _soccer                       = GetComponent<Soccer>();
    _soccer.OnPlayerAddedEvent   += OnPlayerAdded;
    _soccer.OnPlayerRemovedEvent += OnPlayerRemoved;

    var canvas                   = GetComponentInChildren<Canvas>().transform;

    for (int i = 0; i < Sandbox.Config.MaxPlayers; i++)
    {
      var nametag                = Sandbox.Instantiate(NametagPrefab, default, default);
      nametag.transform.SetParent(canvas, false);
      nametag.gameObject.SetActive(false);
      _nametagPool.Push(nametag);
    }
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode)
      return;

    // update nametags to be at the car's position.
    foreach (var nametag in _nametags.Values)
    {
      var carRenderPos             = nametag.Player.Car.NetworkRigidbody.RenderTransform.transform.position;

      // is within view
      if (Vector3.Angle(_camera.transform.forward, (carRenderPos - _camera.transform.position).normalized) < 70)
      {
        if (!nametag.gameObject.activeSelf)
          nametag.gameObject.SetActive(true);

        nametag.transform.position = _camera.WorldToScreenPoint(carRenderPos) + (Vector3.up * NametagHeight);
      }
      else
      {
        // disable nametag if out of view.
        if (nametag.gameObject.activeSelf)
          nametag.gameObject.SetActive(false);
      }

      if (nametag.gameObject.activeSelf)
        nametag.Refresh();
    }
  }

  private void OnPlayerAdded(Player player)
  {
    if (Sandbox.LocalPlayer == player.InputSource)
      return;

    var nameTag              = _nametagPool.Pop();
    nameTag.Init(player);
    nameTag.transform.SetParent(NametagsParent.transform, false);
    nameTag.gameObject.SetActive(true);
    _nametags.Add(player, nameTag);
  }

  private void OnPlayerRemoved(Player player)
  {
    if (Sandbox.LocalPlayer == player.InputSource)
      return;

    var nametag        = _nametags[player];
    nametag.gameObject.SetActive(false);
    _nametagPool.      Push(nametag);
    _nametags.         Remove(player);
  }
}
