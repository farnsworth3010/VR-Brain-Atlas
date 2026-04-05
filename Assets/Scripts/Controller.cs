using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
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
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction attackAction;
    private InputAction interactAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private bool isGrounded;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
    }

    void OnEnable()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError("Controller requires a PlayerInput component with an assigned Input Actions asset.", this);
            return;
        }

        moveAction = playerInput.actions.FindAction("Move", false);
        lookAction = playerInput.actions.FindAction("Look", false);
        attackAction = playerInput.actions.FindAction("Attack", false);
        interactAction = playerInput.actions.FindAction("Interact", false);
        crouchAction = playerInput.actions.FindAction("Crouch", false);
        jumpAction = playerInput.actions.FindAction("Jump", false);
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector2 rotateInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

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
        if (!IsFaceButtonActionPressed())
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

    private bool IsFaceButtonActionPressed()
    {
        return IsPressed(jumpAction)
            || IsPressed(interactAction)
            || IsPressed(crouchAction)
            || IsPressed(attackAction);
    }

    private static bool IsPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }
}
