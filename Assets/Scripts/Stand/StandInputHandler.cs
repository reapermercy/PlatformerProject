using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Обработчик ввода стенда.
///
/// Схема управления ПКМ:
///   Hold  ПКМ  → ручное управление (стенд летит за курсором)
///   1 клик     → командный полёт к точке → действие → возврат к игроку
///   2 клика    → командный полёт к точке → действие → остаётся там
/// </summary>
public class StandInputHandler : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Owner")]
    [SerializeField] private PlayerController ownerController;
    [SerializeField] private StandController  standController;

    [Header("Click Detection")]
    [Tooltip("Если ПКМ зажат дольше — это hold, иначе клик")]
    [SerializeField] private float holdThreshold = 0.2f;

    [Tooltip("Интервал между кликами для распознавания двойного клика")]
    [SerializeField] private float doubleClickThreshold = 0.35f;

    // ──────────────────────────────────────────
    // Events → подключаются в инспекторе к StandController
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent          OnToggle;             // Q — Attached ↔ Free
    public UnityEvent<Vector2> OnMoveToPosition;     // Hold ПКМ — ручное управление
    public UnityEvent          OnMoveReleased;        // Hold ПКМ отпущен
    public UnityEvent<Vector2> OnCommandFly;          // 1 клик — лети → действие → вернись
    public UnityEvent<Vector2> OnCommandStay;         // 2 клика — лети → действие → стой
    public UnityEvent<Vector2> OnMouseWorldUpdated;  // позиция курсора каждый кадр
    public UnityEvent<int>     OnAbilityUsed;         // 0/1/2 — способность
    public UnityEvent<Vector2> OnStandLeftAction;    // ЛКМ при зажатом ПКМ — подобрать или бросить

    // ──────────────────────────────────────────
    // Приватные поля
    // ──────────────────────────────────────────

    private Camera  _cam;
    private Vector2 _mouseWorldPos;

    private bool  _rmbHeld;
    private float _rmbHoldTimer;
    private bool  _rmbWasHold;          // зажат достаточно долго → hold, не клик

    private float _lastClickTime = -999f;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _cam = Camera.main;
        if (ownerController == null)
            ownerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (!IsStandFree()) return;

        // ЛКМ при зажатом ПКМ — подобрать или бросить (зависит от состояния стенда)
        // Достаточно что ПКМ зажат (_rmbHeld), не нужно ждать holdThreshold
        if (_rmbHeld && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            OnStandLeftAction?.Invoke(_mouseWorldPos);

        if (!_rmbHeld) return;

        _rmbHoldTimer += Time.unscaledDeltaTime;

        if (_rmbHoldTimer >= holdThreshold && !_rmbWasHold)
            _rmbWasHold = true;

        if (_rmbWasHold)
            OnMoveToPosition?.Invoke(_mouseWorldPos);
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    private bool IsStandFree() =>
        ownerController != null && ownerController.Context.StandIsFree;

    // ──────────────────────────────────────────
    // PlayerInput callbacks
    // ──────────────────────────────────────────

    public void OnToggleStand(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        OnToggle?.Invoke();
    }

    public void OnStandAction(InputAction.CallbackContext ctx)
    {
        if (!IsStandFree()) return;

        if (ctx.started)
        {
            _rmbHeld      = true;
            _rmbHoldTimer = 0f;
            _rmbWasHold   = false;
            SetManualControl(true);
        }
        else if (ctx.canceled)
        {
            SetManualControl(false);

            if (_rmbWasHold)
            {
                // Было ручное управление — сообщаем об отпускании
                OnMoveReleased?.Invoke();
            }
            else
            {
                // Был быстрый клик — одиночный или двойной?
                float now = Time.unscaledTime;
                bool isDouble = (now - _lastClickTime) < doubleClickThreshold;
                _lastClickTime = now;

                if (isDouble)
                    OnCommandStay?.Invoke(_mouseWorldPos);
                else
                    OnCommandFly?.Invoke(_mouseWorldPos);
            }

            _rmbHeld    = false;
            _rmbWasHold = false;
        }
    }

    public void OnMousePosition(InputAction.CallbackContext ctx)
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        Vector2 screenPos = ctx.ReadValue<Vector2>();
        float   depth     = Mathf.Abs(_cam.transform.position.z);
        Vector3 world     = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        _mouseWorldPos    = new Vector2(world.x, world.y);

        // Обновляем позицию мыши в StandController напрямую (для ProjectileAbility и др.)
        standController?.UpdateMousePosition(_mouseWorldPos);

        if (IsStandFree())
            OnMouseWorldUpdated?.Invoke(_mouseWorldPos);
    }

    // Цифры 1-3 идут в инвентарь пока хотбар открыт — иначе в способности стенда
    public void OnAbility1(InputAction.CallbackContext ctx) { if (ctx.started && !InventoryUI.IsHotbarOpen) OnAbilityUsed?.Invoke(0); }
    public void OnAbility2(InputAction.CallbackContext ctx) { if (ctx.started && !InventoryUI.IsHotbarOpen) OnAbilityUsed?.Invoke(1); }
    public void OnAbility3(InputAction.CallbackContext ctx) { if (ctx.started && !InventoryUI.IsHotbarOpen) OnAbilityUsed?.Invoke(2); }

    public Vector2 MouseWorldPosition => _mouseWorldPos;

    private void SetManualControl(bool active)
    {
        if (ownerController != null)
            ownerController.Context.StandManualControl = active && IsStandFree();
    }
}
