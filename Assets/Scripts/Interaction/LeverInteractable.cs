using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Рычаг / кнопка — активирует другие объекты через UnityEvent.
///
/// Режимы:
///   Toggle  (isOneShot = false) — переключается туда-обратно каждое нажатие.
///   Button  (isOneShot = true)  — срабатывает один раз, после блокируется.
///
/// Подключение в инспекторе:
///   OnActivated   → DoorInteractable.ForceOpen / Platform.StartMove / GameObject.SetActive(true) …
///   OnDeactivated → (только Toggle) обратное действие
///
/// Стенд тоже может дёргать рычаг — Interact(null) работает корректно.
/// </summary>
public class LeverInteractable : MonoBehaviour, IInteractable
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Mode")]
    [Tooltip("false = рычаг (toggle), true = кнопка (одноразово)")]
    [SerializeField] private bool isOneShot = false;
    [Tooltip("Начальное состояние")]
    [SerializeField] private bool startsOn  = false;

    [Header("Prompts")]
    [SerializeField] private string promptOff = "E — Потянуть рычаг";
    [SerializeField] private string promptOn  = "E — Вернуть рычаг";
    [SerializeField] private string promptDone = "Уже активировано";   // для isOneShot

    [Header("Visuals")]
    [Tooltip("Спрайт в выключенном состоянии")]
    [SerializeField] private Sprite spriteOff;
    [Tooltip("Спрайт во включённом состоянии")]
    [SerializeField] private Sprite spriteOn;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animTriggerOn  = "On";
    [SerializeField] private string animTriggerOff = "Off";

    [Header("Events")]
    public UnityEvent OnActivated;    // рычаг включён
    public UnityEvent OnDeactivated;  // рычаг выключен (только Toggle)

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private bool _isOn;
    private bool _used; // для isOneShot

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _isOn = startsOn;
        ApplyVisuals();
    }

    private void OnEnable()  => InteractableRegistry.Register(this);
    private void OnDisable() => InteractableRegistry.Unregister(this);

    // ──────────────────────────────────────────
    // IInteractable
    // ──────────────────────────────────────────

    public string InteractionPrompt
    {
        get
        {
            if (isOneShot && _used) return promptDone;
            return _isOn ? promptOn : promptOff;
        }
    }

    public void Interact(PlayerController player)
    {
        if (isOneShot && _used) return;

        _isOn = !_isOn;

        if (_isOn)
        {
            if (isOneShot) _used = true;
            animator?.SetTrigger(animTriggerOn);
            OnActivated?.Invoke();
        }
        else
        {
            animator?.SetTrigger(animTriggerOff);
            OnDeactivated?.Invoke();
        }

        ApplyVisuals();
    }

    // ──────────────────────────────────────────
    // Визуал
    // ──────────────────────────────────────────

    private void ApplyVisuals()
    {
        if (spriteRenderer == null) return;

        if (_isOn && spriteOn != null)
            spriteRenderer.sprite = spriteOn;
        else if (!_isOn && spriteOff != null)
            spriteRenderer.sprite = spriteOff;
    }

    // ──────────────────────────────────────────
    // Публичный доступ
    // ──────────────────────────────────────────

    public bool IsOn  => _isOn;
    public bool IsUsed => _used;

    /// <summary>Принудительно включить/выключить из кода (без Interact).</summary>
    public void ForceSet(bool on)
    {
        if (isOneShot && _used) return;
        _isOn = on;
        if (on) { if (isOneShot) _used = true; OnActivated?.Invoke(); }
        else OnDeactivated?.Invoke();
        ApplyVisuals();
    }
}
