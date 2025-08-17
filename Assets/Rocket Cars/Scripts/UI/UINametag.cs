using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Netick;
using Netick.Unity;
using System.Text;

public class UINametag : MonoBehaviour
{
  public Player           Player { get; private set; }

  [SerializeField]
  private TextMeshProUGUI _playerName;
  private StringBuilder   _playerNameCache    = new(60);
  private StringBuilder   _playerNameCachePre = new(60);

  public void Init(Player player)
  {
    Player                = player;
    _playerName.text      = Player.Name;
  }

  public void Refresh()
  {
    Player.Name.LoadIntoStringBuilder(_playerNameCache);
    _playerName.SetText(_playerNameCache);
    _playerNameCachePre.Clear();
    _playerNameCachePre.Append(_playerNameCache);
  }
}

