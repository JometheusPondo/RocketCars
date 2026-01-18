using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Netick;
using Netick.Unity;

public class ChatSystem : NetworkBehaviour
{
  public bool IsChatting => _feed != null && _feed.IsChatting;
  UIChatFeed _feed;

  public void SetFeed(UIChatFeed feed)
  {
    _feed = feed;
  }

  public void PushNotificationLocal(string content)
  {
    _feed?.PushMessage("Game", content, default, UIChatFeed.ChatScope.Global, UIChatFeed.MessageType.Notification);
  }

  public void PushNotificationLocal(string title, string content)
  {
    _feed?.PushMessage(title, content, default, UIChatFeed.ChatScope.Global, UIChatFeed.MessageType.Notification);
  }

  [Rpc(target: RpcPeers.Owner, isReliable: true)]
  public void RPC_SendChatMsgToServer_Unjoined(NetworkString32 name, NetworkString64 content, RpcContext ctx = default)
  {
    if (Sandbox.GetPlayerObject(ctx.Source) == null)
      RPC_SendChatMsgToAll_Unjoined(name, content);   // send to all
  }

  [Rpc(target: RpcPeers.Owner, isReliable: true)]
  public void RPC_SendChatMsgToServer_Joined(NetworkString64 content, Team team, UIChatFeed.ChatScope scope, RpcContext context = default)
  {
    if (Sandbox.TryGetPlayerObject<Player>(context.Source, out var player) == false)
      return;

    // send to all
    if (scope == UIChatFeed.ChatScope.Global)
    {
      for (int i = 0; i < Sandbox.Players.Count; i++)
        RPC_SendChatMsgToPlayer_Joined(Sandbox.Players[i], player.InputSourcePlayerId, content, scope);
    }
    else // only to team
    {
      foreach (var playerId in Sandbox.Players)
        if (Sandbox.TryGetPlayerObject(playerId, out Player playerObj) && playerObj.Team == player.Team)
          RPC_SendChatMsgToPlayer_Joined(playerId, player.InputSourcePlayerId, content, scope);
    }

    // send to replay player
    RPC_SendChatMsgToPlayer_Joined(NetworkPlayerId.ReplayPlayer, player.InputSourcePlayerId, content, scope);
  }

  [Rpc(source: RpcPeers.Owner, target: RpcPeers.Everyone, isReliable: true)]
  public void RPC_SendChatMsgToPlayer_Joined([RpcTarget] NetworkPlayerId target, NetworkPlayerId msgerId, NetworkString64 content, UIChatFeed.ChatScope scope, RpcContext ctx = default)
  {
    Team team = Sandbox.TryGetPlayerObject<Player>(msgerId, out var player) == true ? player.Team : Team.None;
    _feed?.PushMessage(team != Team.None ? player.Name : default, content, team, scope, UIChatFeed.MessageType.Player);
  }

  [Rpc(source: RpcPeers.Owner, target: RpcPeers.Everyone, isReliable: true, localInvoke: true)]
  public void RPC_SendChatMsgToAll_Unjoined(NetworkString32 name, NetworkString64 content, RpcContext ctx = default)
  {
    _feed?.PushMessage(name, content, Team.None, UIChatFeed.ChatScope.Global, UIChatFeed.MessageType.Player);
  }

  [Rpc(source: RpcPeers.Owner, target: RpcPeers.Everyone, isReliable: true, localInvoke: true)]
  public void RPC_SendNotificationMsgToAll(NetworkString64 content, RpcContext ctx = default)
  {
    _feed?.PushMessage(default, content, default, UIChatFeed.ChatScope.Global, UIChatFeed.MessageType.Notification);
  }
}
