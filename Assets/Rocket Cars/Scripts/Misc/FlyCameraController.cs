using Netick;
using Netick.Unity;
using UnityEngine;

[ExecuteAfter(typeof(CarCameraController))]
public class FlyCameraController : NetworkBehaviour
{
  public Transform StartPosition;

  [Header("Movement")]
  public float     MoveSpeed         = 10f;
  public float     FastMultiplier    = 3f;
  public float     SlowMultiplier    = 0.25f;

  [Header("Smoothing")]
  public bool      UseSmoothing      = true;
  public float     MovementSharpness = 15f;
  public float     RotationSharpness = 10f;

  [Header("Mouse Look")]
  public float     LookSensitivity   = 2f;
  public float     MaxLookAngle      = 89f;

  private Camera   _cam;
  private GameMode _gm;
  private bool     _isActive;
  private Vector3  _targetPosition;
  private float    _internalYaw;
  private float    _internalPitch;
  private float    _smoothedYaw;
  private float    _smoothedPitch;

  public override void NetworkStart()
  {
    _gm  = GetComponent<GameMode>();
    _cam = Sandbox.GetComponent<GlobalInfo>().Camera;
    SyncToCurrentCamera();
  }

  public void LateUpdate()
  {
    if (!CheckIfActive())
    {
      _isActive = false;
      return;
    }

    if (!_isActive)
    {
      SyncToCurrentCamera();
      _isActive = true;
    }

    HandleMouseLook();
    HandleMovement();
    ApplyCameraTransform();
  }

  private bool CheckIfActive()
  {
    if (Sandbox == null || !Sandbox.IsRunning || _cam == null || !_gm.GlobalInfo.IsReplay)
      return false;

    return !Sandbox.ContainsPlayer(_gm.SpectatedPlayer);
  }

  private void SyncToCurrentCamera()
  {
    _targetPosition = _cam.transform.position;

    Vector3 euler   = _cam.transform.eulerAngles;
    _internalYaw    = euler.y;
    _internalPitch  = (euler.x > 180f) ? euler.x - 360f : euler.x;
    _smoothedYaw    = _internalYaw;
    _smoothedPitch  = _internalPitch;
  }

  void HandleMouseLook()
  {
    if (!Input.GetMouseButton(1))
      return;

    _internalYaw   += Input.GetAxis("Mouse X") * LookSensitivity;
    _internalPitch -= Input.GetAxis("Mouse Y") * LookSensitivity;
    _internalPitch  = Mathf.Clamp(_internalPitch, -MaxLookAngle, MaxLookAngle);
  }

  void HandleMovement()
  {
    float speed     = MoveSpeed;
    if (Input.GetKey(KeyCode.LeftShift)) 
      speed         *= FastMultiplier;
    else if (Input.GetKey(KeyCode.LeftControl)) 
      speed         *= SlowMultiplier;

    var inputDir     = Vector3.zero;
    inputDir        += _cam.transform.forward * Input.GetAxisRaw("Vertical");
    inputDir        += _cam.transform.right * Input.GetAxisRaw("Horizontal");
    if (Input.GetKey(KeyCode.E)) 
      inputDir      += Vector3.up;
    if (Input.GetKey(KeyCode.Q)) 
      inputDir      -= Vector3.up;

    _targetPosition += inputDir * speed * Time.unscaledDeltaTime;
  }

  void ApplyCameraTransform()
  {
    float dt = Time.unscaledDeltaTime;

    if (UseSmoothing)
    {
      float moveFactor        = 1f - Mathf.Exp(-MovementSharpness * dt);
      _cam.transform.position = Vector3.Lerp(_cam.transform.position, _targetPosition, moveFactor );
    }
    else
    {
      _cam.transform.position = _targetPosition;
    }

    if (UseSmoothing)
    {
      float rotFactor = 1f - Mathf.Exp(-RotationSharpness * dt);
      _smoothedYaw    = Mathf.Lerp(_smoothedYaw, _internalYaw, rotFactor);
      _smoothedPitch  = Mathf.Lerp(_smoothedPitch, _internalPitch, rotFactor);
    }
    else
    {
      _smoothedYaw   = _internalYaw;
      _smoothedPitch = _internalPitch;
    }

    _cam.transform.rotation = Quaternion.Euler(_smoothedPitch,_smoothedYaw,0f);
  }
}
