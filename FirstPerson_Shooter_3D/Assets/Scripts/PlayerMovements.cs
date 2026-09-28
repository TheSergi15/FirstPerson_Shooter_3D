using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovements : MonoBehaviour
{
    [Header("Referencias")]
    public CharacterController controller;
    public Transform groundCheck;
    public LayerMask groundMask;
    public float sphereRadius = 0.3f;

    [Header("Movimiento")]
    public float speed = 5f;
    public float sprintSpeed = 8f;
    public float groundAcceleration = 12f; // qué tan rápido acelera en el suelo
    public float airAcceleration = 2.5f;   // bajo = conserva el impulso en el aire

    [Header("Gravedad")]
    public float gravity = -9.81f;
    public float terminalVelocity = -50f;  // velocidad máxima de caída

    [Header("Salto")]
    public float jumpHeight = 1.2f;
    public float coyoteTime = 0.12f;       // puedes saltar justo después de dejar un borde
    public float jumpBufferTime = 0.12f;   // se recuerda el salto pulsado justo antes de aterrizar

    Vector2 moveInput;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float coyoteTimer;
    float jumpBufferTimer;
    bool isGrounded;
    bool isSprinting;

    void Update()
    {
        float dt = Time.deltaTime;

        // --- Salto leído directamente del teclado (no depende del evento de PlayerInput) ---
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpBufferTimer = jumpBufferTime;

        // --- Comprobar suelo (CharacterController o esfera) ---
        isGrounded = (controller.isGrounded || Physics.CheckSphere(groundCheck.position, sphereRadius, groundMask))
                     && verticalVelocity <= 0f;

        // --- Temporizadores ---
        coyoteTimer = isGrounded ? coyoteTime : coyoteTimer - dt;
        jumpBufferTimer -= dt;

        // --- Movimiento horizontal con inercia ---
        float currentSpeed = isSprinting ? sprintSpeed : speed;
        Vector3 targetVelocity = (transform.right * moveInput.x + transform.forward * moveInput.y) * currentSpeed;
        float accel = isGrounded ? groundAcceleration : airAcceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * dt);

        // --- Vertical: suelo, salto y gravedad ---
        if (isGrounded)
            verticalVelocity = -2f; // mantiene al jugador pegado al suelo

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // Integración independiente del framerate (media de velocidad inicial y final)
        float previousVelocity = verticalVelocity;
        verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, terminalVelocity);
        float displacementY = (previousVelocity + verticalVelocity) * 0.5f * dt;

        // --- Aplicar TODO en un único Move ---
        Vector3 move = horizontalVelocity * dt + Vector3.up * displacementY;

        // Debug.Log($"Grounded: {isGrounded} | Buffer: {jumpBufferTimer:F2} | Coyote: {coyoteTimer:F2} | VelY: {verticalVelocity:F2}");
        controller.Move(move);

        // Golpe con el techo: cancelar la subida
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    #region Input
    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            jumpBufferTimer = jumpBufferTime;
    }

    public void OnSprint(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) isSprinting = true;   // tecla pulsada
        if (ctx.canceled) isSprinting = false;   // tecla soltada
    }
    #endregion
}