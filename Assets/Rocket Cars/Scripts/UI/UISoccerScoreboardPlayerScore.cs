using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Netick;
using Netick.Unity;
using System.Text;

public class UISoccerScoreboardPlayerScore : MonoBehaviour
{
  public Player           Player { get; private set; }

  [SerializeField]
  private TextMeshProUGUI _playerName;
  [SerializeField]
  private TextMeshProUGUI _goals;

  private RawImage        _background;

  private StringBuilder   _playerNameCache = new(60);
  private StringBuilder   _goalsCache      = new(60);

  private void Awake()
  {
    _background      = GetComponent<RawImage>();
  }

  public void Init(Player player)
  {
    Player           = player;
  }

  /// <summary>
  /// Show/hides the score.
  /// </summary>
  public void SetVisibility(NetworkSandbox sandbox, bool visibility)
  {
    _playerName.SetEnabled(sandbox, visibility);
    _goals.     SetEnabled(sandbox, visibility);

    _background.SetEnabled(sandbox, visibility);
  }

  private void Update()
  {
    if (Player != null)
    {
      _goalsCache.Clear();
      _goalsCache.Append(Player.Goals);

      Player.Name.LoadIntoStringBuilder(_playerNameCache);

      _playerName.SetText(_playerNameCache);
      _goals.SetText(_goalsCache);
    }
  }
}

