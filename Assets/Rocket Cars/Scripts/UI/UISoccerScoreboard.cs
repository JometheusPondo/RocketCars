using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;

[ExecuteAfter(typeof(GameMode))]
public class UISoccerScoreboard : NetworkBehaviour
{
  [SerializeField] 
  private float        _timeToHide   = 0.3f;
  [SerializeField] 
  private Vector2      _boxSize      = new(400, 400);

  private float        _timer;
  private Soccer       _soccer;
  private GameMode     _gameMode;
  private GUIStyle     _headerStyle;
  private GUIStyle     _playerStyle;
  private GUIStyle     _scoreStyle;
  private List<Player> _playersCache = new(6);

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _soccer      = GetComponent<Soccer>();
    _gameMode    = GetComponent<GameMode>();
    _headerStyle = null;
  }

  public override void NetworkRender()
  {
    if (_soccer == null || _gameMode == null)
      return;

    if ((Sandbox.InputEnabled || Sandbox.IsReplay) && Sandbox.IsVisible)
    {
      if (Input.GetButtonDown("Scoreboard"))
        ShowScoreboard();

      if (Input.GetButton("Scoreboard") || _soccer.GameState == Soccer.State.GameOver)
        _timer = _timeToHide;
    }

    if (_timer > 0)
      _timer -= Time.deltaTime;
  }

  public void ShowScoreboard()
  {
    _timer = _timeToHide;
  }

  private void OnGUI()
  {
    if (_timer <= 0 || _soccer == null || _gameMode == null)
      return;

    if (_headerStyle == null)
    {
      _headerStyle   = new GUIStyle(GUI.skin.label)
      {
        fontSize     = 20,
        fontStyle    = FontStyle.Bold,
        alignment    = TextAnchor.MiddleCenter
      };

      _playerStyle = new GUIStyle(GUI.skin.label)
      {
        fontSize     = 16,
        alignment    = TextAnchor.MiddleCenter
      };

      _scoreStyle    = new GUIStyle(GUI.skin.label)
      {
        fontSize     = 16,
        alignment    = TextAnchor.MiddleCenter
      };
    }

    float screenW    = Screen.width;
    float screenH    = Screen.height;
    float totalWidth = (_boxSize.x * 2) + 20; 
    float startX     = (screenW - totalWidth) / 2f;
    float startY     = (screenH - _boxSize.y) / 2f;

    Rect redRect     = new Rect(startX, startY, _boxSize.x, _boxSize.y);
    Rect blueRect    = new Rect(startX + _boxSize.x + 20, startY, _boxSize.x, _boxSize.y);

    Color bgColor    = new Color(0, 0, 0, 0.4f); 
    GUI.color        = bgColor;
    GUI.DrawTexture(redRect, Texture2D.whiteTexture);
    GUI.DrawTexture(blueRect, Texture2D.whiteTexture);
    GUI.color        = Color.white;

    GUILayout.BeginArea(new Rect(redRect.x + 10, redRect.y + 20, redRect.width - 20, redRect.height - 30));
    DrawTeam(Team.Red, Color.white, "RED TEAM");
    GUILayout.EndArea();

    GUILayout.BeginArea(new Rect(blueRect.x + 10, blueRect.y + 20, blueRect.width - 20, blueRect.height - 30));
    DrawTeam(Team.Blue, Color.white, "BLUE TEAM");
    GUILayout.EndArea();
  }

  private void DrawTeam(Team team, Color color, string teamName)
  {
    foreach (var playerId in Sandbox.Players)
      if (Sandbox.TryGetPlayerObject(playerId, out Player player) && player.Team == team)
        _playersCache.Add(player);

    _playersCache.Sort((x, y) => y.Goals.CompareTo(x.Goals));

    var prevColor = GUI.color;
    GUI.color     = color;

    // team title
    GUILayout.Label(teamName, _headerStyle);
    GUILayout.Space(5);

    float columnWidthName  = 0.65f;
    float columnWidthScore = 0.35f;

    // header row
    GUILayout.BeginHorizontal();
    GUILayout.Label("NAME", _headerStyle, GUILayout.Width(_boxSize.x * columnWidthName - 30));
    GUILayout.Label("SCORE", _headerStyle, GUILayout.Width(_boxSize.x * columnWidthScore - 30));
    GUILayout.EndHorizontal();

    GUILayout.Space(5);

    // player rows
    foreach (var p in _playersCache)
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label(p.Name.ToString(), _playerStyle, GUILayout.Width(_boxSize.x * columnWidthName - 30));
      GUILayout.Label(p.Goals.ToString().ToUpper(), _scoreStyle, GUILayout.Width(_boxSize.x * columnWidthScore - 30));
      GUILayout.EndHorizontal();
    }

    GUI.color = prevColor;
    _playersCache.Clear();
  }
}
