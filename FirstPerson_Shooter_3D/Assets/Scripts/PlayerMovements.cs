using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovements : MonoBehaviour
{

    public CharacterController controller;
    Vector2 moveInput;
    public float speed = 5f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    Vector3 velocity;
    [Header("Jump")]
    public Transform groundCheck;
    bool isGrounded;
    public LayerMask groundMask;
    public float jumpHeight = 3f;
    public float sphereRadius = 0.3f;
    void Update()
    {

        velocity.y += gravity * Time.deltaTime; //aplicar la gravedad
        controller.Move(velocity * Time.deltaTime); //aplicamos movimiento 

        float x = moveInput.x; // obtenemos el valor horizontal del input
        float z = moveInput.y; // obtenemos el valor vertical del input 
        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        isGrounded = Physics.CheckSphere(groundCheck.position, sphereRadius, groundMask); //Verificamos si el jugador está en el suelo 
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; //Reinicia la velocidad vertical y evita que se acumule la gravedad
        }
    }
    #region
    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity); //calcula la velocidad necesaria
        }



    }
    #endregion
}
