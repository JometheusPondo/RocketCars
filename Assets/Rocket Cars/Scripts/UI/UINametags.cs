using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;

[ExecuteBefore(typeof(GameMode))]
public class UINametags : NetworkBehaviour
{
  public float        NametagHeight = 5f;

  private GameMode    _gameMode;
  private Camera      _camera;
  private GUIStyle    _nametagStyle;
  private GUIStyle    _boxStyle;
  private Texture2D   _boxBackgroundTexture;

  private const float FovLimit = 70f;

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _gameMode = GetComponent<GameMode>();
    _camera = Sandbox.FindObjectOfType<Camera>();
    _nametagStyle = null;
  }

  private void OnGUI()
  {
    if (Application.isBatchMode || _gameMode == null || _camera == null)
      return;

    if (!Sandbox.IsVisible || Sandbox.IsRunning == false)
      return;

    if (_nametagStyle == null)
    {
      _nametagStyle = new GUIStyle(GUI.skin.label)
      {
        alignment = TextAnchor.MiddleCenter,
        fontSize  = 16,
        fontStyle = FontStyle.Bold,
        normal    = { textColor = Color.white },
      };

      _boxBackgroundTexture = new Texture2D(1, 1);
      _boxBackgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.3f)); 
      _boxBackgroundTexture.Apply();

      _boxStyle = new GUIStyle
      {
        normal  = { background = _boxBackgroundTexture },
        border  = new RectOffset(0, 0, 0, 0),
        padding = new RectOffset(0, 0, 0, 0),
      };
    }

    foreach (var player in _gameMode.ActivePlayers)
    {
      if (Sandbox.GetBehaviour(player) == null)
        continue;
      // skip the local player's nametag
      if (Sandbox.LocalPlayer == Sandbox.GetBehaviour(player).InputSource)
        continue;

      if (Sandbox.GetBehaviour(player).Car == null)
        continue;

      var carRenderPos = Sandbox.GetBehaviour(player).Car.NetworkRigidbody.RenderTransform.transform.position;
      var angle        = Vector3.Angle(_camera.transform.forward, (carRenderPos - _camera.transform.position).normalized);

      if (angle < FovLimit)
      {
        Vector3 screenPoint = _camera.WorldToScreenPoint(carRenderPos);

        float x             = screenPoint.x;
        float y             = Screen.height - screenPoint.y - NametagHeight;

        float labelWidth    = 150f;
        float labelHeight   = 25f;

        Rect nametagRect    = new Rect(x - (labelWidth / 2f), y - (labelHeight / 2f), labelWidth, labelHeight);
        GUI.Box(nametagRect, GUIContent.none, _boxStyle);
        GUI.Label(nametagRect, Sandbox.GetBehaviour(player).Name, _nametagStyle);
      }
    }
  }
}
