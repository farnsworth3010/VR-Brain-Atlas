using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.XInput;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class Controller : MonoBehaviour
{

    public float moveSpeed = 3f;
    public float inputDeadzone = 0.05f;
    public float controllerXSensitivity = 100f;
    public float controllerYSensitivity = 100f;
    /// <summary>Текущий угол вращения камеры по оси X</summary>
    public float xRotation = 0f;
    /// <summary>Сила гравитации, воздействующая на персонажа</summary>
    public float gravity = -9.81f;
    public float groundDistance = 0.2f;

    public XROrigin xrOrigin;
    /// <summary>Основная камера сцены для расчета направления взгляда</summary>
    public Camera mainCamera;
    public GameObject leftHand;
    public GameObject rightHand;
    public Behaviour gamepadRayInteractor;
    public Vector3 velocity;
    public Transform groundCheck;
    public LayerMask groundMask;

    private CharacterController characterController;
    private PlayerInput playerInput;
    /// <summary>Компонент для отслеживания позы камеры (VR)</summary>
    private TrackedPoseDriver trackedPoseDriver;
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
    }

    void Update()
    {
        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector2 rotateInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        if (Application.isFocused)
        {
            Move(moveInput);
            Rotate(rotateInput.x, rotateInput.y);
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

    /// <summary>
    /// Обрабатывает поворот камеры и XR Origin на основе входных данных.
    /// Применяет мертвую зону входа и ограничивает угол вращения по оси X.
    /// Игнорирует ввод, если нажата одна из кнопок действий.
    /// </summary>
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

    /// <summary>
    /// Обработчик события загрузки сцены.
    /// Обновляет состояние режима ввода при загрузке новой сцены.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CancelInvoke(nameof(UpdateInputModeState));
        Invoke(nameof(UpdateInputModeState), 0.01f);
    }

    /// <summary>
    /// Обработчик события изменения устройства ввода.
    /// Обновляет состояние режима ввода при подключении, отключении или изменении статуса устройства.
    /// </summary>
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

    /// <summary>
    /// Обновляет состояние режима ввода на основе подключенных устройств.
    /// Если подключены VR контроллеры: активирует компоненты и включает TrackedPoseDriver.
    /// Если VR контроллеры отключены: деактивирует компоненты и включает gamepadRayInteractor.
    /// </summary>
    private void UpdateInputModeState()
    {
        bool hasVRControllers = HasConnectedVRControllers();

        if (trackedPoseDriver == null && mainCamera != null)
        {
            trackedPoseDriver = mainCamera.GetComponent<TrackedPoseDriver>();
        }

        if (trackedPoseDriver != null)
        {
            trackedPoseDriver.enabled = hasVRControllers;
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
            if (device.GetType().Name.Contains("XR") || device.GetType().Name.Contains("TrackedDevice"))
            {
                Debug.Log($"✓ Найдено XR устройство: {device.displayName} (тип: {device.GetType().Name})");
                return true;
            }
        }

        Debug.Log("✗ VR контроллеры не найдены");
        return false;
    }
}
