using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;
using Netick;
using Netick.Unity;

[ExecutionOrder(-10)]
public class UIGoalReplay : NetworkBehaviour
{
  [SerializeField]
  private GameObject        _replayUI;
  [SerializeField]
  private Image             _circleIcon;
  [SerializeField]
  private TextMeshProUGUI   _skipersText;
  [SerializeField]
  private TextMeshProUGUI   _scorerText;

  private Vignette          _vignetteEffect;
  private Grain             _grainEffect;
  private Soccer            _soccer;
  private UICarHUD          _UICarHUD;
  private Graphic[]         _UIElements;

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _UIElements                      = _replayUI.GetComponentsInChildren<Graphic>(true);
    _UICarHUD                        = Sandbox.FindObjectOfType<UICarHUD>();
    _soccer                          = GetComponent<Soccer>();
    _soccer.OnGameStateChangedEvent += OnGameStateChanged;
    var volume                       = Sandbox.FindObjectOfType<PostProcessVolume>();
    volume.profile.TryGetSettings(out _vignetteEffect);
    volume.profile.TryGetSettings(out _grainEffect);
  }

  private void OnGameStateChanged(OnChangedData dat)
  {
    // entering replay
    if (_soccer.GameState == Soccer.State.GoalReplay)
    {
      _vignetteEffect.active = true;
      _grainEffect.active    = true;

      if (!Sandbox.IsReplay)
      {
        _UICarHUD.SetVisibility(false); // hide car HUD when entering replay.

        // show replay UI.
        foreach (var element in _UIElements)
          element.SetEnabled(Sandbox, true);
      }        
    }

    // exiting replay (we can know if we are exiting replay by checking the previous value of _soccer.GameState and see if it was equal to Replay).
    else if (dat.GetPreviousValue<Soccer.State>() == Soccer.State.GoalReplay)
    {
      _vignetteEffect.active = false;
      _grainEffect.active    = false;
      if (!Sandbox.IsReplay)
      {
        _UICarHUD.SetVisibility(true); // show car HUD when exiting goal replay.

        // hide replay UI.
        foreach (var element in _UIElements)
          element.SetEnabled(Sandbox, false);
      }
    }
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode)
      return;

    if (_soccer.GameState == Soccer.State.GoalReplay)
    {
      _circleIcon.color = Color.Lerp(new Color(0.5f, 0f, 0f, 1f), Color.red, Mathf.InverseLerp(-1f, 1f, Mathf.Sin(25f * Time.unscaledTime)));
      _skipersText.text = $"{_soccer.GoalReplaySkipersCount}/{_soccer.ActivePlayersCount}";
      _scorerText.text  = _soccer.LastGoalScorer.GetBehaviour(Sandbox).Name;
    }
  }
}
