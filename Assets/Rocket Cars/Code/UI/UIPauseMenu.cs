using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Netick;
using Netick.Unity;
using Network = Netick.Unity.Network;

public class UIPauseMenu : NetworkBehaviour
{
  [SerializeField]
  private GameObject _pauseMenu;

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

  public override void NetworkUpdate()
  {
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

    if (_shown)
      Cursor.lockState = CursorLockMode.None;
    else
      Cursor.lockState = CursorLockMode.Locked;
  }

  void SetVisibility(bool visibility)
  {
    foreach (var graphic in _graphics)
      graphic.SetEnabled(_GM.Sandbox, visibility);
  }

  public void Resume()
  {
    TogglePause();
  }

  public void Leave()
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