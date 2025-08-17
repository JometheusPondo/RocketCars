using UnityEngine;

public class FuelAnimation : MonoBehaviour
{
    public float RotateSpeed = 15f;
    public Transform Render;

    private void LateUpdate()
    {
        Render.Rotate(0f, RotateSpeed * Time.deltaTime, 0f);
    }
}
