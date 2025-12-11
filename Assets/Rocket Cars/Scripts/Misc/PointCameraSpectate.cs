using Netick;
using Netick.Unity;
using UnityEngine;

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
      if (_cam == null )
        _cam = sandbox.GetComponent<GlobalInfo>().Camera;

      if (sandbox.IsReplay || sandbox.TryGetLocalPlayerObject<Player>(out var player) && player.IsReady)
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
    transform.Rotate(0f, _rotateSpeed * Time.deltaTime, 0f);
    cam.transform.SetPositionAndRotation(_pivot.transform.position, _pivot.transform.rotation);
  }
}
