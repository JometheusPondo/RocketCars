using UnityEngine;
using System;
using System.Collections;
using Netick.Unity;
using Netick;
public class CarCameraController : NetworkBehaviour
{
  public Transform        CarRenderTransform;

  [Header("Camera")]
  public bool             LookAtBall         = true;
  public Transform        CameraParent;
  public Vector3          CameraOffset;
  public float            LerpFactor         = 20f;
  public AudioClip        ChangeCameraAudioClip;

  private Transform       _camera;
  private CarController   _carController;
  private Player          _player;
  private GameMode        _gm;
  private Transform       _ballRenderTransform;
  private CameraSpectate  _cameraSpectate;
  private Vector3         _curDir;
  private Vector3         _vel;

  private Vector3         _curPos;
  private Vector3         _prevPos;
  private Quaternion      _curRot;
  private Quaternion      _prevRot;

  public override void NetworkStart()
  {
    _player               = GetComponent<Player>();
    _carController        = GetComponent<CarController>();   
    _gm                   = Sandbox.FindObjectOfType<GameMode>();
    _camera               = Sandbox.FindObjectOfType<Camera>().transform;
    _ballRenderTransform  = Sandbox.FindObjectOfType<Ball>().  transform.GetChild(0);
        _cameraSpectate = Sandbox.FindObjectOfType<CameraSpectate>();
  }

    public override void NetworkRender()
    {
        if (!(IsInputSource && _player.IsReady))
            return;

        _cameraSpectate.DisableSpectate();

        if (_gm.DisableCarCamera)
            return;

        if (Sandbox.InputEnabled && Input.GetButtonDown("Camera Toggle")) // toggle camera mode.
        {
            LookAtBall = !LookAtBall;
            // audio effect
            if (Sandbox.IsVisible)
                AudioSource.PlayClipAtPoint(ChangeCameraAudioClip, transform.position);
        }

        _camera.transform.position = Vector3.Lerp(_prevPos, _curPos, Sandbox.LocalInterpolation.Alpha);
        _camera.transform.rotation = Quaternion.Slerp(_prevRot, _curRot, Sandbox.LocalInterpolation.Alpha);
    }

    public override void NetworkFixedUpdate()
    {
        if (IsResimulating)
            return;

        if (!(IsInputSource && _player.IsReady))
            return;

        _prevPos = _curPos;
        _prevRot = _curRot;

        if (LookAtBall)
            FollowCarAndLookAtBall(Sandbox.ScaledFixedDeltaTime);
        else
            FollowCarAndLookAtCar(Sandbox.ScaledFixedDeltaTime);
    }

  private void FollowCarAndLookAtCar(float deltaTime)
  {
    var forward                = _carController.Rigidbody.velocity;

    if (_carController.IsGrounded)
    {
      // reverse view when middle mouse button is pressed.
      if (Input.GetMouseButton(2))
        forward                = -transform.forward;
      else
        forward                = transform.forward;
    }

    forward.y                  = 0;
    forward.Normalize();

    var dot                    = Mathf.     Abs(Vector3.Dot(_curDir.normalized, forward));
    var t                      = Math.      Max(0.2f, Mathf.Pow(dot, 10)) * LerpFactor * deltaTime;
    _curDir                    = Vector3.   Lerp(_curDir, forward, t);

    var pos                    = CarRenderTransform.parent.position + (_curDir * CameraOffset.z) + (Vector3.up * CameraOffset.y);
    var rot                    = Quaternion.LookRotation(_curDir, Vector3.up);


        //_camera.transform.position = Vector3.Lerp(_camera.transform.position, pos, LerpFactor * deltaTime);
        _curPos = Vector3.Lerp(_curPos, pos, LerpFactor * deltaTime);

        // _camera.transform.position = Vector3.   SmoothDamp (_camera.transform.position, pos, ref _vel, LerpFactor * deltaTime); 
        //_camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, rot, LerpFactor * deltaTime);
        _curRot = Quaternion.Slerp(_curRot, rot, LerpFactor * deltaTime);
  }

  private void FollowCarAndLookAtBall(float deltaTime)
  {
    var ballPosNoY             = _ballRenderTransform.parent.position;
    ballPosNoY.y               = CarRenderTransform.parent.position.y;
 
    var carPos                 = CarRenderTransform.parent.position;
    
    var carToBall              = (ballPosNoY - carPos).normalized;
    var carToBall2             = (_ballRenderTransform.parent.position - carPos).normalized;
    var camLookDir             = carToBall;

    if (Vector3.Distance(ballPosNoY, carPos) > 40f)
      camLookDir               = carToBall2;
    
    if (Vector3.Angle(carToBall, carToBall2) < 10f)
      camLookDir               = carToBall;

    var dot                    = Mathf.     Abs(Vector3.Dot(_curDir.normalized, camLookDir));
    var t                      = Math.      Max(0.2f, Mathf.Pow(dot, 10)) * LerpFactor * deltaTime;
    _curDir                    = Vector3.   Lerp(_curDir, camLookDir, t);

    var pos                    = carPos + (carToBall * CameraOffset.z) + (Vector3.up * CameraOffset.y);
    var rot                    = Quaternion.LookRotation(_curDir, Vector3.up);

    //_camera.transform.position = Vector3.SmoothDamp(_camera.transform.position, pos, ref _vel, LerpFactor * deltaTime);
    //_curPos = Vector3.SmoothDamp(_curPos, pos, ref _vel, LerpFactor * deltaTime);
    _curPos = Vector3.Lerp(_curPos, pos, LerpFactor * deltaTime);
    //_camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, rot, LerpFactor * deltaTime);
    _curRot = Quaternion.Slerp(_curRot, rot, LerpFactor * deltaTime);
  }
}
