using UnityEngine;

public class CameraLookTouch : MonoBehaviour
{
    public Transform playerBody;     
    public TouchLookHandler swipe; 
    public float sensitivity = 0.12f;

    float pitch;

    void Update()
    {
        if (playerBody == null || swipe == null) return;

        Vector2 d = swipe.LookDelta;

        float yaw = d.x * sensitivity;
        float yPitch = d.y * sensitivity;

        pitch -= yPitch;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        playerBody.Rotate(Vector3.up * yaw);
    }
}
