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
  private Button     _buttonSettings;
  [SerializeField]
  private Button     _buttonLeave;
  [SerializeField]
  private Button     _buttonQuit;

  private Graphic[]  _graphics;
  private GameMode   _gm;
  private bool       _shown;
  private UISettings _settings;

  void Awake()
  {
    if (Application.isBatchMode)
      return;

    _gm       = GetComponent<GameMode>();
    _graphics = _pauseMenu.GetComponentsInChildren<Graphic>();
    _settings = GetComponent<UISettings>();
    if (_settings == null)
      _settings = gameObject.AddComponent<UISettings>();
  }

  private void Start()
  {
    _buttonResume.onClick.AddListener(Resume);
    if (_buttonSettings != null)
      _buttonSettings.onClick.AddListener(OpenSettings);
    _buttonLeave.onClick.AddListener(Leave);
    _buttonQuit.onClick.AddListener(Quit);
  }

  public void Update()
  {
    if (Application.isBatchMode || Sandbox == null || !Sandbox.IsRunning || _gm.GlobalData == null)
      return;

    // Close settings with Escape before closing pause menu
    if (Input.GetKeyDown(KeyCode.Escape))
    {
      if (_settings != null && _settings.IsOpen)
      {
        _settings.Close();
        return;
      }

      if (_gm != null && (_gm.GlobalData.CanUseInput || _gm.GlobalData.IsReplay))
        TogglePause();
    }
  }

  void TogglePause()
  {
    _shown      = !_shown;
    _gm.Paused  = _shown;

    if (_gm.Paused)
      Sandbox.SetInput<GameInput>(default);

    // Close settings when closing pause menu
    if (!_shown && _settings != null)
      _settings.Close();

    _pauseMenu.SetActive(_shown);
    SetVisibility(_shown);
  }

  void SetVisibility(bool visibility)
  {
    foreach (var graphic in _graphics)
      graphic.SetEnabled(_gm.Sandbox, visibility);
  }

  private void Resume()
  {
    TogglePause();
  }

  private void OpenSettings()
  {
    if (_settings != null)
    {
      _pauseMenu.SetActive(false);
      _settings.Open();
    }
  }

  private void Leave()
  {
    Network.Shutdown();
    SceneManager.LoadScene(0);
  }

  public void Quit()
  {
    Application.Quit();
  }
}
