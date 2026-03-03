using System;
using UnityEngine;
using Netick;
using Netick.Unity;

/// Expanded input struct matching Rocket League's control model.
/// Ported from RocketSim's CarControls: throttle, steer, pitch, yaw, roll, jump, boost, handbrake.

[Networked]
[Serializable]
public struct GameInput : INetworkInput
{
    // Analog axes (-1 to 1)
    [Networked] public float Throttle { get; set; }  // Forward/backward drive
    [Networked] public float Steer { get; set; }  // Ground steering 
    [Networked] public float Pitch { get; set; }  // Air pitch 
    [Networked] public float Yaw { get; set; }  // Air yaw 
    [Networked] public float Roll { get; set; }  // Air roll

    // Digital buttons
    public NetworkBool Jump;
    public NetworkBool Boost;
    public NetworkBool Handbrake;  // Powerslide


    // Clamp all analog inputs to [-1, 1].

    public void Clamp()
    {
        Throttle = Mathf.Clamp(Throttle, -1f, 1f);
        Steer = Mathf.Clamp(Steer, -1f, 1f);
        Pitch = Mathf.Clamp(Pitch, -1f, 1f);
        Yaw = Mathf.Clamp(Yaw, -1f, 1f);
        Roll = Mathf.Clamp(Roll, -1f, 1f);
    }
}