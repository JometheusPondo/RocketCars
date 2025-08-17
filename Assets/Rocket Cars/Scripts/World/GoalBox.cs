using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick.Unity;

public class GoalBox : NetworkBehaviour
{
  public Team    Team;

  private Soccer _soccer;

  public override void NetworkStart()
  {
    _soccer = Sandbox.GetComponent<GlobalInfo>().GameMode as Soccer;
  }

  private void OnTriggerEnter(Collider other)
  {
    var ball = other.gameObject.GetComponent<Ball>();

    // we only score goals in the server, therefore scoring goals is not predicted.
    if (ball != null && IsServer)
      _soccer.RegisterGoal(ball.LastTouchedPlayer, this);
  }
}
