using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages input bindings at runtime. Stores custom bindings in PlayerPrefs.
/// Replaces direct Input.GetAxis/GetButton calls with bindable actions.
/// </summary>
public class InputBindingManager : MonoBehaviour
{
    public static InputBindingManager Instance { get; private set; }

    public enum InputAction
    {
        Throttle,
        Brake,
        SteerLeft,
        SteerRight,
        Jump,
        Boost,
        AirRollPowerslide,
        AirRollLeft,
        BallCamToggle,
        Scoreboard
    }

    public enum BindingType
    {
        Key,
        MouseButton,
        JoystickButton,
        JoystickAxisPositive,
        JoystickAxisNegative
    }

    [Serializable]
    public class Binding
    {
        public BindingType type;
        public KeyCode key;
        public int axisIndex;       
        public string displayName;

        public Binding(KeyCode key)
        {
            this.type = BindingType.Key;
            this.key = key;
            this.displayName = key.ToString();
        }

        public Binding(BindingType type, int axisIndex, string displayName)
        {
            this.type = type;
            this.key = KeyCode.None;
            this.axisIndex = axisIndex;
            this.displayName = displayName;
        }

        public float GetValue()
        {
            switch (type)
            {
                case BindingType.Key:
                case BindingType.MouseButton:
                case BindingType.JoystickButton:
                    return Input.GetKey(key) ? 1f : 0f;
                case BindingType.JoystickAxisPositive:
                    return Mathf.Max(0f, Input.GetAxisRaw("Joy Axis " + axisIndex));
                case BindingType.JoystickAxisNegative:
                    return Mathf.Max(0f, -Input.GetAxisRaw("Joy Axis " + axisIndex));
                default:
                    return 0f;
            }
        }

        public bool GetDown()
        {
            switch (type)
            {
                case BindingType.Key:
                case BindingType.MouseButton:
                case BindingType.JoystickButton:
                    return Input.GetKeyDown(key);
                default:
                    return false; 
            }
        }

        public bool GetHeld()
        {
            switch (type)
            {
                case BindingType.Key:
                case BindingType.MouseButton:
                case BindingType.JoystickButton:
                    return Input.GetKey(key);
                case BindingType.JoystickAxisPositive:
                    return Input.GetAxisRaw("Joy Axis " + axisIndex) > 0.5f;
                case BindingType.JoystickAxisNegative:
                    return Input.GetAxisRaw("Joy Axis " + axisIndex) < -0.5f;
                default:
                    return false;
            }
        }
    }

    private Dictionary<InputAction, Binding> _kbBindings = new Dictionary<InputAction, Binding>();
    private Dictionary<InputAction, Binding> _controllerBindings = new Dictionary<InputAction, Binding>();

    public float StickX => Input.GetAxis("Horizontal");
    public float StickY => Input.GetAxis("Vertical");

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetDefaults();
        LoadBindings();
        SetupJoystickAxes();
    }

    private void SetupJoystickAxes()
    {
        // Unity's legacy input needs axes predefined in InputManager
        // We rely on the axes already set up there for analog sticks
        // Button rebinding works through KeyCode which is runtime-flexible
    }

    private void SetDefaults()
    {
        // Keyboard/Mouse defaults
        _kbBindings[InputAction.Throttle]           = new Binding(KeyCode.W);
        _kbBindings[InputAction.Brake]              = new Binding(KeyCode.S);
        _kbBindings[InputAction.SteerLeft]          = new Binding(KeyCode.A);
        _kbBindings[InputAction.SteerRight]         = new Binding(KeyCode.D);
        _kbBindings[InputAction.Jump]               = new Binding(KeyCode.Mouse1);
        _kbBindings[InputAction.Boost]              = new Binding(KeyCode.Mouse0);
        _kbBindings[InputAction.AirRollPowerslide]  = new Binding(KeyCode.LeftShift);
        _kbBindings[InputAction.AirRollLeft]        = new Binding(KeyCode.R);
        _kbBindings[InputAction.BallCamToggle]      = new Binding(KeyCode.Space);
        _kbBindings[InputAction.Scoreboard]         = new Binding(KeyCode.Tab);

        // Controller defaults (Xbox layout)
        _controllerBindings[InputAction.Throttle]           = new Binding(BindingType.JoystickAxisPositive, 10, "RT");
        _controllerBindings[InputAction.Brake]              = new Binding(BindingType.JoystickAxisPositive, 9, "LT");
        _controllerBindings[InputAction.SteerLeft]          = new Binding(BindingType.JoystickAxisNegative, 1, "L Stick Left");
        _controllerBindings[InputAction.SteerRight]         = new Binding(BindingType.JoystickAxisPositive, 1, "L Stick Right");
        _controllerBindings[InputAction.Jump]               = new Binding(KeyCode.JoystickButton0) { displayName = "A" };
        _controllerBindings[InputAction.Boost]              = new Binding(KeyCode.JoystickButton1) { displayName = "B" };
        _controllerBindings[InputAction.AirRollPowerslide]  = new Binding(KeyCode.JoystickButton4) { displayName = "LB" };
        _controllerBindings[InputAction.AirRollLeft]        = new Binding(KeyCode.JoystickButton5) { displayName = "RB" };
        _controllerBindings[InputAction.BallCamToggle]      = new Binding(KeyCode.JoystickButton3) { displayName = "Y" };
        _controllerBindings[InputAction.Scoreboard]         = new Binding(KeyCode.JoystickButton9) { displayName = "Menu" };
    }

    // --- Public API ---

    public float GetValue(InputAction action)
    {
        float kb = _kbBindings.ContainsKey(action) ? _kbBindings[action].GetValue() : 0f;
        float ctrl = _controllerBindings.ContainsKey(action) ? _controllerBindings[action].GetValue() : 0f;
        return Mathf.Clamp(kb + ctrl, -1f, 1f);
    }

    public bool GetDown(InputAction action)
    {
        bool kb = _kbBindings.ContainsKey(action) && _kbBindings[action].GetDown();
        bool ctrl = _controllerBindings.ContainsKey(action) && _controllerBindings[action].GetDown();
        return kb || ctrl;
    }

    public bool GetHeld(InputAction action)
    {
        bool kb = _kbBindings.ContainsKey(action) && _kbBindings[action].GetHeld();
        bool ctrl = _controllerBindings.ContainsKey(action) && _controllerBindings[action].GetHeld();
        return kb || ctrl;
    }

    public Binding GetKBBinding(InputAction action)
    {
        return _kbBindings.ContainsKey(action) ? _kbBindings[action] : null;
    }

    public Binding GetControllerBinding(InputAction action)
    {
        return _controllerBindings.ContainsKey(action) ? _controllerBindings[action] : null;
    }

    public void SetKBBinding(InputAction action, Binding binding)
    {
        _kbBindings[action] = binding;
        SaveBindings();
    }

    public void SetControllerBinding(InputAction action, Binding binding)
    {
        _controllerBindings[action] = binding;
        SaveBindings();
    }

    public void ResetToDefaults()
    {
        SetDefaults();
        SaveBindings();
    }

    // --- Persistence ---

    private void SaveBindings()
    {
        foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
        {
            if (_kbBindings.ContainsKey(action))
            {
                var b = _kbBindings[action];
                PlayerPrefs.SetInt($"KB_{action}_type", (int)b.type);
                PlayerPrefs.SetInt($"KB_{action}_key", (int)b.key);
                PlayerPrefs.SetInt($"KB_{action}_axis", b.axisIndex);
                PlayerPrefs.SetString($"KB_{action}_name", b.displayName);
            }
            if (_controllerBindings.ContainsKey(action))
            {
                var b = _controllerBindings[action];
                PlayerPrefs.SetInt($"GP_{action}_type", (int)b.type);
                PlayerPrefs.SetInt($"GP_{action}_key", (int)b.key);
                PlayerPrefs.SetInt($"GP_{action}_axis", b.axisIndex);
                PlayerPrefs.SetString($"GP_{action}_name", b.displayName);
            }
        }
        PlayerPrefs.Save();
    }

    private void LoadBindings()
    {
        foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
        {
            string kbKey = $"KB_{action}_type";
            if (PlayerPrefs.HasKey(kbKey))
            {
                var b = new Binding(KeyCode.None);
                b.type = (BindingType)PlayerPrefs.GetInt($"KB_{action}_type");
                b.key = (KeyCode)PlayerPrefs.GetInt($"KB_{action}_key");
                b.axisIndex = PlayerPrefs.GetInt($"KB_{action}_axis");
                b.displayName = PlayerPrefs.GetString($"KB_{action}_name");
                _kbBindings[action] = b;
            }

            string gpKey = $"GP_{action}_type";
            if (PlayerPrefs.HasKey(gpKey))
            {
                var b = new Binding(KeyCode.None);
                b.type = (BindingType)PlayerPrefs.GetInt($"GP_{action}_type");
                b.key = (KeyCode)PlayerPrefs.GetInt($"GP_{action}_key");
                b.axisIndex = PlayerPrefs.GetInt($"GP_{action}_axis");
                b.displayName = PlayerPrefs.GetString($"GP_{action}_name");
                _controllerBindings[action] = b;
            }
        }
    }

    // --- Rebinding helpers ---

    /// <summary>
    /// Call this every frame during rebinding to detect what the user presses.
    /// Returns a new Binding if input detected, null otherwise.
    /// </summary>
    public Binding DetectInput(bool controllerMode)
    {
        if (controllerMode)
            return DetectControllerInput();
        else
            return DetectKeyboardInput();
    }

    private Binding DetectKeyboardInput()
    {
        // Check mouse buttons
        for (int i = 0; i < 3; i++)
        {
            if (Input.GetMouseButtonDown(i))
            {
                KeyCode mouseKey = KeyCode.Mouse0 + i;
                return new Binding(mouseKey);
            }
        }

        // Check keyboard
        if (Event.current == null) return null;

        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            // Skip joystick and mouse entries, and modifiers we don't want
            if (kc == KeyCode.None) continue;
            if (kc == KeyCode.Escape) continue;
            if ((int)kc >= (int)KeyCode.JoystickButton0) continue;
            if ((int)kc >= (int)KeyCode.Mouse0 && (int)kc <= (int)KeyCode.Mouse6) continue;

            if (Input.GetKeyDown(kc))
                return new Binding(kc);
        }

        return null;
    }

    private Binding DetectControllerInput()
    {
        // Check joystick buttons
        for (int i = 0; i <= 19; i++)
        {
            KeyCode jk = KeyCode.JoystickButton0 + i;
            if (Input.GetKeyDown(jk))
            {
                string name = GetJoystickButtonName(i);
                return new Binding(jk) { displayName = name };
            }
        }

        // Check joystick axes (triggers, sticks)
        for (int axis = 1; axis <= 10; axis++)
        {
            string axisName = "Joy Axis " + axis;
            try
            {
                float val = Input.GetAxisRaw(axisName);
                if (val > 0.7f)
                {
                    string name = GetAxisName(axis, true);
                    return new Binding(BindingType.JoystickAxisPositive, axis, name);
                }
                if (val < -0.7f)
                {
                    string name = GetAxisName(axis, false);
                    return new Binding(BindingType.JoystickAxisNegative, axis, name);
                }
            }
            catch { }
        }

        return null;
    }

    private string GetJoystickButtonName(int index)
    {
        switch (index)
        {
            case 0: return "A";
            case 1: return "B";
            case 2: return "X";
            case 3: return "Y";
            case 4: return "LB";
            case 5: return "RB";
            case 6: return "Back";
            case 7: return "Start";
            case 8: return "L Stick Press";
            case 9: return "R Stick Press";
            default: return "Button " + index;
        }
    }

    private string GetAxisName(int axis, bool positive)
    {
        switch (axis)
        {
            case 1: return positive ? "L Stick Right" : "L Stick Left";
            case 2: return positive ? "L Stick Down" : "L Stick Up";
            case 4: return positive ? "R Stick Right" : "R Stick Left";
            case 5: return positive ? "R Stick Down" : "R Stick Up";
            case 6: return positive ? "D-Pad Right" : "D-Pad Left";
            case 7: return positive ? "D-Pad Down" : "D-Pad Up";
            case 9: return "LT";
            case 10: return "RT";
            default: return "Axis " + axis + (positive ? "+" : "-");
        }
    }
}
