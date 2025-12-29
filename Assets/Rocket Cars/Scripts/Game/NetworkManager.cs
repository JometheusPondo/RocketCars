//using Netick;
//using Netick.Unity;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Runtime.InteropServices;
//using UnityEngine;
//using UnityEngine.SceneManagement;
//using Network = Netick.Unity.Network;

//public struct RocketCarsRequestData
//{
//  public int GameVersionHash;
//}

//[ExecutionOrder(-30)]
//public class NetworkManager : MonoBehaviour
//{
//  public GlobalInfo                          GlobalInfo                  { get; private set; }

//  // const
//  public static readonly byte[]              BadRequestError             = System.Text.Encoding.ASCII.GetBytes("You sent a bad request!");
//  public static readonly byte[]              BadVersionError             = System.Text.Encoding.ASCII.GetBytes("Your game is running a differnt build version than the server!");

//  //public override void NetworkAwake()
//  //{
//  //  GlobalInfo                               = Sandbox.GetComponent<GlobalInfo>();
//  //  Sandbox.Events.OnDisconnectedFromServer += OnDisconnectedFromServer;
//  //  Sandbox.Events.OnConnectRequest         += OnConnectRequest;
//  //}

//  public void OnConnectRequest(NetworkSandbox sandbox, NetworkConnectionRequest request)
//  {
//    if (!GlobalInfo.StartedThroughMainMenu) // accept all connections if not started through menu scene, this means the game was started in a map directly for testing reasons.
//      return;

//    if (request.DataLength < Marshal.SizeOf<RocketCarsRequestData>())
//      request.Refuse(BadRequestError);

//    RocketCarsRequestData dataStruct = MemoryMarshal.Read<RocketCarsRequestData>(request.Data);

//    if (dataStruct.GameVersionHash != Netick.Unity.Network.GameVersion)
//      request.Refuse(BadVersionError);
//  }

//  public void OnDisconnectedFromServer(NetworkSandbox sandbox, NetworkConnection server, TransportDisconnectReason transportDisconnectReason)
//  {
//    // if started in client mode (single-peer mode), when we are disconnected from the server we shut down Netick and switch to the menu scene.
//    if (Network.Instance != null && Network.StartMode == StartMode.Client)
//    {
//      // shutting down Netick.
//      Netick.Unity.Network.Shutdown();
//      // we use the regular Unity API for scene management instead of Netick scene management API, because we just shut down Netick and it's no longer in control of the game.
//      SceneManager.LoadScene(0);
//    }
//  }
//}