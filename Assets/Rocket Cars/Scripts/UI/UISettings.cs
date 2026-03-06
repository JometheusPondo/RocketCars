using UnityEngine;
using System;


// Settings menu with key rebinding. 
// Attach to the same object as UIPauseMenu.

public class UISettings : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    private InputBindingManager _ibm;
    private Vector2 _scrollPos;
    private bool _isRebinding;
    private InputBindingManager.InputAction _rebindAction;
    private bool _rebindIsController;
    private float _rebindTimer;
    private GUIStyle _headerStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _boxStyle;
    private GUIStyle _bindButtonStyle;
    private GUIStyle _activeBindStyle;
    private bool _stylesInitialized;
    private int _currentTab; // 0 = controller, 1 = keyboard

    private readonly string[] _tabNames = { "Controller", "Keyboard / Mouse" };

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _headerStyle.normal.textColor = Color.white;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };
        _labelStyle.normal.textColor = Color.white;

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16
        };

        _boxStyle = new GUIStyle(GUI.skin.box);

        _bindButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fixedHeight = 32
        };

        _activeBindStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fixedHeight = 32
        };
        _activeBindStyle.normal.textColor = Color.yellow;
        _activeBindStyle.hover.textColor = Color.yellow;

        _stylesInitialized = true;
    }

    public void Open()
    {
        _ibm = InputBindingManager.Instance;
        IsOpen = true;
        _isRebinding = false;
    }

    public void Close()
    {
        IsOpen = false;
        _isRebinding = false;
    }

    void OnGUI()
    {
        if (!IsOpen || _ibm == null) return;

        InitStyles();

        float w = 650;
        float h = 550;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        Rect windowRect = new Rect(x, y, w, h);
        GUI.Box(windowRect, "", _boxStyle);

        GUILayout.BeginArea(new Rect(x + 20, y + 10, w - 40, h - 20));

        GUILayout.Label("Settings", _headerStyle);
        GUILayout.Space(10);

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, _buttonStyle, GUILayout.Height(30));
        GUILayout.Space(10);

        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(380));

        bool isController = _currentTab == 0;

        GUILayout.BeginHorizontal();
        GUILayout.Label("Action", _labelStyle, GUILayout.Width(200));
        GUILayout.Label("Binding", _labelStyle, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        foreach (InputBindingManager.InputAction action in Enum.GetValues(typeof(InputBindingManager.InputAction)))
        {
            DrawBindingRow(action, isController);
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        // Bottom buttons
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Reset to Defaults", _buttonStyle, GUILayout.Height(35)))
        {
            _ibm.ResetToDefaults();
            _isRebinding = false;
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Back", _buttonStyle, GUILayout.Width(120), GUILayout.Height(35)))
        {
            Close();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        if (_isRebinding)
        {
            HandleRebinding();
        }
    }

    private void DrawBindingRow(InputBindingManager.InputAction action, bool isController)
    {
        var binding = isController
            ? _ibm.GetControllerBinding(action)
            : _ibm.GetKBBinding(action);

        string actionName = FormatActionName(action);
        string bindingName = binding != null ? binding.displayName : "None";

        bool isThisRebinding = _isRebinding
            && _rebindAction == action
            && _rebindIsController == isController;

        GUILayout.BeginHorizontal();

        GUILayout.Label(actionName, _labelStyle, GUILayout.Width(200));

        if (isThisRebinding)
        {
            float pulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
            GUI.color = Color.Lerp(Color.yellow, Color.white, pulse);

            if (GUILayout.Button("... Press any input ...", _activeBindStyle, GUILayout.Width(200)))
            {
                _isRebinding = false;
            }

            GUI.color = Color.white;
        }
        else
        {
            if (GUILayout.Button(bindingName, _bindButtonStyle, GUILayout.Width(200)))
            {
                StartRebind(action, isController);
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    private void StartRebind(InputBindingManager.InputAction action, bool isController)
    {
        _isRebinding = true;
        _rebindAction = action;
        _rebindIsController = isController;
        _rebindTimer = 5f; 
    }

    private void HandleRebinding()
    {
        _rebindTimer -= Time.unscaledDeltaTime;
        if (_rebindTimer <= 0f)
        {
            _isRebinding = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _isRebinding = false;
            return;
        }

        var newBinding = _ibm.DetectInput(_rebindIsController);
        if (newBinding != null)
        {
            if (_rebindIsController)
                _ibm.SetControllerBinding(_rebindAction, newBinding);
            else
                _ibm.SetKBBinding(_rebindAction, newBinding);

            _isRebinding = false;
        }
    }

    private string FormatActionName(InputBindingManager.InputAction action)
    {
        switch (action)
        {
            case InputBindingManager.InputAction.Throttle:          return "Throttle";
            case InputBindingManager.InputAction.Brake:             return "Brake / Reverse";
            case InputBindingManager.InputAction.SteerLeft:         return "Steer Left";
            case InputBindingManager.InputAction.SteerRight:        return "Steer Right";
            case InputBindingManager.InputAction.Jump:              return "Jump";
            case InputBindingManager.InputAction.Boost:             return "Boost";
            case InputBindingManager.InputAction.AirRollPowerslide: return "Air Roll / Powerslide";
            case InputBindingManager.InputAction.AirRollLeft:       return "Air Roll Left";
            case InputBindingManager.InputAction.BallCamToggle:     return "Ball Cam Toggle";
            case InputBindingManager.InputAction.Scoreboard:        return "Scoreboard";
            default: return action.ToString();
        }
    }
}
