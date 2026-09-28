using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("Referencias")]
    public Transform playerBody;          // el objeto Player

    [Header("Sensibilidad")]
    public float sensitivity = 0.1f;
    public float maxLookAngle = 85f;

    float xRotation;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 lookInput = Mouse.current.delta.ReadValue(); // lee el ratón directamente

        float mouseX = lookInput.x * sensitivity;
        float mouseY = lookInput.y * sensitivity;

        xRotation = Mathf.Clamp(xRotation - mouseY, -maxLookAngle, maxLookAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        playerBody.Rotate(Vector3.up * mouseX);
    }
}