using UnityEngine;

public class PointCameraSpectate : MonoBehaviour
{
  [SerializeField] private Transform _pivot;
  [SerializeField] private float     _rotateSpeed;

  private void LateUpdate()
  {
    _pivot.transform.Rotate(0f, _rotateSpeed * Time.deltaTime, 0f);
  }
}
