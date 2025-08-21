using UnityEngine;
using System;
using System.Collections;
using Netick.Unity;
using Netick;

public class CarWheel : MonoBehaviour
{
  public Transform Render;
  public Vector3   SideNormal;
  public bool      IsFront;

  [HideInInspector]
  public bool      IsGrounded;
  [HideInInspector]
  public float     Speed;
  public float     VisualSpeed;
}
