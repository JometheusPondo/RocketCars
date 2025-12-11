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
  private UIFuelBar           _uiFuelBar;
  [SerializeField]
  private TextMeshProUGUI     _cameraModeText;

  private Graphic[]           _UIElements;
  private bool                _cameraModeLast;
  private CarController       _carController;
  private CarCameraController _carCameraController;
  private GameMode            _gm;

  public override void NetworkStart()
  {
    _gm         = Sandbox.GetComponent<GlobalInfo>().GameMode;
    _UIElements = _UICarHUD.GetComponentsInChildren<MaskableGraphic>();
  }

  public override void NetworkRender()
  {
    var plrId = Sandbox.IsReplay ? _gm.ReplaySelectedPlayer : Sandbox.LocalPlayer.PlayerId;

    Sandbox.TryGetPlayerObject(plrId, out Player player);

    if (player != null && player.IsReady)
    {
      _carController       = player.GetComponent<CarController>();
      _carCameraController = player.GetComponent<CarCameraController>();
      _UICarHUD.SetActive(true);
    }
    else
    {
      _carController = null;
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

    float fuel = Mathf.RoundToInt(Sandbox.TickToTime(_carController.FuelTickTime));
    _uiFuelBar.UpdateValue(fuel, _carController.MaxFuel);
  }

  /// <summary>
  /// Show/hides the car HUD.
  /// </summary>
  public void SetVisibility(bool visibility)
  {
    foreach (var element in _UIElements)
      element.SetEnabled(Sandbox, visibility);
  }
}
