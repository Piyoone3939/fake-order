using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// スパイ側のメインコントローラー
/// FPS視点での移動とインタラクション管理
/// </summary>
public class SpyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 90f;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    private Vector3 velocity = Vector3.zero;
    private Vector2 moveInput = Vector2.zero;
    private float xRotation = 0f;
    private bool isMoving = false;
    private bool isSprinting = false;
    private bool isPerformingSuspiciousAction = false;
    private bool inputEnabled = false;
    private SpyUI spyUI;
    private IInteractable currentLookTarget;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        spyUI = FindAnyObjectByType<SpyUI>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        ApplyControlState();
    }

    private void Update()
    {
        if (!inputEnabled) return;

        UpdateInteractionTarget();
        HandleInput();
        UpdateMovement();
        UpdateCamera();
    }

    private void UpdateInteractionTarget()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        IInteractable target = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange))
            target = hit.collider.GetComponent<IInteractable>();

        currentLookTarget = target;
        if (spyUI == null) return;

        // 同じ対象を注視し続けていてもプロンプト文言は変化しうる（例：ハッキング開始でnullになる）ため毎フレーム反映する
        string prompt = currentLookTarget?.GetInteractionPrompt();
        if (string.IsNullOrEmpty(prompt))
            spyUI.ClearInteractionPrompt();
        else
            spyUI.SetInteractionPrompt(prompt);
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveInput = Vector2.zero;
            isMoving = false;
            isSprinting = false;
            return;
        }

        // 移動入力
        float moveX = 0f;
        float moveY = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveY -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveY += 1f;
        moveInput = new Vector2(moveX, moveY);
        isMoving = moveInput.sqrMagnitude > 0f;

        // スプリント
        isSprinting = keyboard.leftShiftKey.isPressed && isMoving;

        // インタラクション
        if (keyboard.eKey.wasPressedThisFrame)
            TryInteract();

        // マウスロック解除（デバッグ用）
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            bool isLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isLocked;
        }
    }

    private void UpdateMovement()
    {
        Vector3 moveDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        
        velocity.x = moveDir.x * currentSpeed;
        velocity.z = moveDir.z * currentSpeed;

        // 重力
        if (characterController.isGrounded)
        {
            velocity.y = -0.5f;
        }
        else
        {
            velocity.y -= 9.81f * Time.deltaTime;
        }

        characterController.Move(velocity * Time.deltaTime);
    }

    private void UpdateCamera()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity * 0.02f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.02f;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void TryInteract()
    {
        currentLookTarget?.Interact(this);
    }

    public void OnGameStart()
    {
        Debug.Log("🕵️ Spy: Game started!");
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public void TeleportTo(Vector3 destination)
    {
        bool wasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;
        transform.position = destination;
        velocity = Vector3.zero;
        if (characterController != null)
            characterController.enabled = wasEnabled;
    }

    public bool IsSprinting()
    {
        return isSprinting;
    }

    public bool IsPerformingSuspiciousAction()
    {
        return isPerformingSuspiciousAction;
    }

    public void SetSuspiciousAction(bool active)
    {
        isPerformingSuspiciousAction = active;
    }

    public void SetControlEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (!enabled)
        {
            moveInput = Vector2.zero;
            isMoving = false;
            isSprinting = false;
            isPerformingSuspiciousAction = false;
            currentLookTarget = null;
            spyUI?.ClearInteractionPrompt();
        }

        ApplyControlState();
    }

    private void ApplyControlState()
    {
        if (playerCamera != null)
        {
            playerCamera.enabled = inputEnabled;
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = inputEnabled;
        }

        if (inputEnabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void AddInformation(int terminalId, Vector3 position, bool isTrap = false)
    {
        GameState.Instance.AddCollectedInformation(terminalId, isTrap, position);
        if (spyUI != null)
            spyUI.UpdateInformationDisplay();
    }
}

public interface IInteractable
{
    void Interact(SpyController spy);
    string GetInteractionPrompt();
}
