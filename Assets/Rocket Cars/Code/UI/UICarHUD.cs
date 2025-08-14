using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Netick;
using Netick.Unity;

[ExecuteBefore(typeof(GameMode))]
public class UICarHUD : NetworkBehaviour
{
  [SerializeField]
  private GameObject          _UICarHUD;
  [SerializeField]
  private TextMeshProUGUI     _boosterText;
  [SerializeField]
  private TextMeshProUGUI     _cameraModeText;

  private Graphic[]           _UIElements;
  private bool                _cameraModeLast;
  private CarController       _carController;
  private CarCameraController _carCameraController;

  public override void NetworkStart()
  {
    _UIElements = _UICarHUD.GetComponentsInChildren<MaskableGraphic>();
  }

  public override void NetworkRender()
  {
    if (Sandbox.TryGetLocalPlayerObject(out Player localPlayer) && localPlayer.IsReady)
    {
      _carController       = localPlayer.GetComponent<CarController>();
      _carCameraController = localPlayer.GetComponent<CarCameraController>();
      _UICarHUD.SetActive(true);
    }
    else
    {
      _carController       = null;
      _carCameraController = null;
      _UICarHUD.SetActive(false);
    }

    if (_carController == null)
      return;

    if (_cameraModeLast != _carCameraController.LookAtBall)
    {
      _cameraModeText.text   = _carCameraController.LookAtBall ? "Ball Cam" : "Car Cam";
      _cameraModeLast        = _carCameraController.LookAtBall;
    }
  
    if (_carCameraController.LookAtBall)
      _cameraModeText.color  = Color.Lerp(Color.white, Color.red, Mathf.InverseLerp(-1f, 1f, Mathf.Sin(15f * Time.unscaledTime)));
    else
      _cameraModeText.color  = Color.white;

    _boosterText.text        = (Mathf.RoundToInt(Sandbox.TickToTime(_carController.FuelTickTime))).ToString();
  }

  /// <summary>
  /// Show/hides the car HUD.
  /// </summary>
  /// <param name="visibility"></param>
  public void SetVisibility(bool visibility)
  {
    foreach (var element in _UIElements)
      element.SetEnabled(Sandbox, visibility);
  }
}
