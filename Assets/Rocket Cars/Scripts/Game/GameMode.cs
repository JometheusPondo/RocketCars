using Netick;
using Netick.Samples;
using Netick.Unity;
using UnityEngine;

/// <summary>
/// This is the Game Mode script, the parent script of all game modes. 
/// </summary>
[ExecutionOrder(-30)]
public abstract class GameMode : NetworkBehaviour
{
  // Networked State ********************
  [Networked] public NetworkBool             DisableInputForAll      { get; set; }  // when true, the car will no longer accept user inputs, but can still be simulated.
  [Networked] public NetworkBool             DisableCarCamera        { get; set; }  // when true, the camera will no longer follow the car.
  [Networked] public NetworkBool             DisableCarSimulation    { get; set; }  // when true the cars will no longer be physically simulated.
  [HideInInspector] public GlobalData        GlobalData              { get; private set; }
  [HideInInspector] public bool              Paused                  = false;
  public GameObject                          UIParent;
  public NetworkPlayerId                     SpectatedPlayer         = NetworkPlayerId.Invalid;

  public override void NetworkAwake()
  {
    GlobalData                               = Sandbox.GetComponent<GlobalData>();
    GlobalData.Camera                        = Sandbox.FindObjectOfType<Camera>();
    GlobalData.GameMode                      = this;
  }

  public override void NetworkStart()
  {
    if (GlobalData.IsReplay)
      GlobalData.Chat.PushNotificationLocal("Press ` (backquote) to toggle UI during replays.");
    else if (Sandbox.IsPlayer)
      GlobalData.Chat.PushNotificationLocal("Press T for team-only chat, and Y for global chat.");
  }

  private void Update()
  {
    if (GlobalData != null && GlobalData.Sandbox.IsRunning && GlobalData.IsReplay && Input.GetKeyDown(KeyCode.BackQuote))
    {
      Cursor.lockState = GlobalData.HideUI ? CursorLockMode.None : CursorLockMode.Locked;
      GlobalData.HideUI = !GlobalData.HideUI;
      UpdateUIState();
    }
  }

  public override void NetworkUpdate()
  {
    if (GlobalData.IsReplay)
      ReactToSpectateControls();

    if (Paused || !GlobalData.CanUseInput)
      return;
    // collecting networked user input.
    var input         = Sandbox.GetInput<GameInput>();
    var movementInput = input.Movement + new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Roll"));
    input.Movement    = new Vector3(Mathf.Clamp(movementInput.x, -1f, 1f), Mathf.Clamp(movementInput.y, -1f, 1f), Mathf.Clamp(movementInput.z, -1f, 1f));
    input.Rocket     |= Input.GetButton("Rocket");
    input.Jump       |= Input.GetButtonDown("Jump");
    input.Drift      |= Input.GetButton("Drift");
    input.AirRoll    |= Input.GetKey(KeyCode.LeftShift);
    Sandbox.SetInput(input);
  }

  public virtual void ReactToSpectateControls()
  {
    if (!Sandbox.ContainsPlayer(SpectatedPlayer))
      SpectatedPlayer = NetworkPlayerId.Invalid;

    var players = Sandbox.Players;

    if (Input.GetKeyDown(KeyCode.F))
    {
      if (SpectatedPlayer.IsValid)
        SpectatedPlayer = NetworkPlayerId.Invalid;
      else if (players.Count > 0)
      {
        if (Sandbox.TryGetPlayerObject<Player>(players[0], out var player0) && player0.IsReady) // handle case where server/host car is not spawned
          SpectatedPlayer = players[0];
        if (players.Count > 1)
        {
          if (Sandbox.TryGetPlayerObject<Player>(players[1], out var player1) && player1.IsReady)
            SpectatedPlayer = players[1];
        }
      }
    }
    else if (Input.inputString.Length > 0 && char.IsDigit(Input.inputString[0]))
    {
      int numValue = (int.Parse(Input.inputString[0].ToString()))-1;
      if (numValue < players.Count && numValue >= 0)
        SpectatedPlayer = players[numValue];
    }
  }

  public void UpdateUIState()
  {
    UIParent.SetActive(!GlobalData.HideUI);
    GetComponent<NetworkInfo>().enabled    = !GlobalData.HideUI;
    GetComponent<GameStarter>().enabled    = !GlobalData.HideUI;
    GetComponent<ReplayTimeline>().enabled = !GlobalData.HideUI;
  }
}