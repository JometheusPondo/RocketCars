using Netick;
using Netick.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteBefore(typeof(GameMode))]
public class UISoccer : NetworkBehaviour
{
  [SerializeField]
  private CanvasGroup        _joinCanvasGroup;
  [SerializeField]
  private Button             _joinRedButton;
  [SerializeField]
  private Button             _joinBlueButton;

  [SerializeField]
  private TextMeshProUGUI    _timerText;
  [SerializeField]
  private TextMeshProUGUI    _roundStartTimer;
  [SerializeField]
  private Transform          _ballIndicator;

  [SerializeField]
  private TextMeshProUGUI    _goalScoredText;
  [SerializeField]
  private TextMeshProUGUI    _teamRedGoalsText;
  [SerializeField]
  private TextMeshProUGUI    _teamBlueGoalsText;

  [Header("Game Over Screen")]
  [SerializeField]
  private GameObject[]       _UIsToDisableAtGameOver;
  [SerializeField]
  private TextMeshProUGUI    _gameOverTimerText;
  [SerializeField]
  private TextMeshProUGUI    _winnerText;

  [Header("Audio")]
  [SerializeField]
  private AudioClip          _timerTickAudioClip;

  private float              _goalScoredTextShownTimer;
  private int                _previousTimerValue;
  private Soccer             _soccer;
  private Camera             _camera;
  private UISoccerScoreboard _UIScoreboard;
  private char[]             _textTimerBuffer = new char[16];

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _camera                              = Sandbox.FindObjectOfType<Camera>();
    _UIScoreboard                        = GetComponent<UISoccerScoreboard>();
    _soccer                              = GetComponent<Soccer>();
    _soccer.OnGoalsChangedEvent         += OnGoalsChanged;
    _soccer.OnRoundStartedEvent         += HideRoundStartTimer;
    _soccer.OnWaitingForRoundStartEvent += ShowRoundStartTimer;
    _soccer.OnWaitingForRoundStartEvent += OnGameStarted;

    _soccer.OnRoundStartedEvent         += OnGameStarted;
    _soccer.OnGameOverEvent             += OnGameEnded;

    _joinRedButton.onClick. AddListener(OnJoinRedPressed);
    _joinBlueButton.onClick.AddListener(OnJoinBluePressed);
  }

  private void OnGoalsChanged(Player lastGoalScorer, bool didScoreGoal)
  {
    _teamRedGoalsText.text = _soccer.TeamRedGoals.ToString();
    _teamBlueGoalsText.text = _soccer.TeamBlueGoals.ToString();

    if (_soccer.GameState == Soccer.State.GoalScored)
    {
      _goalScoredTextShownTimer = 5f;
      _goalScoredText.text = lastGoalScorer.Name + " Scored!";
    }
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode)
      return;

    if (_soccer.GameState == Soccer.State.GoalReplay || Sandbox.TryGetLocalPlayerObject(out Player localPlayer) && localPlayer.IsReady || Sandbox.IsReplay)
    {
      _joinCanvasGroup.interactable           = false;
      _joinCanvasGroup.alpha                  = 0f;
      _joinCanvasGroup.blocksRaycasts         = false;
    }

    float time                                = Sandbox.TickToTime(Sandbox.Tick - _soccer.RoundStartTick);
    TimeSpan timeSpan                         = TimeSpan.FromSeconds(time);

    if (timeSpan.TryFormat(_textTimerBuffer.AsSpan(), out int charsWritten, @"mm\:ss"))
      _timerText.SetText(_textTimerBuffer, 0, charsWritten);

    // if the time remaining is less than one second, get an alpha from the remaining time, otherwise use 1f alpha.
    var alpha                                 = _goalScoredTextShownTimer <= 1f ? Mathf.InverseLerp(0f, 1f, _goalScoredTextShownTimer) : 1f;
    _goalScoredText.color                     = Color.Lerp(new Color(1f, 1f, 1f, 0f), Color.white, alpha);
    _goalScoredTextShownTimer                 = Mathf.Max(0f, _goalScoredTextShownTimer - Time.deltaTime);


    if (_soccer.GameState == Soccer.State.WaitingForRoundStart)
    {
      var timeToStart                         = (int)(Mathf.Max(0f, _soccer.DelayUntilRoundStart - Sandbox.TickToTime(Sandbox.Tick - _soccer.TransitionTick)) + 1f);
      _roundStartTimer.text                   = timeToStart.ToString();

      if (_previousTimerValue != (int)timeToStart)
        _soccer.AudioSource.NetworkPlayOneShot(Sandbox, _timerTickAudioClip);

      _previousTimerValue                     = (int)timeToStart;
    }

    if (_soccer.GameState == Soccer.State.GameOver)
    {
      var timeToStart                         = Mathf.Max(0f, _soccer.DelayUntilRestart - Sandbox.TickToTime(Sandbox.Tick - _soccer.TransitionTick));
      _gameOverTimerText.text                 = $"Next game starts in {(int)timeToStart} seconds";
      _winnerText.text                        = $"{_soccer.WinnerTeam} Team Won";
    }

    UpdateBallIndicator();
  }

  private void UpdateBallIndicator()
  {
    var ball                                  = _soccer.Ball.transform.position;
    // in player perspective.
    var fromCameraToBall                      = _camera.transform.InverseTransformPoint(ball);
    fromCameraToBall.y                        = 0;
    var normalizedPos                         = new Vector3(fromCameraToBall.x, fromCameraToBall.z, 0);
    _ballIndicator.transform.localEulerAngles = Vector3.forward * ((Mathf.Atan2(normalizedPos.y, normalizedPos.x) * Mathf.Rad2Deg) + -90);
  }

  void OnGameEnded()
  {
    _winnerText.       SetEnabled(Sandbox, true);
    _gameOverTimerText.SetEnabled(Sandbox, true);

    if (Sandbox.StartMode == NetickStartMode.ReplayClient)
      return;

    foreach (var ui in _UIsToDisableAtGameOver)
      ui.SetActive(false);

    _UIScoreboard?.    ShowScoreboard();
  }

  void OnGameStarted()
  {
    _winnerText.SetEnabled(Sandbox, false);
    _gameOverTimerText.SetEnabled(Sandbox, false);

    if (Sandbox.StartMode == NetickStartMode.ReplayClient)
      return;

    foreach (var ui in _UIsToDisableAtGameOver)
      ui.SetActive(true);
  }

  private void ShowRoundStartTimer()          => _roundStartTimer.SetEnabled(Sandbox, true);
  private void HideRoundStartTimer()          => _roundStartTimer.SetEnabled(Sandbox, false);
  private void OnJoinRedPressed()             => _soccer.RPC_EnterGame(Team.Red,  _soccer.GlobalInfo.PlayerName);
  private void OnJoinBluePressed()            => _soccer.RPC_EnterGame(Team.Blue, _soccer.GlobalInfo.PlayerName);
}
