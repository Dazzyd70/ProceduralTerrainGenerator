using UnityEngine;

// Basic first-person controller for ground walking and flying, with mouse look and FOV effects.

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Walk Settings")]
    public float walkSpeed = 6f;
    public float jumpHeight = 0.5f;
    public float gravity = -5f;

    [Header("Flight Settings")]
    public float flightSpeed = 12f;
    public float flightFOV = 90f;

    [Header("Look Settings")]
    public float mouseSensitivity = 2f;

    [Header("FOV Transition")]
    public float fovTransitionSpeed = 4f;

    [Tooltip("Drag your Camera here")]
    public Camera playerCamera;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch = 0f;

    private bool isFlying = false;
    private float normalFOV;


    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        normalFOV = playerCamera.fieldOfView;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        HandleFOV();

        // Press F to toggle flying mode
        if (Input.GetKeyDown(KeyCode.F))
            isFlying = !isFlying;
    }

    // Mouse look for pitch and yaw
    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);

        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseY, -90f, 90f);
        playerCamera.transform.localEulerAngles = Vector3.right * pitch;
    }

    // Move either on ground (with jump/gravity) or fly freely in 3D
    void HandleMovement()
    {
        Vector3 move = Vector3.zero;

        if (isFlying)
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            float up = 0f;
            if (Input.GetKey(KeyCode.Space)) up += 1f;
            if (Input.GetKey(KeyCode.LeftShift)) up -= 1f;

            move = (transform.right * x + transform.forward * z + Vector3.up * up)
                   .normalized * flightSpeed;
            controller.Move(move * Time.deltaTime);
            verticalVelocity = 0f; // No gravity in flight
        }
        else
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            move = (transform.right * x + transform.forward * z) * walkSpeed;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // Keeps player grounded

            if (Input.GetButtonDown("Jump") && controller.isGrounded)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }
    }

    // Smoothly transitions field of view for walk/fly mode
    void HandleFOV()
    {
        float targetFOV = isFlying ? flightFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Time.deltaTime * fovTransitionSpeed
        );
    }
}
