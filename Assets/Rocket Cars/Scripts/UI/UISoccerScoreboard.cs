using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;

[ExecuteAfter(typeof(GameMode))]
public class UISoccerScoreboard : NetworkBehaviour
{
  [SerializeField]
  private float        _timeToHide = 0.3f;
  [SerializeField]
  private Vector2      _boxSize    = new(400, 400);

  private float        _timer;
  private Soccer       _soccer;
  private GameMode     _gm;

  private GUIStyle     _headerStyle;
  private GUIStyle     _playerStyle;
  private GUIStyle     _scoreStyle;
  private GUIStyle     _winStyle;

  private List<Player> _playersCache = new(6);

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _soccer      = GetComponent<Soccer>();
    _gm          = GetComponent<GameMode>();
    _headerStyle = null;
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode  || _soccer == null || _gm == null)
      return;

    if ((Sandbox.InputEnabled || _gm.GlobalInfo.IsReplay) && Sandbox.IsVisible)
    {
      if (Input.GetButton("Scoreboard"))
        _timer = _timeToHide;
    }

    if (_timer > 0)
      _timer -= Time.deltaTime;
  }

  private void OnGUI()
  {
    if (Application.isBatchMode)
      return;

      // keep showing if timer > 0 or if the match state is GameOver
    bool isGameOver = _soccer != null && _soccer.GameState == Soccer.State.GameOver;

    if ((_timer <= 0 && !isGameOver) || _soccer == null || _gm == null || !Sandbox.IsVisible || _soccer.GlobalInfo.HideUI)
      return;

    if (_headerStyle == null)
      InitStyles();

    float screenW    = Screen.width;
    float screenH    = Screen.height;
    float totalWidth = (_boxSize.x * 2) + 20;
    float startX     = (screenW - totalWidth) / 2f;
    float startY     = (screenH - _boxSize.y) / 2f;

    if (isGameOver)
    {
      string winText = "DRAW!";
      Color winColor = Color.white;

      if (_soccer.TeamRedGoals > _soccer.TeamBlueGoals)
        winText      = "RED TEAM WON!";
      else if (_soccer.TeamBlueGoals > _soccer.TeamRedGoals)
        winText      = "BLUE TEAM WON!";

      var oldColor   = GUI.color;
      GUI.color      = winColor;
      Rect winRect   = new Rect(startX, startY - 60, totalWidth, 50);
      GUI.Label(winRect, winText, _winStyle);
      GUI.color      = oldColor;
    }

    Rect redRect     = new Rect(startX, startY, _boxSize.x, _boxSize.y);
    Rect blueRect    = new Rect(startX + _boxSize.x + 20, startY, _boxSize.x, _boxSize.y);

    Color bgColor    = new Color(0, 0, 0, 0.3f);
    GUI.color        = bgColor;
    GUI.DrawTexture(redRect, Texture2D.whiteTexture);
    GUI.DrawTexture(blueRect, Texture2D.whiteTexture);
    GUI.color        = Color.white;

    GUILayout.BeginArea(new Rect(redRect.x + 10, redRect.y + 20, redRect.width - 20, redRect.height - 30));
    DrawTeam(Team.Red, Color.white, $"RED TEAM: {_soccer.TeamRedGoals}");
    GUILayout.EndArea();

    GUILayout.BeginArea(new Rect(blueRect.x + 10, blueRect.y + 20, blueRect.width - 20, blueRect.height - 30));
    DrawTeam(Team.Blue, Color.white, $"BLUE TEAM: {_soccer.TeamBlueGoals}");
    GUILayout.EndArea();

    if (_soccer.GameState == Soccer.State.GameOver)
    {
      var timeToStart = Mathf.Max(0f, _soccer.DelayUntilRestart - Sandbox.TickToTime(Sandbox.Tick - _soccer.TransitionTick));
      Rect footerRect = new Rect(startX, startY + _boxSize.y + 20, totalWidth, 40);
      GUI.Label(footerRect, $"Next game starts in: {(int)timeToStart}".ToUpper(), _scoreStyle);
    }
  }

  private void DrawTeam(Team team, Color color, string teamName)
  {
    foreach (var playerId in Sandbox.Players)
      if (Sandbox.TryGetPlayerObject(playerId, out Player player) && player.Team == team)
        _playersCache.Add(player);

    _playersCache.Sort((x, y) => y.Goals.CompareTo(x.Goals));

    var prevColor = GUI.color;
    GUI.color     = color;
    GUILayout.Label(teamName, _headerStyle);
    GUILayout.Space(5);

    float columnWidthName = 0.65f;
    float columnWidthScore = 0.35f;

    GUILayout.BeginHorizontal();
    GUILayout.Label("NAME", _headerStyle, GUILayout.Width(_boxSize.x * columnWidthName - 30));
    GUILayout.Label("SCORE", _headerStyle, GUILayout.Width(_boxSize.x * columnWidthScore - 30));
    GUILayout.EndHorizontal();

    GUILayout.Space(5);

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

  private void InitStyles()
  {
    _headerStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize  = 20,
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter
    };

    _playerStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize  = 16,
      alignment = TextAnchor.MiddleCenter
    };

    _scoreStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize  = 16,
      alignment = TextAnchor.MiddleCenter
    };

    _winStyle   = new GUIStyle(_headerStyle)
    {
      fontSize  = 40,
      fontStyle = FontStyle.Bold
    };
  }
}