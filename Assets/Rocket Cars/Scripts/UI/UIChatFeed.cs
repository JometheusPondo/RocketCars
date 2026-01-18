using Netick;
using Netick.Unity;
using System.Collections.Generic;
using UnityEngine;

public class UIChatFeed : MonoBehaviour
{
  public enum MessageType       
  {
    Player,
    Notification
  }

  public enum ChatScope
  {
    Global,
    Team
  }

  public enum AnchorCorner
  {
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
  }
  
  struct Message
  {
    public string                Name;
    public string                Content;
    public Color                 NameColor;
    public ChatScope             Scope;
    public MessageType           Type;
  }

  public bool                    IsChatting        => _chatOpen;

  [Header("Settings")]
  public int                     MaxInputLength    = 32;
  public int                     MaxMessages       = 10;
  public float                   VisibleSeconds    = 10f;
  public float                   BackspaceDelay    = 0.5f; // Wait half a second before rapid delete
  public float                   BackspaceInterval = 0.05f; // Delete every 0.05s during rapid delete
 
  [Header("Layout")]
  public AnchorCorner            FeedAnchor        = AnchorCorner.TopLeft;
  public Vector2                 FeedOffset        = new Vector2(20, 20);
  public float                   LineHeight        = 25f;
  public float                   InputWidth        = 450f;
  public int                     FontSize          = 14;

  [Header("Colors")]
  public Color                   RedTeamColor      = new Color(1f, 0.35f, 0.35f);
  public Color                   BlueTeamColor     = new Color(0.4f, 0.65f, 1f);
  public Color                   NotificationColor = Color.yellow;
  public Color                   BackgroundColor   = new Color(0, 0, 0, 0.7f);

  private readonly List<Message> _messages         = new(16);
  private bool                   _chatOpen;
  private string                 _input            = "";
  private ChatScope              _currentScope;
  private float                  _hideAtTime;
  private bool                   _feedVisible;  
  private float                  _nextBackspaceTime;

  private GUIStyle               _labelStyle;
  private GUIStyle               _boxStyle;
  private Texture2D              _bgTexture;
  private bool                   _stylesInitialized;
  private ChatSystem             _chatSystem;
  private GlobalData             _globalData;

  public void Init(GlobalData globalInfo)
  {
    _globalData = GetComponent<GlobalData>();
    _chatSystem = GetComponent<ChatSystem>();
    _chatSystem.SetFeed(this);

    if (_globalData.Sandbox.IsReplay)
    {
      _globalData.Sandbox.Replay.Playback.OnSeeked += (a, b) =>
      {
        _messages.Clear();
      };
    }
  }

  void Update()
  {
    if (Application.isBatchMode || _globalData == null || _chatSystem == null)
      return;

    if (!_globalData.IsReplay)
    {
      // open logic
      if (!_chatOpen && _globalData.CanUseInput && (!_globalData.IsReplay))
      {
        if (Input.GetKeyDown(KeyCode.Y))
          StartChat(ChatScope.Global);
        else if (Input.GetKeyDown(KeyCode.T))
          StartChat(ChatScope.Team);
      }
      else if (_chatOpen)
      {
        if (_globalData.Sandbox.InputEnabled)
          HandleTyping();
      }
    }

    if (!_chatOpen && _feedVisible && Time.time >= _hideAtTime)
      _feedVisible = false;
  }

  void StartChat(ChatScope scope)
  {
    _chatOpen     = true;
    _currentScope = scope;
    _input        = "";
    _feedVisible  = true;
  }

  void CloseChat()
  {
    _chatOpen     = false;
    _input        = "";
    _hideAtTime   = Time.time + VisibleSeconds;
  }

  public void PushMessage(string name, string content, Team team, ChatScope scope, MessageType type)
  {
    var nameCol = Color.grey;
    if (type == MessageType.Player)
    {
      if (team != Team.None)
        nameCol = (team == Team.Red ? RedTeamColor : BlueTeamColor);
    }
    else
      nameCol = NotificationColor;


    if (_messages.Count >= MaxMessages)
      _messages.RemoveAt(0);

    _messages.Add(new Message { Name = name, Content = content, NameColor = nameCol, Scope = scope, Type = type });
    _feedVisible = true;
    _hideAtTime = Time.time + VisibleSeconds;
  }


  void HandleTyping()
  {
    // submit
    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
    {
      SubmitMessage();
      return;
    }

    // cancel
    if (Input.GetKeyDown(KeyCode.Escape))
    {
      CloseChat();
      return;
    }

    // backspace
    if (Input.GetKey(KeyCode.Backspace))
    {
      if (Input.GetKeyDown(KeyCode.Backspace))
      {
        DeleteLastChar();
        _nextBackspaceTime = Time.time + BackspaceDelay;
      }
      else if (Time.time >= _nextBackspaceTime)
      {
        DeleteLastChar();
        _nextBackspaceTime = Time.time + BackspaceInterval;
      }
    }

    foreach (char c in Input.inputString)
    {
      if (c == '\b') 
        continue; // backspace handled above
      if (c == '\n' || c == '\r') 
        continue; // enters handled above

      if (_input.Length < MaxInputLength)
        _input += c;
    }
  }

  private void DeleteLastChar()
  {
    if (_input.Length > 0)
      _input = _input.Substring(0, _input.Length - 1);
  }

  void SubmitMessage()
  {
    if (!string.IsNullOrWhiteSpace(_input))
    {
      if (_globalData.Sandbox.TryGetLocalPlayerObject(out Player player))
        _chatSystem.RPC_SendChatMsgToServer_Joined(_input, player.Team, _currentScope);
      else
        _chatSystem.RPC_SendChatMsgToServer_Unjoined(_globalData.LocalPlayerName, _input);
    }
    CloseChat();
  }

  void OnGUI()
  {
    if (_globalData == null || _chatSystem == null || !_chatSystem.Sandbox.IsVisible || (_globalData != null && _globalData.HideUI))
      return;

    float scaleFactor    = Screen.height / 1080f;
    int scaledLineHeight = Mathf.RoundToInt(LineHeight * scaleFactor);
    int scaledInputWidth = Mathf.RoundToInt(InputWidth * scaleFactor);
    int scaledFontSize   = Mathf.RoundToInt(FontSize * scaleFactor);
    int scaledPadding    = Mathf.RoundToInt(5 * scaleFactor);

    if (!_stylesInitialized) InitStyles();

    _labelStyle.fontSize = scaledFontSize;

    Vector2 pos = GetLayoutPosition(scaledLineHeight, scaleFactor);
    float currentY = pos.y;

    if (_feedVisible)
    {
      foreach (var msg in _messages)
      {
        string scopePrefix = "";
        if (msg.Type == MessageType.Player)
          scopePrefix = msg.Scope == ChatScope.Team ? "[TEAM] " : "[ALL] ";

        var namePart  = !string.IsNullOrEmpty(msg.Name) ? $"{scopePrefix}{msg.Name}: " : "";
        var nameSize  = _labelStyle.CalcSize(new GUIContent(namePart));
        GUI.Box(new Rect(pos.x - scaledPadding, currentY, scaledInputWidth, scaledLineHeight), "", _boxStyle);
        GUI.color = msg.NameColor;
        GUI.Label(new Rect(pos.x, currentY, nameSize.x, scaledLineHeight), namePart, _labelStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(pos.x + nameSize.x, currentY, scaledInputWidth - nameSize.x, scaledLineHeight), msg.Content, _labelStyle);
        currentY += scaledLineHeight;
      }
    }

    if (_chatOpen)
    {
      currentY          += scaledPadding;
      string prefix      = _currentScope == ChatScope.Team ? "[TEAM]: " : "[ALL]: ";
      string displayText = prefix + _input + ((Time.time % 1.0f > 0.5f) ? "|" : "");

      GUI.Box(new Rect(pos.x - scaledPadding, currentY, scaledInputWidth, scaledLineHeight + Mathf.RoundToInt(4 * scaleFactor)), "", _boxStyle);
      GUI.color = Color.white;
      GUI.Label(new Rect(pos.x, currentY + Mathf.RoundToInt(2 * scaleFactor), scaledInputWidth, scaledLineHeight), displayText, _labelStyle);
    }
  }

  Vector2 GetLayoutPosition(int scaledLineHeight, float scaleFactor)
  {
    float offX = Mathf.RoundToInt(FeedOffset.x * scaleFactor);
    float offY = Mathf.RoundToInt(FeedOffset.y * scaleFactor);

    if (FeedAnchor == AnchorCorner.BottomLeft)
    {
      int spacing = Mathf.RoundToInt(10 * scaleFactor);
      float totalHeight = (_messages.Count * scaledLineHeight) + (_chatOpen ? scaledLineHeight + spacing : 0);
      return new Vector2(offX, Screen.height - offY - totalHeight);
    }
    return new Vector2(offX, offY);
  }

  void InitStyles()
  {
    _bgTexture = new Texture2D(1, 1);
    _bgTexture.SetPixel(0, 0, BackgroundColor);
    _bgTexture.Apply();

    _labelStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize = FontSize,
      alignment = TextAnchor.MiddleLeft,
      clipping = TextClipping.Clip,
      wordWrap = false
    };

    _boxStyle = new GUIStyle();
    _boxStyle.normal.background = _bgTexture;
    _stylesInitialized = true;
  }
}