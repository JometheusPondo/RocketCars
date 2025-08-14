using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Netick;
using Netick.Unity;

[ExecuteAfter(typeof(GameMode))]
public class UISoccerScoreboard : NetworkBehaviour
{
  [SerializeField]
  public Transform                            _teamRedScoreboard;
  [SerializeField]
  public Transform                            _teamBlueScoreboard;

  [SerializeField]
  public float                                _scoreOffset        = 5;
  [SerializeField]
  public GameObject                           _playerScorePrefab;

  [SerializeField]
  private float                               _timeToHide         = 0.3f;
  private float                               _timer;

  private List<UISoccerScoreboardPlayerScore> _redPlayersScores   = new(10);
  private List<UISoccerScoreboardPlayerScore> _bluePlayersScores  = new(10);
  private Stack<UISoccerScoreboardPlayerScore>_scoresPool         = new(6);

  private UISoccerScoreboardPlayerScore       _teamRedCaption;
  private UISoccerScoreboardPlayerScore       _teamBlueCaption;
  private Soccer                              _soccer;

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _teamRedCaption                           = _teamRedScoreboard. GetComponentInChildren<UISoccerScoreboardPlayerScore>();
    _teamBlueCaption                          = _teamBlueScoreboard.GetComponentInChildren<UISoccerScoreboardPlayerScore>();

    _soccer                                   = GetComponent<Soccer>();
    _soccer.OnRoundEndedEvent                += ShowScoreboard;
    _soccer.OnPlayerAddedEvent               += OnPlayerAdded;
    _soccer.OnPlayerRemovedEvent             += OnPlayerRemoved;

    var canvas                                = GetComponentInChildren<Canvas>().transform;

    for (int i = 0; i < Sandbox.Config.MaxPlayers; i++)
    {
      var score                               = Sandbox.Instantiate(_playerScorePrefab, default, Quaternion.identity).GetComponent<UISoccerScoreboardPlayerScore>();
      score.transform.SetParent(canvas, false);
      score.gameObject.SetActive(false);
      _scoresPool.     Push(score);
    }
  }

  public void OnPlayerAdded(Player player)
  {
    Transform                                 scoreboard;
    List<UISoccerScoreboardPlayerScore>       playerToScores;
        
    scoreboard                                = player.Team == Team.Red ? _teamRedScoreboard : _teamBlueScoreboard;
    playerToScores                            = player.Team == Team.Red ? _redPlayersScores  : _bluePlayersScores;

    var newScore                              = _scoresPool.Pop();;
    newScore.gameObject.SetActive(true);
    newScore.transform.SetParent(scoreboard, false);
    newScore.transform.localScale             = Vector3.one;
    newScore.transform.localPosition          = playerToScores.Count * Vector3.down * _scoreOffset;
    newScore.Init(player);

    playerToScores.Add(newScore);
  }

  public void OnPlayerRemoved(Player player)
  {
    List<UISoccerScoreboardPlayerScore> playerToScores;

    if (player.Team == Team.Red)
      playerToScores = _redPlayersScores;
    else
      playerToScores = _bluePlayersScores;

    var score        = playerToScores.Find(x => x.Player == player);
    playerToScores.  Remove(score);
    score.gameObject.SetActive(false);
    _scoresPool.     Push(score);
  }

  public override void NetworkRender()
  {
    if (_soccer == null)
      return;

    // show the scoreboard when the player presses tab, or when the game has ended.
    if (Sandbox.InputEnabled)
    {
      if (Input.GetButtonDown("Scoreboard"))
        ShowScoreboard();

      if (Input.GetButton("Scoreboard") || _soccer.GameState == Soccer.State.GameOver)
        _timer = _timeToHide;
    }

    // if scoreboard is shown
    if (_timer > 0)
    {
      SortScores(_redPlayersScores);
      SortScores(_bluePlayersScores);

      var newTime = _timer - UnityEngine.Time.deltaTime;

      // we hide it when showing time is over.
      if (newTime <= 0)
        SetVisibility(false);

      _timer      = newTime;
    }
  }

  /// <summary>
  /// Sorts the scores in a descending order based on goals. 
  /// </summary>
  public void SortScores(List<UISoccerScoreboardPlayerScore> scores)
  {
    scores.Sort((x, y) => y.Player.Goals.CompareTo(x.Player.Goals));

    for (int i = 0; i < scores.Count; i++)
      scores[i].transform.localPosition = i * Vector3.down * _scoreOffset;
  }

  public void ShowScoreboard()
  {
    SetVisibility(true);
  }

  /// <summary>
  /// Show/hides the scoreboard.
  /// </summary>
  private void SetVisibility(bool visibility)
  {
    _teamBlueCaption.       SetVisibility(Sandbox, visibility);
    _teamRedCaption.        SetVisibility(Sandbox, visibility);

    for (int i = 0; i < _redPlayersScores.Count; i++)
      _redPlayersScores[i]. SetVisibility(Sandbox, visibility);

    for (int i = 0; i < _bluePlayersScores.Count; i++)
      _bluePlayersScores[i].SetVisibility(Sandbox, visibility);
  }
}
