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
  private TextMeshProUGUI    _redTeamCountText;
  [SerializeField]
  private TextMeshProUGUI    _blueTeamCountText;

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

  [Header("Audio")]
  [SerializeField]
  private AudioClip          _timerTickAudioClip;

  private float              _goalScoredTextShownTimer;
  private int                _previousTimerValue;
  private Soccer             _soccer;
  private Camera             _camera;
  private char[]             _textTimerBuffer = new char[16];

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _camera                              = Sandbox.FindObjectOfType<Camera>();
    _soccer                              = GetComponent<Soccer>();
    _soccer.OnGoalsChangedEvent         += OnGoalsChanged;

    _joinRedButton.onClick. AddListener(() => _soccer.RPC_Join(Team.Red,  _soccer.GlobalInfo.LocalPlayerName));
    _joinBlueButton.onClick.AddListener(() => _soccer.RPC_Join(Team.Blue, _soccer.GlobalInfo.LocalPlayerName));
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode)
      return;

    Sandbox.TryGetPlayerObject(_soccer.GlobalInfo.IsReplay ? _soccer.SpectatedPlayer : Sandbox.LocalPlayer.PlayerId, out Player player);
  
    if (_soccer.GameState != Soccer.State.GoalReplay && player == null && _joinCanvasGroup.gameObject.activeInHierarchy == false)
      _joinCanvasGroup.gameObject.SetActive(true);
    if (_soccer.GameState == Soccer.State.GoalReplay || player != null && player.IsReady || _soccer.GlobalInfo.IsReplay)
      _joinCanvasGroup.gameObject.SetActive(false);

    if (_soccer.GameState == Soccer.State.WaitingForRoundStart && _roundStartTimer.enabled == false)
      _roundStartTimer.SetEnabled(Sandbox, true);
    if (_soccer.GameState != Soccer.State.WaitingForRoundStart && _roundStartTimer.enabled == true)
      _roundStartTimer.SetEnabled(Sandbox, false);
    if (_soccer.GlobalInfo.IsReplay && player == null && _roundStartTimer.enabled)
      _roundStartTimer.SetEnabled(Sandbox, false);

    if (player == null)
    {
      var playersPerTeam                      = Sandbox.Config.MaxPlayers / 2;
      var redTeamPlayersCount                 = _soccer.GetTeamSize(Team.Red);
      var blueTeamPlayersCount                = _soccer.GetTeamSize(Team.Blue);
      _redTeamCountText.text                  = $"{redTeamPlayersCount. ToString()}/{playersPerTeam} {(redTeamPlayersCount  == playersPerTeam ? "(FULL)" : "")}";
      _blueTeamCountText.text                 = $"{blueTeamPlayersCount.ToString()}/{playersPerTeam} {(blueTeamPlayersCount == playersPerTeam ? "(FULL)" : "")}";
    }

    float time                                = _soccer.GameState == Soccer.State.Started ? Sandbox.TickToTime(Sandbox.Tick - _soccer.RoundStartTick) : 0f;
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

  private void OnGoalsChanged(Player lastGoalScorer, bool didScoreGoal)
  {
    _teamRedGoalsText.text                    = _soccer.TeamRedGoals.ToString();
    _teamBlueGoalsText.text                   = _soccer.TeamBlueGoals.ToString();

    if (_soccer.GameState == Soccer.State.GoalScored)
    {
      _goalScoredTextShownTimer               = 5f;
      _goalScoredText.text                    = lastGoalScorer.Name + "\nSCORED!";
    }
  }
}
