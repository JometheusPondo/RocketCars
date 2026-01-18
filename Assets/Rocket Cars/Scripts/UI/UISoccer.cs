using Netick;
using Netick.Unity;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteBefore(typeof(GameMode))]
public class UISoccer : NetworkBehaviour
{
  [SerializeField]
  private CanvasGroup     _joinCanvasGroup;
  [SerializeField]
  private Button          _joinRedButton;
  [SerializeField]
  private Button          _joinBlueButton;

  [SerializeField]
  private TextMeshProUGUI _redTeamCountText;
  [SerializeField]
  private TextMeshProUGUI _blueTeamCountText;

  [SerializeField]
  private TextMeshProUGUI _timerText;
  [SerializeField]
  private TextMeshProUGUI _roundStartTimer;

  [SerializeField]
  private TextMeshProUGUI _goalScoredText;
  [SerializeField]
  private TextMeshProUGUI _teamRedGoalsText;
  [SerializeField]
  private TextMeshProUGUI _teamBlueGoalsText;

  [Header("Ball Indicator")]
  [SerializeField]
  private Texture2D       _indicatorIcon;
  [SerializeField]
  private float           _iconSize      = 40f;
  [SerializeField]
  private float           _screenPadding = 30f;

  [Header("Scoreboard")]
  [SerializeField]
  private float           _timeToHide    = 0.3f;
  [SerializeField]
  private Vector2         _boxSize       = new(400, 400);

  [Header("Audio")]
  [SerializeField]
  private AudioClip       _timerTickAudioClip;

  private Soccer          _soccer;
  private Camera          _camera;
  private float           _goalScoredTextShownTimer;
  private int             _previousTimerValue;
  private char[]          _textTimerBuffer = new char[16];
  private float           _sooreboardTimer;
  private GUIStyle        _headerStyle;
  private GUIStyle        _playerStyle;
  private GUIStyle        _scoreStyle;
  private GUIStyle        _winStyle;
  private List<Player>    _playersCache    = new(6);

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _headerStyle                 = null;
    _camera                      = Sandbox.GetComponent<GlobalData>().Camera;
    _soccer                      = GetComponent<Soccer>();
    _soccer.OnGoalsChangedEvent += OnGoalsChanged;

    _joinRedButton.onClick.AddListener(()  => _soccer.RPC_Join(Team.Red, _soccer.GlobalData.LocalPlayerName));
    _joinBlueButton.onClick.AddListener(() => _soccer.RPC_Join(Team.Blue, _soccer.GlobalData.LocalPlayerName));
  }
  
  private void OnGoalsChanged(Player lastGoalScorer, bool didScoreGoal)
  {
    _teamRedGoalsText.text  = _soccer.TeamRedGoals.ToString();
    _teamBlueGoalsText.text = _soccer.TeamBlueGoals.ToString();

    if (_soccer.GameState == Soccer.State.GoalScored)
    {
      _goalScoredTextShownTimer = 5f;
      _goalScoredText.text = lastGoalScorer.Name + "\nSCORED!";
    }
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode) 
      return;

    Sandbox.TryGetPlayerObject(_soccer.GlobalData.IsReplay ? _soccer.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId, out Player player);

    if (_soccer.GameState != Soccer.State.GoalReplay && player == null && _joinCanvasGroup.gameObject.activeInHierarchy == false)
      _joinCanvasGroup.gameObject.SetActive(true);
    if (_soccer.GameState == Soccer.State.GoalReplay || player != null && player.IsReady || _soccer.GlobalData.IsReplay)
      _joinCanvasGroup.gameObject.SetActive(false);

    if (_soccer.GameState == Soccer.State.WaitingForRoundStart && _roundStartTimer.enabled == false)
      _roundStartTimer.SetEnabled(Sandbox, true);
    if (_soccer.GameState != Soccer.State.WaitingForRoundStart && _roundStartTimer.enabled == true)
      _roundStartTimer.SetEnabled(Sandbox, false);
    if (_soccer.GlobalData.IsReplay && player == null && _roundStartTimer.enabled)
      _roundStartTimer.SetEnabled(Sandbox, false);

    if (player == null)
    {
      var playersPerTeam       = Sandbox.Config.MaxPlayers / 2;
      var redTeamPlayersCount  = _soccer.GetTeamSize(Team.Red);
      var blueTeamPlayersCount = _soccer.GetTeamSize(Team.Blue);
      _redTeamCountText.text   = $"{redTeamPlayersCount. ToString()}/{playersPerTeam} {(redTeamPlayersCount  == playersPerTeam ? "(FULL)" : "")}";
      _blueTeamCountText.text  = $"{blueTeamPlayersCount.ToString()}/{playersPerTeam} {(blueTeamPlayersCount == playersPerTeam ? "(FULL)" : "")}";
    }

    float time                 = _soccer.GameState == Soccer.State.Started ? Sandbox.TickToTime(Sandbox.Tick - _soccer.RoundStartTick) : 0f;
    TimeSpan timeSpan          = TimeSpan.FromSeconds(time);

    if (timeSpan.TryFormat(_textTimerBuffer.AsSpan(), out int charsWritten, @"mm\:ss"))
      _timerText.SetText(_textTimerBuffer, 0, charsWritten);

    var alpha                  = _goalScoredTextShownTimer <= 1f ? Mathf.InverseLerp(0f, 1f, _goalScoredTextShownTimer) : 1f;
    _goalScoredText.color      = Color.Lerp(new Color(1f, 1f, 1f, 0f), Color.white, alpha);
    _goalScoredTextShownTimer  = Mathf.Max(0f, _goalScoredTextShownTimer - Time.deltaTime);

    if (_soccer.GameState == Soccer.State.WaitingForRoundStart)
    {
      var timeToStart          = (int)(Mathf.Max(0f, _soccer.DelayUntilRoundStart - Sandbox.TickToTime(Sandbox.Tick - _soccer.TransitionTick)) + 1f);
      _roundStartTimer.text    = timeToStart.ToString();

      if (_previousTimerValue != (int)timeToStart)
        _soccer.AudioSource.NetworkPlayOneShot(Sandbox, _timerTickAudioClip);

      _previousTimerValue      = (int)timeToStart;
    }
  }

  private void OnGUI()
  {
    if (Application.isBatchMode)
      return;

    if (_soccer == null || !Sandbox.IsRunning ||  !Sandbox.IsVisible || _soccer.GlobalData.HideUI)
      return;

    if (_soccer.GameState == Soccer.State.Started)
      if (Sandbox.TryGetPlayerObject(_soccer.GlobalData.IsReplay ? _soccer.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId, out Player player))
        DrawBallIndicator();

    DrawScoreboard();
  }

  void DrawBallIndicator()
  {
    float referenceHeight = 1080f;
    float scaleFactor     = Screen.height / referenceHeight;
    float scaledSize      = _iconSize * scaleFactor;
    float scaledPadding   = _screenPadding * scaleFactor;
    Vector3 ballPos       = _soccer.Ball.NetworkRigidbody.RenderTransform.position;
    Vector3 viewportPos   = _camera.WorldToViewportPoint(ballPos);
    bool isBehind         = viewportPos.z < 0;
    bool isOnScreen       = !isBehind && viewportPos.x > 0 && viewportPos.x < 1 && viewportPos.y > 0 && viewportPos.y < 1;

    if (isOnScreen) 
      return;

    Vector3 centerOffset  = viewportPos - new Vector3(0.5f, 0.5f, 0);
    if (isBehind) 
      centerOffset        = -centerOffset;

    float angle           = Mathf.Atan2(centerOffset.y, centerOffset.x) * Mathf.Rad2Deg;
    float divX            = Mathf.Max(Mathf.Abs(centerOffset.x), 0.001f);
    float divY            = Mathf.Max(Mathf.Abs(centerOffset.y), 0.001f);
    float scale           = Mathf.Min(0.5f / divX, 0.5f / divY);

    Vector3 clampedPos    = centerOffset * scale;
    clampedPos           += new Vector3(0.5f, 0.5f, 0);

    float screenX         = clampedPos.x * Screen.width;
    float screenY         = clampedPos.y * Screen.height;
    screenX               = Mathf.Clamp(screenX, scaledPadding, Screen.width - scaledPadding);
    screenY               = Mathf.Clamp(screenY, scaledPadding, Screen.height - scaledPadding);
    float guiY            = Screen.height - screenY;
    Rect rect             = new Rect(screenX - scaledSize / 2, guiY - scaledSize / 2, scaledSize, scaledSize);
    Matrix4x4 oldMatrix   = GUI.matrix;
    Vector2 pivotPoint    = new Vector2(screenX, guiY);
    GUIUtility.RotateAroundPivot(-angle + 90, pivotPoint);
    GUI.DrawTexture(rect, _indicatorIcon);
    GUI.matrix            = oldMatrix;
  }

  void DrawScoreboard()
  {
    if ((_soccer.GlobalData.CanUseInput || _soccer.GlobalData.IsReplay) && Sandbox.IsVisible)
    {
      if (Input.GetButton("Scoreboard"))
        _sooreboardTimer = _timeToHide;
    }

    if (_sooreboardTimer > 0)
      _sooreboardTimer -= Time.deltaTime;

    // keep showing if timer > 0 or if the match state is GameOver
    bool isGameOver = _soccer != null && _soccer.GameState == Soccer.State.GameOver;

    if ((_sooreboardTimer <= 0 && !isGameOver) || !Sandbox.IsVisible || _soccer.GlobalData.HideUI)
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

    float columnWidthName  = 0.65f;
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