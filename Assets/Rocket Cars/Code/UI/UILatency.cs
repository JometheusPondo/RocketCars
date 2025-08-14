using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Netick;
using Netick.Unity;

public class UILatency : NetworkBehaviour
{
  [SerializeField]
  private TextMeshProUGUI  _latencyText;

  public override void NetworkRender()
  {
    int rttInMilliseconds  = (int)(Sandbox.RTT * 1000);

    if (IsServer)
    {
      _latencyText.color   = Color.white;
      _latencyText.text    = "Latency: 0 ms";
    }
    else
    {
      if (rttInMilliseconds < 120)
      {
        _latencyText.color = Color.white;
      }
      else if (rttInMilliseconds >= 200)
      {
        _latencyText.color = Color.red;
      }
      else if (rttInMilliseconds > 120)
      {
        _latencyText.color = Color.yellow;
      }

      _latencyText.text    = $"Latency: {rttInMilliseconds.ToString()} ms";
    }
  }
}
