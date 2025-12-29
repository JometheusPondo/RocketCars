using JetBrains.Annotations;
using Netick;
using Netick.Unity;
using System.Collections;
using System.Collections.Generic;
using Unity;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Soccer game mode. This script implements the soccer game mode.
/// </summary>
public class Soccer : GameMode
{
  public enum State
  {
    Started,
    GoalScored,
    GoalReplay,
    WaitingForRoundStart,
    GameOver
  }

  // Networked State ********************
  [Networked, Smooth] public State                GameState              { get; set; } // the current state of the game.
  [Networked] public Tick                         TransitionTick         { get; set; } // used for state transitions.
  [Networked] public Tick                         RoundStartTick         { get; set; } // the tick at which the round started.
  [Networked] public int                          ActivePlayersCount     { get; set; } // the number of active players.
  [Networked] public int                          TeamRedGoals           { get; set; } // number of goals for the red team.
  [Networked] public int                          TeamBlueGoals          { get; set; } // number of goals for the blue team.
  [Networked] public Team                         WinnerTeam             { get; set; } // the winning team when the game ends.
  [Networked] public NetworkBehaviourRef<Player>  LastGoalScorer         { get; set; } // the last player to score a goal.
  [Networked] public Team                         LastGoalTarget         { get; set; } // the team that was scored at.
  [Networked] public Tick                         LastGoalTick           { get; set; } // the tick at which the last goal was scored at.
  [Networked] public NetworkBool                  GoalReplayLookAtScorer { get; set; } // indicate if we should look at the scorer (instead of the ball) during replay.
  [Networked] public int                          GoalReplaySkipersCount { get; set; } // number of players who want to skip goal replay.


  // Public Events
  public event UnityAction<Player, bool>          OnGoalsChangedEvent;
  public event UnityAction<OnChangedData>         OnGameStateChangedEvent;
  public event UnityAction                        OnRoundStartedEvent;
  public event UnityAction                        OnWaitingForRoundStartEvent;
  public event UnityAction                        OnGameOverEvent;

  [Header("Testing")]
  public bool                                     SpawnPlayerImmediately = false;

  [Header("General")]
  public CarController                            PlayerPrefab;
  public Transform[]                              RedTeamSpawns;
  public Transform[]                              BlueTeamSpawns;
  public Ball                                     Ball;
  public int                                      GoalsToWin             = 5;  // number of goals required for the match to end.
  public float                                    DelayUntilReplay       = 3f; // how long until we transition to replay after a goal was scored.
  public float                                    DelayUntilRoundStart   = 3f; // how long until the round starts after replay finishes.
  public float                                    DelayUntilRestart      = 5f; // how long until the game is restarted after it game over.
 
  [Header("Goal Explosion Effect")]
  public float                                    GoalExplosionForce     = -50f;
  public float                                    GoalExplosionTorque    = 50f;
  public float                                    GoalExplosionRadius    = 30f;
  public float                                    GoalExplosionUpForce   = 10f;
  public float                                    MaxDistanceToCar       = 5f;
  public float                                    MaxBallSpeed           = 1.1f;

  [Header("Arena Roof Material Emission Goal Effect")]
  public Material                                 ArenaRoofMaterial;
  public Color                                    RedTeamColor;
  public Color                                    BlueTeamColor;
  public float                                    FlashingSpeed          = 1.2f;

  [Header("Audio")]
  public AudioClip                                GoalScoreAudioClip;
  public AudioClip                                RoundStartAudioClip;
  public AudioSource                              AudioSource;

  // private
  private Stack<Transform>                        _freeRedTeamSpawns;
  private Stack<Transform>                        _freeBlueTeamSpawns;
  private Stack<CarController>                    _freeCars;
  private List<CarController>                     _allCars;
  private GoalReplay                              _goalReplay;
  private GoalReplayCameraController              _goalReplayCameraController;
  private Vector4                                 _originalArenaRoofEmissionColor;

  public override void NetworkStart()
  {
    base.NetworkStart();
    _goalReplay                     = GetComponent<GoalReplay>();
     AudioSource                    = GetComponent<AudioSource>();
    _goalReplayCameraController     = GetComponent<GoalReplayCameraController>();
    _originalArenaRoofEmissionColor = ArenaRoofMaterial.GetVector("_EmissionColor");
    Sandbox.Events.OnPlayerJoined  += OnPlayerJoined;
    Sandbox.Events.OnPlayerLeft    += OnPlayerLeft;

    if (Sandbox.Config.MaxPlayers % 2 != 0)
      Sandbox.LogError("Incorrect MaxPlayers value! In Rocket Cars, MaxPlayers must be an even number.");

    if (IsServer)
      PreAllocateCars();

    if (SpawnPlayerImmediately && IsHost)
      JoinAnyTeam(Sandbox.LocalPlayer.PlayerId, "Unnamed"); 
  }

  public void OnPlayerJoined(NetworkSandbox sandbox, NetworkPlayerId plr)
  {
    if (IsServer && SpawnPlayerImmediately)
      JoinAnyTeam(plr, "Unnamed");
  }

  public void OnPlayerLeft(NetworkSandbox sandbox, NetworkPlayerId plr)
  {
    if (IsServer && Sandbox.TryGetPlayerObject(plr, out Player player))
      ReleaseCar(player);
  }

  public void JoinAnyTeam(NetworkPlayerId plr, string name)
  {
    var team = Random.value > 0.5f ? Team.Red : Team.Blue;
    if (GetTeamSize(team) == (Sandbox.Config.MaxPlayers / 2)) // if full, join other team.
      team = team == Team.Red ? Team.Blue : Team.Red;
    AccuireCar(plr, team, name);
  }

  /// <summary>
  /// An RPC used to request to join the game. This can be called by anyone, but only executed in the Owner (the server/host in Netick is called Owner).
  /// </summary>
  [Rpc(RpcPeers.Everyone, RpcPeers.Owner, isReliable: true)]
  public void RPC_Join(Team team, NetworkString16 name, RpcContext ctx = default)
  {
    // return if in goal replay or team is full.
    if (GameState == State.GoalReplay || (team == Team.Red && _freeRedTeamSpawns.Count == 0) || (team == Team.Blue && _freeBlueTeamSpawns.Count == 0))
      return;

    AccuireCar(ctx.Source, team, name);
  }


  void AccuireCar(NetworkPlayerId playerId, Team team, NetworkString16 name)
  {
    if (Sandbox.GetPlayerObject(playerId) != null) // return if player already has a car.
      return;

    var netPlayer             = Sandbox.GetPlayerById(playerId);
    var car                   = _freeCars.Pop(); // getting a car from the pool of free cars.
    var player                = car.GetComponent<Player>();
    car.InputSource           = netPlayer;  // set the input source of the car to the Rpc calling player.
    car.SetCarActive(true);
    Sandbox.SetPlayerObject(netPlayer, player.Object); 
    player.Name               = name;
    player.Team               = team;
    player.Spawn              = player.Team == Team.Red ? _freeRedTeamSpawns.Pop() : _freeBlueTeamSpawns.Pop();
    player.IsReady            = true;
    car.NetworkRigidbody.Teleport(player.Spawn.position, player.Spawn.rotation);  
    ActivePlayersCount++;
  }

  void ReleaseCar(Player plr)
  {
    ActivePlayersCount--;
    _freeCars.Push(plr.Car);
    plr.Car.SetCarActive(false);

    if (plr.Team == Team.Red)
      _freeRedTeamSpawns.Push(plr.Spawn);
    else if (plr.Team == Team.Blue)
      _freeBlueTeamSpawns.Push(plr.Spawn);
  }

  /// <summary>
  /// We pre-allocate all cars in the game and add them to a pool. When a player joins, we grab a car from the pool and assign it to the player. 
  /// <para>Note: we could've chosen to use Netick built-in pooling system, but because we need to still replay players (for goal replay) who could leave the game at any point in
  /// time, we can't destroy their cars when they leave and instead we use this other approach of always having 
  /// every car pre-spawned, and simply assigning/removing players as Input Sources when they join/leave. </para>
  /// </summary>
  void PreAllocateCars()
  {
    int playersPerTeam   = Sandbox.Config.MaxPlayers / 2;
    _allCars             = new (Sandbox.Config.MaxPlayers);
    _freeCars            = new(Sandbox.Config.MaxPlayers);
    _freeBlueTeamSpawns  = new(playersPerTeam);
    _freeRedTeamSpawns   = new(playersPerTeam);

    for (int i = 0; i < Sandbox.Config.MaxPlayers; i++)
    {
      var car            = Sandbox.NetworkInstantiate(PlayerPrefab, Vector3.up * -1000f);
      car.SetCarActive(false);
      _freeCars.Push(car);
      _allCars.Add(car);
    }

    for (int i = 0; i < playersPerTeam; i++)
      _freeBlueTeamSpawns.Push(BlueTeamSpawns[i]);
    for (int i = 0; i < playersPerTeam; i++)
      _freeRedTeamSpawns.Push(RedTeamSpawns[i]);
  }

  public int GetTeamSize(Team team)
  {
    int count          = 0;
    foreach (var playerId in Sandbox.Players)
      if (Sandbox.TryGetPlayerObject(playerId, out Player player) && player.Team == team)
        count++;

    return count;
  }

  /// <summary>
  /// In NetworkFixedUpdate, we execute per-state logic and check if a state transition should happen and we execute it.
  /// </summary>
  public override void NetworkFixedUpdate()
  {
    switch (GameState)
    {
      case State.Started:
        {
          break;
        }
      case State.GoalScored:
        {
          if (IsClient)
            return;

          // this is just a state to wait a little bit after scoring a goal, then switching to goal replay state.
          if (Sandbox.TickToTime(Sandbox.Tick - TransitionTick) >= DelayUntilReplay)
            ChangeState(State.GoalReplay);
          
          // reset skip state for everyone
          foreach (var playerId in Sandbox.Players)
            if (Sandbox.TryGetPlayerObject(playerId, out Player player))
              player.SkipGoalReplay = false;

          break;
        }
      case State.GoalReplay:
        {
          if (IsClient)
            return;

          GoalReplayLookAtScorer = _goalReplay.TimeUntilReplayFinish < 4;
          GoalReplaySkipersCount = 0;

          // see who wants to skip goal replay
          foreach (var playerId in Sandbox.Players)
          {
            if (Sandbox.TryGetPlayerObject(playerId, out Player player) && _goalReplay.TimeUntilReplayFinish <= (_goalReplay.MaxReplayTime - 0.15f))
            {
              if (player.FetchInput(out GameInput input) && input.Jump == true) // jump is used as the skip input
                player.SkipGoalReplay = true;

              if (player.SkipGoalReplay)
                GoalReplaySkipersCount++;
            }
          }

          if (GoalReplaySkipersCount == ActivePlayersCount) // everyone wants to skip
            _goalReplay.StopReplaying(); // skip replay
          
          // see if replay finished, and if so check if the match should end if one of the team scored enough goals, if not go to SoccerState.WaitingForRoundStart state.
          if (!_goalReplay.IsReplaying)
          {
            // if a team has won.
            if (TeamRedGoals >= GoalsToWin || TeamBlueGoals >= GoalsToWin)
            {
              WinnerTeam                 = TeamRedGoals >= GoalsToWin ? Team.Red : Team.Blue;
              ChangeState(State.GameOver);
            }
            // if no one one, we just start a new round.
            else
            {
              ChangeState(State.WaitingForRoundStart);
            }

            // make sure the players who left during relay are reset properly.
            foreach (var car in _freeCars)
              car.SetCarActive(false);
          }
          break;
        }
      case State.WaitingForRoundStart:
        {
          // see if round starts timer finished and go to SoccerState.Started if so.
          if (Sandbox.TickToTime(Sandbox.Tick - TransitionTick) >= DelayUntilRoundStart)
          {
            if (IsServer)
              _goalReplay.StartRecording(); // start recording.

            ChangeState(State.Started);
            RoundStartTick     = Sandbox.Tick;
            DisableInputForAll = false; // enable input.
          }

          break;
        }
      case State.GameOver:
        {
          if (IsClient)
            return;

          // see if restart timer finished and go to SoccerState.WaitingForRoundStart if so.
          if (Sandbox.TickToTime(Sandbox.Tick - TransitionTick) >= DelayUntilRestart)
          {
            ChangeState(State.WaitingForRoundStart);

            // reset goals
            TeamBlueGoals  = 0;
            TeamRedGoals   = 0;

            foreach (var playerId in Sandbox.Players)
              if (Sandbox.TryGetPlayerObject(playerId, out Player player))
                player.Goals = 0;
          }

          break;
        }
      default:
        break;
    }
  }

  private void ChangeState(State state)
  {
    GameState      = state;
    TransitionTick = Sandbox.Tick;
  }

  /// <summary>
  /// Called when GameState changes. Since this is an [OnChanged] method, it will be invoked in both the server and all clients,
  /// and it will help us do various things when we are entering or existing specific states.
  /// </summary>
  [OnChanged(nameof(GameState))][UsedImplicitly]
  private void OnGameStateChanged(OnChangedData dat)
  {
    OnGameStateChangedEvent?.Invoke(dat);

    switch (GameState)
    {
      case State.Started:
        {
          OnRoundStartedEvent?.Invoke();

          // playing round start audio.
          if (dat.IsCatchingUp == false)
            AudioSource.NetworkPlayOneShot(Sandbox, RoundStartAudioClip);

          break;
        }
      case State.GoalScored:
        {
          break;
        }
      case State.GoalReplay:
        {
          if (IsServer)
          {
            // disable the car camera and simulation when replaying.
            DisableCarSimulation                        = true;
            DisableCarCamera                            = true;
            _goalReplay.StartReplaying();
          }

          foreach (var playerId in Sandbox.Players)
            if (Sandbox.TryGetPlayerObject(playerId, out Player player))
              player.GetComponent<CarCameraController>().LookAtBall = true;

          break;
        }
      case State.WaitingForRoundStart:
        {
          if (IsServer)
          {
            // disable input, enable car camera, and enable car simulation when entering this state.
            DisableInputForAll                          = true;
            DisableCarCamera                            = false;
            DisableCarSimulation                        = false;

            // reset the cars to their spawn positions.
            foreach (var playerId in Sandbox.Players)
            {
              if (Sandbox.TryGetPlayerObject(playerId, out Player player))
              {
                player.Car.ClearState();
                player.Car.NetworkRigidbody.Teleport(player.Spawn.position, player.Spawn.rotation);
              }
            }

            // reset the ball to its spawn position.
            Ball.GetComponent<NetworkRigidbody>().      Teleport(Ball.InitialPosition);
            Ball.Rigidbody.velocity                     = default;
            Ball.Rigidbody.angularVelocity              = default;
          }

          OnWaitingForRoundStartEvent?.Invoke();
          break;
        }
      case State.GameOver:
        {
          // disable input, disable car camera, and enable car simulation when entering this state.
          DisableInputForAll                            = true;
          DisableCarCamera                              = true;
          DisableCarSimulation                          = false;

          OnGameOverEvent?.Invoke();
          break;
        }
      default:
        break;
    }
  }

  /// <summary>
  /// Called (in the host/server only) when a goal was scored.
  /// </summary>
  public void RegisterGoal(Player scorer, GoalBox box)
  {
    if (GameState != State.Started)
      return;

    // store the tick at which this goal was scored at.
    LastGoalTick = Sandbox.Tick;

    if (scorer != null) 
    {
      LastGoalScorer = new NetworkBehaviourRef<Player>(scorer);
      LastGoalTarget = box.Team;

      // is this not an own goal.
      if (box.Team != scorer.Team)
        scorer.Goals++;

      // explode effect on players near the goal area.
      foreach (var playerId in Sandbox.Players)
        if (Sandbox.TryGetPlayerObject(playerId, out Player player))
          ExplodeRigidbody(player.Car.Rigidbody, box, Ball);
      ExplodeRigidbody(Ball.Rigidbody, box, Ball);
    }

    ChangeState(State.GoalScored);

    if (box.Team == Team.Red)
      TeamBlueGoals++;  
    else
      TeamRedGoals++; 
  }

  // Since we have two network variables for goal count for each team, we use two [OnChanged] methods for each one to call OnGoalsChanged which will invoke OnGoalsChangedEvent.
  // we check if a new goal was scored during this change by checking against the previous value and see if the new value has increased.
  [OnChanged(nameof(TeamRedGoals))][UsedImplicitly]  void OnTeamRedGoalsChanged (OnChangedData dat) => OnGoalScored(TeamRedGoals  > dat.GetPreviousValue<int>(), dat);
  [OnChanged(nameof(TeamBlueGoals))][UsedImplicitly] void OnTeamBlueGoalsChanged(OnChangedData dat) => OnGoalScored(TeamBlueGoals > dat.GetPreviousValue<int>(), dat);

  private void OnGoalScored(bool didScoreGoal, OnChangedData dat)
  {
    OnGoalsChangedEvent?.Invoke(Sandbox.GetBehaviour(LastGoalScorer), didScoreGoal);

    // play goal audio
    if (GameState == State.GoalScored && dat.IsCatchingUp == false)
      AudioSource.NetworkPlayOneShot(Sandbox, GoalScoreAudioClip);
  }

  /// <summary>
  /// This is an effect to push cars and ball away from the goal box after a goal was scored.
  /// </summary>
  private void ExplodeRigidbody(Rigidbody rigidbody, GoalBox box, Ball ball)
  {
    var   rigid               = rigidbody;
    float distanceMultiplier  = Mathf.InverseLerp(0f, MaxDistanceToCar, Mathf.Min(MaxDistanceToCar, Vector3.Distance(box.transform.position, rigid.transform.position)));
    float ballSpeedMultiplier = Mathf.InverseLerp(0f, MaxBallSpeed,     Mathf.Min(MaxBallSpeed, ball.Rigidbody.velocity.magnitude));
    float multiplier          = distanceMultiplier * ballSpeedMultiplier;
    rigid.AddForce(-box.transform.right   * GoalExplosionForce   * multiplier, ForceMode.VelocityChange);
    rigid.AddForce(Vector3.up             * GoalExplosionUpForce * multiplier, ForceMode.VelocityChange);
    rigid.AddTorque(rigid.angularVelocity * GoalExplosionTorque  * multiplier, ForceMode.VelocityChange);
  }
 
  public override void NetworkRender()
  {
    _goalReplayCameraController.Render();
    // roof emission effect when scoring goal.
    var goalRoofColor = Color.Lerp(Color.white, LastGoalTarget == Team.Blue ? RedTeamColor : BlueTeamColor, Mathf.InverseLerp(-1f, 1f, Mathf.Sin(FlashingSpeed * Time.time))) * 2f;
    ArenaRoofMaterial.SetVector("_EmissionColor", GameState == State.GoalScored ? goalRoofColor : _originalArenaRoofEmissionColor);
  }
}
