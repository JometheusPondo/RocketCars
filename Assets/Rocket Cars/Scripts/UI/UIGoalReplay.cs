using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;
using Netick;
using Netick.Unity;
using UnityEngine.Serialization;

[ExecutionOrder(-10)]
public class UIGoalReplay : NetworkBehaviour
{
  [SerializeField]
  private GameObject        _container;
  [SerializeField]
  private Image             _circleIcon;
  [SerializeField]
  private TextMeshProUGUI   _skipersText;
  [SerializeField]
  private TextMeshProUGUI   _scorerText;

  private Vignette          _vignetteEffect;
  private Grain             _grainEffect;
  private Soccer            _soccer;

  public override void NetworkStart()
  {
    if (Application.isBatchMode)
      return;

    _soccer                          = GetComponent<Soccer>();
    var volume                       = Sandbox.FindObjectOfType<PostProcessVolume>();
    volume.profile.TryGetSettings(out _vignetteEffect);
    volume.profile.TryGetSettings(out _grainEffect);
  }

  public override void NetworkRender()
  {
    if (Application.isBatchMode)
      return;

    SetState(_soccer.GameState == Soccer.State.GoalReplay && !_soccer.GlobalInfo.HideUI);

    if (_soccer.GameState == Soccer.State.GoalReplay)
    {
      _circleIcon.color = Color.Lerp(new Color(0.5f, 0f, 0f, 1f), Color.red, Mathf.InverseLerp(-1f, 1f, Mathf.Sin(25f * Time.unscaledTime)));
      _skipersText.text = $"{_soccer.GoalReplaySkipersCount}/{_soccer.ActivePlayersCount}";
      _scorerText.text  = _soccer.LastGoalScorer.GetBehaviour(Sandbox).Name;
    }
  }

  void SetState(bool shown)
  {
    _container.SetActive(shown);

    if (Sandbox.IsVisible)
    {
      _vignetteEffect.active = shown;
      _grainEffect.active = shown;
    }   
  }

}
