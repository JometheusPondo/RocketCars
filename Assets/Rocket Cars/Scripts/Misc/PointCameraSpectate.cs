using Netick;
using Netick.Unity;
using UnityEngine;

[ExecutionOrder(20)]
public class PointCameraSpectate : MonoBehaviour
{
  [SerializeField] 
  private Transform _pivot;
  [SerializeField] 
  private float     _rotateSpeed;
  [SerializeField]
  private Camera    _cam;

  private void LateUpdate()
  {
    var sandbox = NetworkSandbox.FindSandboxOf(gameObject);
    if (sandbox != null)
    {
      var globalInfo  = sandbox.GetComponent<GlobalInfo>();
      if (_cam == null)
        _cam          = globalInfo.Camera;

      NetworkPlayerId plrId = default;

      if (globalInfo.GameMode != null)
        plrId         = globalInfo.IsReplay ? globalInfo.GameMode.SpectatedPlayer : sandbox.LocalPlayer.PlayerId;
      var soccer      = globalInfo.GameMode as Soccer;
     
      sandbox.TryGetPlayerObject(plrId, out Player player);

      if (soccer != null)
        if ((player && player.IsReady && soccer.GameState != Soccer.State.GameOver) || (soccer.GameState == Soccer.State.GoalReplay))
          return;

      if (player == null && globalInfo.IsReplay)
        return;

      if (_cam != null && _pivot != null)
        MoveCamera(_cam);
    }
    else
    {
      if (_cam != null && _pivot != null)
        MoveCamera(_cam);
    }
  }

  private void MoveCamera(Camera cam)
  {
    transform.Rotate(0f, _rotateSpeed * Time.unscaledDeltaTime, 0f);
    cam.transform.SetPositionAndRotation(_pivot.transform.position, _pivot.transform.rotation);
  }
}
