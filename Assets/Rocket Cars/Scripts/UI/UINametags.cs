using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;

[ExecuteBefore(typeof(GameMode))]
public class UINametags : NetworkBehaviour
{
  public float      NametagHeight = 5f;
  public float      FovLimit      = 70f;
  public int        FontSize      = 16; 
  public float      LabelWidth    = 150f;
  public float      LabelHeight   = 25f;

  private GameMode  _gm;
  private Camera    _camera;
  private GUIStyle  _redPlayerNametagStyle;
  private GUIStyle  _bluePlayerNametagStyle;
  private GUIStyle  _boxStyle;
  private Texture2D _boxBackgroundTexture;

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _gm                     = GetComponent<GameMode>();
    _camera                 = _gm.GlobalData.Camera;
    _redPlayerNametagStyle  = null;
    _bluePlayerNametagStyle = null;
  }

  private void OnGUI()
  {
    if (Application.isBatchMode || _gm == null || _camera == null)
      return;

    if (!Sandbox.IsVisible || Sandbox.IsRunning == false || _gm.GlobalData.HideUI)
      return;

    float scaleFactor  = Screen.height / 1080f;
    int scaledFontSize = Mathf.RoundToInt(FontSize * scaleFactor);

    if (_redPlayerNametagStyle == null)
    {
      _redPlayerNametagStyle = new GUIStyle(GUI.skin.label)
      {
        alignment = TextAnchor.MiddleCenter,
        fontStyle = FontStyle.Bold,
        normal    = { textColor = new Color(1.00f, 0.70f, 0.70f, 1f) },
      };

      _bluePlayerNametagStyle        = new GUIStyle(_redPlayerNametagStyle);
      _bluePlayerNametagStyle.normal = new GUIStyleState { textColor = new Color(0.68f, 0.85f, 0.90f, 1f) };

      _boxBackgroundTexture          = new Texture2D(1, 1);
      _boxBackgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.2f));
      _boxBackgroundTexture.Apply();

      _boxStyle = new GUIStyle
      {
        normal = { background = _boxBackgroundTexture },
      };
    }

    _redPlayerNametagStyle.fontSize  = scaledFontSize;
    _bluePlayerNametagStyle.fontSize = scaledFontSize;

    foreach (var playerId in Sandbox.Players)
    {
      Sandbox.TryGetPlayerObject(playerId, out Player player);

      if (player == null || player.Car == null)
        continue;
      if (Sandbox.LocalPlayer == player.InputSource)
        continue;

      var carRenderPos           = player.Car.NetworkRigidbody.RenderTransform.transform.position;
      var angle                  = Vector3.Angle(_camera.transform.forward, (carRenderPos - _camera.transform.position).normalized);

      if (angle < FovLimit)
      {
        Vector3 screenPoint      = _camera.WorldToScreenPoint(carRenderPos);
        float scaledHeightOffset = NametagHeight * scaleFactor;
        float scaledWidth        = LabelWidth * scaleFactor;
        float scaledLabelHeight  = LabelHeight * scaleFactor;
        float x                  = screenPoint.x;
        float y                  = Screen.height - screenPoint.y - scaledHeightOffset;
        Rect nametagRect         = new Rect(x - (scaledWidth / 2f), y - (scaledLabelHeight / 2f), scaledWidth, scaledLabelHeight);
        GUI.Box(nametagRect, GUIContent.none, _boxStyle);
        GUI.Label(nametagRect, player.Name, player.Team == Team.Red ? _redPlayerNametagStyle : _bluePlayerNametagStyle);
      }
    }
  }
}