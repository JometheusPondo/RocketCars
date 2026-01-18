using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick.Unity;

public class GoalBox : NetworkBehaviour
{
  public Team    Team;
  private Soccer _soccer;
  private float  _goalTimer     = 0f;
  private bool   _isPendingGoal = false;

  public override void NetworkStart()
  {
    _soccer = Sandbox.GetComponent<GlobalData>().GameMode as Soccer;
  }

  private void OnTriggerEnter(Collider other)
  {
    var ball = other.gameObject.GetComponent<Ball>();

    if (ball != null && IsServer && !_isPendingGoal && _soccer.GameState == Soccer.State.Started)
    {
      _goalTimer     = 0.1f;
      _isPendingGoal = true;
    }
  }

  public override void NetworkFixedUpdate()
  {
    if (!IsServer || !_isPendingGoal) 
      return;

    _goalTimer -= Sandbox.FixedDeltaTime;

    if (_goalTimer <= 0)
      FinishRegisterGoal();
  }

  private void FinishRegisterGoal()
  {
    _isPendingGoal = false;

    if (_soccer != null)
      _soccer.RegisterGoal(_soccer.Ball.LastTouchedPlayer, this);
  }
}