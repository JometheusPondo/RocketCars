using UnityEngine;

public class CameraSpectate : MonoBehaviour
{
  [SerializeField] private Transform _pivotPoint;
  private bool _enableSpectate = true;

  public void DisableSpectate()
  {
    _enableSpectate = false;
  }

  private void LateUpdate()
  {
    if (_enableSpectate)
      transform.SetPositionAndRotation(_pivotPoint.transform.position, _pivotPoint.transform.rotation);
  }
}
