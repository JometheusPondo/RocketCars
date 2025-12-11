using Netick;
using Netick.Unity;
using UnityEngine;

[ExecuteAfter(typeof(CarCameraController))]
public class FlyCameraController : NetworkBehaviour
{
  public Transform StartPosition;

  [Header("Movement")]
  public float     MoveSpeed       = 10f;
  public float     FastMultiplier  = 3f;
  public float     SlowMultiplier  = 0.25f;

  [Header("Mouse Look")]
  public float     LookSensitivity = 2f;
  public float     MaxLookAngle    = 90f;

  private Camera   _cam;
  private GameMode _gm;

  public override void NetworkStart()
  {
    _gm                     = GetComponent<GameMode>();
    _cam                    = Sandbox.GetComponent<GlobalInfo>().Camera;
    _cam.transform.position = StartPosition.position;
    _cam.transform.rotation = StartPosition.rotation;
  }

  public void LateUpdate()
  {
    if (Sandbox == null || _cam == null || !Sandbox.IsReplay)
      return;

    if (Sandbox.ContainsPlayer(_gm.ReplaySelectedPlayer))
      return;

    var player = Sandbox.GetPlayerObject<CarController>(_gm.ReplaySelectedPlayer);
    if (player != null)
      return;

    if (Input.GetMouseButton(1))
      HandleMouseLook();

    HandleMovement();
  }

  void HandleMouseLook()
  {
    float mouseX = Input.GetAxis("Mouse X") * LookSensitivity;
    float mouseY = Input.GetAxis("Mouse Y") * LookSensitivity;

    // Rotate relative to the camera’s current rotation directly
    Vector3 euler = _cam.transform.eulerAngles;
    euler.x -= mouseY;
    euler.y += mouseX;

    // Clamp pitch (x-axis) to avoid flipping
    euler.x = ClampAngle(euler.x, -MaxLookAngle, MaxLookAngle);

    _cam.transform.rotation = Quaternion.Euler(euler);
  }

  void HandleMovement()
  {
    float speed = MoveSpeed;

    if (Input.GetKey(KeyCode.LeftShift))
      speed *= FastMultiplier;
    else if (Input.GetKey(KeyCode.LeftControl))
      speed *= SlowMultiplier;

    Vector3 move = Vector3.zero;
    move += _cam.transform.forward * Input.GetAxisRaw("Vertical");
    move += _cam.transform.right * Input.GetAxisRaw("Horizontal");

    if (Input.GetKey(KeyCode.E))
      move += _cam.transform.up;
    if (Input.GetKey(KeyCode.Q))
      move -= _cam.transform.up;

    _cam.transform.position += move * speed * Time.unscaledDeltaTime;
  }

  static float ClampAngle(float angle, float min, float max)
  {
    if (angle > 180f) angle -= 360f;
    return Mathf.Clamp(angle, min, max);
  }
}
