using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Netick.Unity;
using Network = Netick.Unity.Network;

public class UIPauseMenu : NetworkBehaviour
{
  [SerializeField]
  private GameObject _pauseMenu;
  [SerializeField]
  private Button     _buttonResume;
  [SerializeField]
  private Button     _buttonLeave;
  [SerializeField]
  private Button     _buttonQuit;

  private Graphic[]  _graphics;
  private GameMode   _GM;
  private bool       _shown = false;

  void Awake()
  {
    if (Application.isBatchMode)
    {
      return;
    }

    _GM       = GetComponent<GameMode>();
    _graphics = _pauseMenu.GetComponentsInChildren<Graphic>();
  }

  private void Start()
  {
    _buttonResume.onClick.AddListener(Resume);
    _buttonLeave.onClick.AddListener(Leave);
    _buttonQuit.onClick.AddListener(Quit);
  }

  public void Update()
  {
    if (Sandbox == null || !Sandbox.IsRunning)
      return;

    if (_GM != null && Input.GetKeyDown(KeyCode.Escape))
      TogglePause();
  }

  void TogglePause()
  {
    _shown               = !_shown;
    _GM.Paused           = _shown;

    if (_GM.Paused)
      Sandbox.SetInput<GameInput>(default);

    _pauseMenu.SetActive(_shown);
    SetVisibility(_shown);
  }

  void SetVisibility(bool visibility)
  {
    foreach (var graphic in _graphics)
      graphic.SetEnabled(_GM.Sandbox, visibility);
  }

  private void Resume()
  {
    TogglePause();
  }

  private void Leave()
  {
    // shuts down Netick.
    Network.Shutdown();

    // loads menu scene.
    SceneManager.LoadScene(0);
  }

  public void Quit()
  {
    Application.Quit();
  }
}