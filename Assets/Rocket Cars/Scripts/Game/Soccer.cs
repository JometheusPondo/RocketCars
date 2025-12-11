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
  [Networked] public State                        GameState              { get; set; } // the current state of the game.
  [Networked] public Tick                         TransitionTick         { get; set; } // used for state transitions.
  [Networked] public Tick                         RoundStartTick         { get; set; } // the tick at which the round started.
  [Networked] public int                          TeamRedGoals           { get; set; } // number of goals for the red team.
  [Networked] public int                          TeamBlueGoals          { get; set; } // number of goals for the blue team.
  [Networked] public Team                         WinnerTeam             { get; set; } // the winning team when the game ends.
  [Networked] public NetworkBehaviourRef<Player>  LastGoalScorer         { get; set; } // the last player to score a goal.
  [Networked] public Team                         LastGoalTarget         { get; set; } // the team that was scored at.
  [Networked] public Tick                         LastGoalTick           { get; set; } // the tick at which the last goal was scored at.
  [Networked] public NetworkBool                  GoalReplayLookAtScorer { get; set; } // indicate if we should look at the scorer (instead of the ball) during replay.

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
  public int                                      GoalsToWin             = 5;  // number of goals required for the game to end.
  public float                                    DelayUntilReplay       = 3f; // how long until we transition to replay after a goal was scored.
  public float                                    DelayUntilRoundStart   = 3f; // how long until the round starts after replay finishes.
  public float                                    DelayUntilRestart      = 5f; // how long until the game is restarted after it game over.
 
  [Header("Goal Replay")]
  public Transform                                ReplayCameraBluePosition;
  public Transform                                ReplayCameraRedPosition;
  public int                                      ReplayCameraFOV        = 10;
  public float                                    ReplayCameraLerpFactor = 5f;
  public Vector3                                  ReplayCameraPosition   => LastGoalTarget == Team.Blue ? ReplayCameraRedPosition.position : ReplayCameraBluePosition.position;

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
  private int                                     _redTeamNum;
  private Stack<Transform>                        _freeRedTeamSpawns;
  private Stack<Transform>                        _freeBlueTeamSpawns;
  private Stack<CarController>                    _freeCars;
  private ReplaySystem                            _goalReplay;
  private Camera                                  _camera;
  private float                                   _originalFOV;
  private Vector4                                 _originalArenaRoofEmissionColor;

  public override void NetworkStart()
  {
    _goalReplay                     = GetComponent<ReplaySystem>();
     AudioSource                    = GetComponent<AudioSource>();
    _camera                         = Sandbox.FindObjectOfType<Camera>();
    _originalFOV                    = _camera.fieldOfView;
    _originalArenaRoofEmissionColor = ArenaRoofMaterial.GetVector("_EmissionColor");
  
    base.NetworkStart();

    if (IsServer)
      PreAllocateCars();

    // asking the server to join the game and assign us a car.
    if (SpawnPlayerImmediately && IsClient)
      RPC_EnterGame(Random.value > 0.5f ? Team.Blue : Team.Red, "Unnamed"); 

    if (IsServer && Sandbox.GetPlayerObject(Sandbox.LocalPlayer) == null)
      AcquireCar(Sandbox.LocalPlayer.PlayerId);

    Sandbox.Events.OnPlayerJoined += OnPlayerJoined;
    Sandbox.Events.OnPlayerLeft   += OnPlayerLeft;
  }

  public void OnPlayerJoined(NetworkSandbox sandbox, NetworkPlayerId plr)
  {
    if (IsClient)
      return;
    
    AcquireCar(plr);

    if (SpawnPlayerImmediately)
      RPC_EnterGame(Random.value > 0.5f ? Team.Red : Team.Blue, "Unnamed");
  }

  public void OnPlayerLeft(NetworkSandbox sandbox, NetworkPlayerId plr)
  {
    if (IsClient)
      return;
    var player = Sandbox.GetPlayerObject<Player>(plr);
    if (player != null)
    {
      ActivePlayers.Remove(player.GetRef<Player>());
      ReleaseCar(player);
    }
  }

  /// <summary>
  /// An RPC used to request to join the game. This can be called by anyone, but only executed in the Owner (the server/host in Netick is called Owner).
  /// </summary>
  [Rpc(RpcPeers.Everyone, RpcPeers.Owner, isReliable: true)]
  public void RPC_EnterGame(Team team, NetworkString16 name, RpcContext ctx = default)
  {
    if (GameState == State.GoalReplay) // return when in replay
      return;

    var plr                   = ctx.Source;
    int playersPerTeam        = Sandbox.Config.MaxPlayers / 2;

    if (team == Team.Red && playersPerTeam == _redTeamNum)  // if the player wants to join red team but it's full, we switch it to blue team.
      team                    = Team.Blue;

    var player                = Sandbox.GetPlayerObject<Player>(plr);
    var car                   = player.Car;
    car.SetCarActive(true);
    player.SetTeam(team);
    player.Spawn              = player.Team == Team.Red ? _freeRedTeamSpawns.Pop() : _freeBlueTeamSpawns.Pop();
    car.NetworkRigidbody.     Teleport(player.Spawn.position, player.Spawn.rotation);
    player.Name               = name;
    player.IsReady            = true; // setting the player as ready, which will cause the player to be registered using the OnChanged callback on IsReady.
    ActivePlayers.Add(player.GetRef<Player>());
  }

  /// <summary>
  /// We pre-allocate all cars in the game and add them to a pool. When a player joins, we grab a car from the pool and assign it to the player. 
  /// <para>Note: we could've chosen to use Netick built-in pooling system, but because we need to still replay players (for goal replay) who could leave the game at any point in
  /// time, we can't destroy their cars when they leave and instead we use this other approach of always having 
  /// every car pre-spawned, and simply assigning/removing players as Input Sources when they join/leave. </para>
  /// </summary>
  void PreAllocateCars()
  {
    int playersPerTeam        = Sandbox.Config.MaxPlayers / 2;
    _freeCars                 = new(playersPerTeam);
    _freeBlueTeamSpawns       = new(playersPerTeam);
    _freeRedTeamSpawns        = new(playersPerTeam);

    for (int i = 0; i < playersPerTeam; i++)
      _freeBlueTeamSpawns.    Push(BlueTeamSpawns[i]);
    for (int i = 0; i < playersPerTeam; i++)
      _freeRedTeamSpawns.     Push(RedTeamSpawns[i]);
    for (int i = 0; i < Sandbox.Config.MaxPlayers; i++)
      _freeCars.              Push(CreateCar().Car);
  }

  Player CreateCar()
  {
    var car    = Sandbox.NetworkInstantiate(PlayerPrefab, Vector3.up * -1000f);
    car.SetCarActive(false);
    return car.GetComponent<Player>();
  }

  Player AcquireCar(NetworkPlayerId netPlayerId)
  {
    var netPlayer   = Sandbox.GetPlayerById(netPlayerId);
    var car         = _freeCars.Pop(); // getting a car from the pool of free cars.
    var player      = car.GetComponent<Player>();
    car.InputSource = netPlayer;  // set the input source of the car to the Rpc calling player.
    Sandbox.SetPlayerObject(netPlayer, player.Object);
    return player;
  }

  void ReleaseCar(Player plr)
  {
    _freeCars.Push(plr.Car);
    plr.Car.SetCarActive(false);

    if (Sandbox.GetPlayerObject(plr.InputSourcePlayerId) == null)
      return;

    if (plr.Team == Team.Red)
    {
      _redTeamNum--;
      _freeRedTeamSpawns.Push(plr.Spawn);
    }
    else if (plr.Team == Team.Blue)
    {
      _freeBlueTeamSpawns.Push(plr.Spawn);
    }
  }

  /// <summary>
  /// In NetworkFixedUpdate, we check if a state transition should happen and we execute it.
  /// </summary>
  public override void NetworkFixedUpdate()
  {
    if (IsClient)
      return;

    switch (GameState)
    {
      case State.Started:
        {
          if (_camera.fieldOfView != _originalFOV)
            _camera.fieldOfView = _originalFOV;
         
          break;
        }
      case State.GoalScored:
        {
          // this is just a state to wait a little bit after scoring a goal, then switching to replay mode.
          if (Sandbox.TickToTime(Sandbox.Tick - TransitionTick) >= DelayUntilReplay)
            GameState = State.GoalReplay;

          break;
        }
      case State.GoalReplay:
        {
          GoalReplayLookAtScorer            = _goalReplay.TimeUntilReplayFinish < 4;
          // see if replay finished, and if so check if game should end if one of the team scored enough goals, if not go to SoccerState.WaitingForRoundStart state.
          if (!_goalReplay.IsReplaying)
          {
            // if a team has won.
            if (TeamRedGoals >= GoalsToWin || TeamBlueGoals >= GoalsToWin)
            {
              WinnerTeam                = TeamRedGoals >= GoalsToWin ? Team.Red : Team.Blue;
              GameState                 = State.GameOver;
            }
            // if no one one, we just start a new round.
            else
            {
              GameState                 = State.WaitingForRoundStart;
            }

            if (!Sandbox.IsReplay || Sandbox.ContainsPlayer(ReplaySelectedPlayer))
            {
              // change the camera fov in goal replay.
              _camera.fieldOfView = ReplayCameraFOV;
              // snap the camera rotation to look at the scorer.
              var scorer = LastGoalScorer.GetBehaviour<Player>(Sandbox).Car;
              _camera.transform.rotation = Quaternion.LookRotation((scorer.transform.position - ReplayCameraPosition).normalized, Vector3.up);
            }

            // make sure the players who left during relay are reset properly.
            foreach (var car in _freeCars)
            {
              car.SetCarActive(false);
            }
          }
          break;
        }
      case State.WaitingForRoundStart:
        {
          // see if round starts timer finished and go to SoccerState.Started if so.
          if (Sandbox.TickToTime(Sandbox.Tick - TransitionTick) >= DelayUntilRoundStart)
            GameState = State.Started;

          break;
        }
      case State.GameOver:
        {
          // see if restart timer finished and go to SoccerState.WaitingForRoundStart if so.
          if (Sandbox.TickToTime(Sandbox.Tick - TransitionTick) >= DelayUntilRestart)
          {
            GameState      = State.WaitingForRoundStart;

            // reset goals
            TeamBlueGoals  = 0;
            TeamRedGoals   = 0;

            foreach (var player in ActivePlayers)
              player.GetBehaviour(Sandbox).Goals = 0;
          }

          break;
        }
      default:
        break;
    }

  }

  /// <summary>
  /// Called when GameState changes. Since this is an [OnChanged] method, it will be invoked in both the server and all clients,
  /// and it will help us do various things when we are entering or existing specific states.
  /// </summary>
  [OnChanged(nameof(GameState))][UsedImplicitly]
  private void OnGameStateChanged(OnChangedData dat)
  {
    OnGameStateChangedEvent?.Invoke(dat);

    if (IsServer)
      TransitionTick = Sandbox.Tick;

    switch (GameState)
    {
      case State.Started:
        {
          if (IsServer)
          {
            RoundStartTick          = Sandbox.Tick;

            // make sure to enable input.
            DisableInputForEveryone = false;
            // start recording.
            _goalReplay.StartRecording();
          }

          // playing round start audio.
          if (dat.IsCatchingUp == false)
            AudioSource.NetworkPlayOneShot(Sandbox, RoundStartAudioClip);

          OnRoundStartedEvent?.Invoke();
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

          // reset the cars to their spawn positions.
          foreach (var player in ActivePlayers)
            player.GetBehaviour(Sandbox).GetComponent<CarCameraController>().LookAtBall = true;

          break;
        }
      case State.WaitingForRoundStart:
        {
          if (IsServer)
          {
            // disable input, enable car camera, and enable car simulation when entering this state.
            DisableInputForEveryone                     = true;
            DisableCarCamera                            = false;
            DisableCarSimulation                        = false;

            // reset the cars to their spawn positions.
            foreach (var player in ActivePlayers)
            {
              var plr = player.GetBehaviour(Sandbox);
              plr.Car.                               ClearState();
              plr.Car.NetworkRigidbody.              Teleport(plr.Spawn.position, plr.Spawn.rotation);
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
          DisableInputForEveryone                       = true;
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

      // explode effect on players near the goal.
      foreach (var player in ActivePlayers)
        ExplodePlayer(player.GetBehaviour(Sandbox), box, Ball);
    }

    GameState = State.GoalScored;

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
  /// This is an effect to push cars away from the goal box after a goal was scored.
  /// </summary>
  private void ExplodePlayer(Player scorer, GoalBox box, Ball ball)
  {
    var   rigid               = scorer.GetComponent<Rigidbody>();
    float distanceMultiplier  = Mathf.InverseLerp(0f, MaxDistanceToCar, Mathf.Min(MaxDistanceToCar, Vector3.Distance(box.transform.position, rigid.transform.position)));
    float ballSpeedMultiplier = Mathf.InverseLerp(0f, MaxBallSpeed,     Mathf.Min(MaxBallSpeed, ball.Rigidbody.velocity.magnitude));
    float multiplier          = distanceMultiplier * ballSpeedMultiplier;
    rigid.AddForce(-box.transform.right   * GoalExplosionForce   * multiplier, ForceMode.VelocityChange);
    rigid.AddForce(Vector3.up             * GoalExplosionUpForce * multiplier, ForceMode.VelocityChange);
    rigid.AddTorque(rigid.angularVelocity * GoalExplosionTorque  * multiplier, ForceMode.VelocityChange);
  }

  public override void NetworkRender()
  {
    if ((Sandbox.ContainsPlayer(ReplaySelectedPlayer) || !Sandbox.IsReplay) && GameState == State.GoalReplay)
    {
      _camera.fieldOfView = ReplayCameraFOV;
      ControlGoalReplayCamera();
    }
    else
    {
      _camera.fieldOfView = _originalFOV;
    }

    // roof emission effect when scoring goal.
    var goalRoofColor = Color.Lerp(Color.white, LastGoalTarget == Team.Blue ? RedTeamColor : BlueTeamColor, Mathf.InverseLerp(-1f, 1f, Mathf.Sin(FlashingSpeed * Time.time))) * 2f;
    ArenaRoofMaterial.SetVector("_EmissionColor", GameState == State.GoalScored ? goalRoofColor : _originalArenaRoofEmissionColor);
  }

  /// <summary>
  /// Rotates the camera to look at the ball and the player who scored the last goal.
  /// </summary>
  public void ControlGoalReplayCamera()
  {
    var scorer                 = LastGoalScorer.GetBehaviour(Sandbox).Car.transform;
    var target                 = GoalReplayLookAtScorer ? scorer : Ball.transform;
    var rot                    = Quaternion.LookRotation((target.position - ReplayCameraPosition).normalized, Vector3.up);
    _camera.transform.position = ReplayCameraPosition;
    _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, rot, ReplayCameraLerpFactor * Time.unscaledDeltaTime);
  }
}
