using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovements : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float sprintSpeed = 8f;
    public float groundAcceleration = 12f;   // al pulsar una dirección
    public float groundFriction = 14f;       // frenado al soltar las teclas
    public float airAcceleration = 3f;       // control en el aire (dirige, no crea velocidad)

    [Header("Gravedad y resistencia del aire")]
    public float gravity = -9.81f;
    public float airDrag = 0.0032f;          // velocidad terminal ≈ sqrt(9.81 / airDrag) ≈ 55 m/s

    [Header("Salto")]
    public float jumpHeight = 1.2f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Aterrizaje")]
    public float hardLandingSpeed = 8f;            // impacto "fuerte" (≈ caída de 3 m)
    public float hardLandingMomentumLoss = 0.6f;   // 0-1: impulso horizontal que se pierde
    public float recoveryTime = 0.35f;             // tiempo torpe tras un aterrizaje fuerte
    public float recoverySpeedFactor = 0.5f;       // velocidad durante la recuperación

    public event Action<float> Landed;             // velocidad de impacto (para cámara, sonido, daño)

    const float MinLandingSpeed = 3f;

    CharacterController controller;
    Vector2 moveInput;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float coyoteTimer;
    float jumpBufferTimer;
    float recoveryTimer;
    bool isSprinting;
    bool wasGrounded;

    void Awake() => controller = GetComponent<CharacterController>();

    void Update()
    {
        float dt = Time.deltaTime;
        bool grounded = controller.isGrounded;

        if (grounded && !wasGrounded) HandleLanding();
        wasGrounded = grounded;

        ReadJumpInput();
        UpdateTimers(dt, grounded);
        UpdateHorizontalVelocity(dt, grounded);
        UpdateVerticalVelocity(dt, grounded, out float displacementY);

        controller.Move(horizontalVelocity * dt + Vector3.up * displacementY);

        // Golpe con el techo: cancelar la subida
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    void HandleLanding()
    {
        float impact = -verticalVelocity;   // aún guarda la velocidad de caída
        if (impact < MinLandingSpeed) return;

        Landed?.Invoke(impact);

        if (impact >= hardLandingSpeed)
        {
            horizontalVelocity *= 1f - hardLandingMomentumLoss;
            recoveryTimer = recoveryTime;
        }
    }

    void ReadJumpInput()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpBufferTimer = jumpBufferTime;
    }

    void UpdateTimers(float dt, bool grounded)
    {
        coyoteTimer = (grounded && verticalVelocity <= 0f) ? coyoteTime : coyoteTimer - dt;
        jumpBufferTimer -= dt;
        recoveryTimer -= dt;
    }

    void UpdateHorizontalVelocity(float dt, bool grounded)
    {
        Vector3 direction = Vector3.ClampMagnitude(transform.right * moveInput.x + transform.forward * moveInput.y, 1f);
        bool hasInput = direction.sqrMagnitude > 0.0001f;
        float targetSpeed = (isSprinting ? sprintSpeed : speed) * (recoveryTimer > 0f ? recoverySpeedFactor : 1f);

        if (grounded)
        {
            // Suelo: aceleras hacia la velocidad objetivo y la fricción te frena
            float accel = hasInput ? groundAcceleration : groundFriction;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, direction * targetSpeed, accel * dt);
        }
        else if (hasInput)
        {
            // Aire: puedes dirigirte, pero no ganas velocidad de la nada
            float maxSpeed = Mathf.Max(horizontalVelocity.magnitude, targetSpeed);
            horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity + direction * airAcceleration * dt, maxSpeed);
        }
        // Aire sin input: se conserva el impulso (inercia)
    }

    void UpdateVerticalVelocity(float dt, bool grounded, out float displacementY)
    {
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // mantiene al jugador pegado al suelo

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // Gravedad + resistencia del aire (siempre se opone al movimiento)
        float previous = verticalVelocity;
        float acceleration = gravity - airDrag * verticalVelocity * Mathf.Abs(verticalVelocity);
        verticalVelocity += acceleration * dt;
        displacementY = (previous + verticalVelocity) * 0.5f * dt;
    }

    #region Input
    public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    public void OnSprint(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) isSprinting = true;
        if (ctx.canceled) isSprinting = false;
    }
    #endregion
}