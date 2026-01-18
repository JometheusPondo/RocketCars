using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Netick;
using Netick.Unity;
using UnityEngine.Serialization;

[ExecuteBefore(typeof(GameMode))]
public class UICar : NetworkBehaviour
{
  [SerializeField]
  private GameObject                _container;
  [SerializeField] 
  private Image                     _fuelFillBar;
  [SerializeField] 
  private TMP_Text                  _fuelText;
  [SerializeField]
  private TextMeshProUGUI           _cameraModeText;

  private bool                      _cameraModeLast;
  private CarController             _carController;
  private CarCameraController       _carCameraController;
  private GameMode                  _gm;

  public override void NetworkStart()
  {
    _gm = Sandbox.GetComponent<GlobalData>().GameMode;
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode)
      return;

    var plrId = _gm.GlobalData.IsReplay ? _gm.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId;
    Sandbox.TryGetPlayerObject(plrId, out Player player);

    if (player != null && player.IsReady)
    {
      _carController                  = player.GetComponent<CarController>();
      _carCameraController            = player.GetComponent<CarCameraController>();

      if (!_container.activeInHierarchy && !_gm.DisableCarCamera)
        _container.SetActive(true);
      if (_container.activeInHierarchy && _gm.DisableCarCamera)
        _container.SetActive(false);

      if (!_gm.GlobalData.IsReplay && Netick.Unity.Network.StartMode != StartMode.MultiplePeers)
        Cursor.lockState = _gm.Paused == true ? CursorLockMode.None : CursorLockMode.Locked;
    }
    else
    {
      _carController                  = null;
      _carCameraController            = null;
      _container.SetActive(false);
    }

    if (_carController == null)
      return;

    if (_cameraModeLast != _carCameraController.LookAtBall)
    {
      _cameraModeText.text            = _carCameraController.LookAtBall ? "Ball" : "Car";
      _cameraModeLast                 = _carCameraController.LookAtBall;
    }

    if (_carCameraController.LookAtBall)
      _cameraModeText.color           = Color.Lerp(Color.white, Color.red, Mathf.InverseLerp(-1f, 1f, Mathf.Sin(15f * Time.unscaledTime)));
    else
      _cameraModeText.color           = Color.white;

    float fuel                        = Mathf.RoundToInt(Sandbox.TickToTime(_carController.FuelTickTime));
    float alpha                       = fuel / _carController.MaxFuel;
    _fuelFillBar.transform.localScale = new Vector3(alpha, 1f, 1f);
    _fuelText.SetText("{0}", fuel);
  }
}
