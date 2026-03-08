using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

public class Controller : MonoBehaviour
{

    public float moveSpeed = 3f;
    public float inputDeadzone = 0.05f;
    public float controllerXSensitivity = 100f;
    public float controllerYSensitivity = 100f;
    public float xRotation = 0f;
    public float gravity = -9.81f;
    public float groundDistance = 0.2f;

    public XROrigin xrOrigin;
    public Camera mainCamera;
    public Vector3 velocity;
    public Transform groundCheck;
    public LayerMask groundMask;

    private CharacterController characterController;
    private Gamepad gamepad;
    private bool isGrounded;

    void Start()
    {
        gamepad = Gamepad.current;

        Cursor.lockState = CursorLockMode.Locked;

        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        Vector2 moveInput = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
        Vector2 rotateInput = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;

        Move(moveInput);
        Rotate(rotateInput.x, rotateInput.y);
    }

    private void Move(Vector2 input)
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = mainCamera.transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 move = right * input.x + forward * input.y;

        characterController.Move(move * moveSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void Rotate(float x, float y)
    {
        if (gamepad == null)
        {
            return;
        }

        if (!gamepad.buttonSouth.isPressed && !gamepad.buttonNorth.isPressed && !gamepad.buttonEast.isPressed && !gamepad.buttonWest.isPressed)
        {
            if (Mathf.Abs(x) >= inputDeadzone)
            {
                x = x * Time.deltaTime * controllerXSensitivity;
                xrOrigin.transform.Rotate(Vector3.up * x);
            }

            if (Mathf.Abs(y) >= inputDeadzone)
            {
                y = y * Time.deltaTime * controllerYSensitivity;
                xRotation -= y;
                xRotation = Mathf.Clamp(xRotation, -90f, 90f);
                mainCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
        }
    }
}
