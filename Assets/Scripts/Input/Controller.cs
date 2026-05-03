using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class Controller : MonoBehaviour
{
    // ===== Enum Types =====
    private enum InputDeviceType { Unknown, MouseKeyboard, Gamepad, VR }

    // ===== Public Parameters - Movement =====
    public float moveSpeed = 3f;
    public float gravity = -9.81f;
    public float groundDistance = 0.2f;

    // ===== Public Parameters - Input =====
    public float inputDeadzone = 0.05f;
    public float controllerXSensitivity = 100f;
    public float controllerYSensitivity = 100f;
    public float mouseXSensitivity = 30f;
    public float mouseYSensitivity = 30f;

    // ===== Public Parameters - Rotation =====
    public float rotationSmoothing = 0.02f;
    public float maxRotationDegreesPerFrame = 15f;
    public float xRotation = 0f;

    // ===== Public Component References =====
    public XROrigin xrOrigin;
    public Camera mainCamera;
    public GameObject leftHand;
    public GameObject rightHand;
    public Behaviour gamepadRayInteractor;
    public Transform groundCheck;
    public LayerMask groundMask;

    // ===== Private Cached Components =====
    private CharacterController characterController;
    private PlayerInput playerInput;
    private TrackedPoseDriver trackedPoseDriver;

    // ===== Private Input Actions =====
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction attackAction;
    private InputAction interactAction;
    private InputAction crouchAction;
    private InputAction jumpAction;

    // ===== Private State Variables =====
    private InputDeviceType currentInputDevice = InputDeviceType.Unknown;
    private bool rotationPaused = false;
    private bool isGrounded;
    private Vector3 velocity;
    private float activeXSensitivity;
    private float activeYSensitivity;
    private float inputIgnoreUntil = 0f;
    private Vector2 rotateSmoothVelocity = Vector2.zero;
    private Vector2 lastRotateInput = Vector2.zero;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        trackedPoseDriver = mainCamera != null ? mainCamera.GetComponent<TrackedPoseDriver>() : null;
    }

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        SceneManager.sceneLoaded += OnSceneLoaded;

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

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Ignore input for the first 100 ms to avoid sudden camera jumps on scene start
        inputIgnoreUntil = Time.time + 0.1f;
    }

    void Update()
    {
        // Ignore all user input (including Escape/click) for a short startup window
        if (Time.time < inputIgnoreUntil)
            return;

        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector2 rotateInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        // Toggle pause on Escape: unlock and show cursor, stop rotation
        bool escapePressed;

        if (Keyboard.current != null)
            escapePressed = Keyboard.current.escapeKey.wasPressedThisFrame;
        else
            escapePressed = Input.GetKeyDown(KeyCode.Escape);

        if (escapePressed)
        {
            rotationPaused = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // If rotation is paused, resume when user clicks (left mouse) inside the window
        bool resumeClick;
        if (rotationPaused)
        {
            if (Mouse.current != null)
                resumeClick = Mouse.current.leftButton.wasPressedThisFrame;
            else
                resumeClick = Input.GetMouseButtonDown(0);

            if (resumeClick)
            {
                rotationPaused = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // ignore input briefly after re-lock to avoid spikes
                inputIgnoreUntil = Time.time + 0.1f;
                UpdateInputModeState();
            }
        }

        if (Cursor.lockState == CursorLockMode.Locked && !rotationPaused)
        {
            // smooth rotate input to reduce spikes
            Vector2 smoothedRotate = Vector2.SmoothDamp(lastRotateInput, rotateInput, ref rotateSmoothVelocity, rotationSmoothing);
            lastRotateInput = smoothedRotate;

            // Choose sensitivity based on the control that produced the look action if available
            float xsens = activeXSensitivity;
            float ysens = activeYSensitivity;
            if (lookAction != null && lookAction.activeControl != null)
            {
                var device = lookAction.activeControl.device;
                if (device is Gamepad)
                {
                    xsens = controllerXSensitivity;
                    ysens = controllerYSensitivity;
                }
                else if (device is Mouse || device is Pointer || device is Keyboard)
                {
                    xsens = mouseXSensitivity;
                    ysens = mouseYSensitivity;
                }
            }

            Move(moveInput);
            Rotate(smoothedRotate.x, smoothedRotate.y, xsens, ysens);
        }
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

    private void Rotate(float x, float y, float xsens, float ysens)
    {
        if (!IsFaceButtonActionPressed())
        {
            if (Mathf.Abs(x) >= inputDeadzone)
            {
                x = x * Time.deltaTime * xsens;
                x = Mathf.Clamp(x, -maxRotationDegreesPerFrame, maxRotationDegreesPerFrame);
                xrOrigin.transform.Rotate(Vector3.up * x);
            }

            if (Mathf.Abs(y) >= inputDeadzone)
            {
                y = y * Time.deltaTime * ysens;
                y = Mathf.Clamp(y, -maxRotationDegreesPerFrame, maxRotationDegreesPerFrame);
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CancelInvoke(nameof(UpdateInputModeState));
        Invoke(nameof(UpdateInputModeState), 0.01f);
    }
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Enabled:
            case InputDeviceChange.Disabled:
                CancelInvoke(nameof(UpdateInputModeState));
                Invoke(nameof(UpdateInputModeState), 0.1f);
                break;
        }
    }

    private void UpdateInputModeState()
    {
        bool hasVRControllers = HasConnectedVRControllers();
        bool hasXRHeadset = HasXRHeadset();

        if (trackedPoseDriver == null && mainCamera != null)
        {
            trackedPoseDriver = mainCamera.GetComponent<TrackedPoseDriver>();
        }

        if (trackedPoseDriver != null)
        {
            trackedPoseDriver.enabled = hasXRHeadset;
        }

        if (leftHand != null)
        {
            leftHand.SetActive(hasVRControllers);
        }

        if (rightHand != null)
        {
            rightHand.SetActive(hasVRControllers);
        }

        if (gamepadRayInteractor != null)
        {
            if (gamepadRayInteractor.gameObject.activeSelf != !hasVRControllers)
            {
                gamepadRayInteractor.gameObject.SetActive(!hasVRControllers);
            }

            gamepadRayInteractor.enabled = !hasVRControllers;
        }

        // Determine current input device and set active sensitivities.
        InputDeviceType detected = InputDeviceType.Unknown;

        if (hasVRControllers)
        {
            detected = InputDeviceType.VR;
        }
        else if (playerInput != null && !string.IsNullOrEmpty(playerInput.currentControlScheme))
        {
            string scheme = playerInput.currentControlScheme.ToLowerInvariant();
            if (scheme.Contains("mouse") || scheme.Contains("keyboard"))
                detected = InputDeviceType.MouseKeyboard;
            else if (scheme.Contains("gamepad") || scheme.Contains("xbox") || scheme.Contains("controller"))
                detected = InputDeviceType.Gamepad;
        }
        else
        {
            if (Gamepad.current != null && Gamepad.current.enabled)
                detected = InputDeviceType.Gamepad;
            else if (Mouse.current != null)
                detected = InputDeviceType.MouseKeyboard;
        }

        currentInputDevice = detected;
        switch (currentInputDevice)
        {
            case InputDeviceType.MouseKeyboard:
                activeXSensitivity = mouseXSensitivity;
                activeYSensitivity = mouseYSensitivity;
                break;
            case InputDeviceType.Gamepad:
            case InputDeviceType.VR:
            default:
                activeXSensitivity = controllerXSensitivity;
                activeYSensitivity = controllerYSensitivity;
                break;
        }
    }

    /// <summary>
    /// Проверяет наличие подключенного XR HMD (шлема) в системе.
    /// Включает TrackedPoseDriver, если найдётся шлем.
    /// </summary>
    /// <returns>true если найден XR HMD, иначе false</returns>
    private static bool HasXRHeadset()
    {
        Debug.Log("=== Проверка XR HMD устройств ===");

        foreach (InputDevice device in InputSystem.devices)
        {
            if (!device.enabled) continue;

            if (device is XRHMD)
            {
                Debug.Log($"✓ Найден XR HMD: {device.displayName}");
                return true;
            }

            string dn = device.displayName.ToLower();
            if (dn.Contains("oculus") || dn.Contains("quest") || dn.Contains("index") || dn.Contains("xr") || dn.Contains("vive") || dn.Contains("htc") || dn.Contains("openxr") || dn.Contains("openvr"))
            {
                Debug.Log($"✓ Предположительно XR HMD: {device.displayName}");
                return true;
            }
        }

        Debug.Log("✗ XR HMD не найдены");
        return false;
    }

    /// <summary>
    /// Проверяет наличие подключенных VR контроллеров HTC Vive в системе.
    /// </summary>
    /// <returns>true если найдены подключенные VR контроллеры, иначе false</returns>
    private static bool HasConnectedVRControllers()
    {
        Debug.Log("=== Проверка подключенных устройств ===");
        Debug.Log($"Всего устройств: {InputSystem.devices.Count}");

        foreach (InputDevice device in InputSystem.devices)
        {
            Debug.Log($"Устройство: {device.displayName} | ID: {device.deviceId} | Тип: {device.GetType().Name} | Включено: {device.enabled}");

            if (!device.enabled) continue;

            string displayName = device.displayName.ToLower();

            // Проверяем по названиям HTC Vive контроллеров
            if (displayName.Contains("vive") ||
                displayName.Contains("htc") ||
                displayName.Contains("openvr"))
            {
                Debug.Log($"✓ Найден VR контроллер: {device.displayName}");
                return true;
            }

            // Проверяем по типам устройств (XR контроллеры)
            // Дополнить когда будут известны конкретные типы контроллеров
            if (false)
            {
                Debug.Log($"✓ Найдено XR устройство: {device.displayName} (тип: {device.GetType().Name})");
                return true;
            }
        }

        Debug.Log("✗ VR контроллеры не найдены");
        return false;
    }
}
