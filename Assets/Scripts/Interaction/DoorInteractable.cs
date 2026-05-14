using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Дверь / проход.
///
/// Без ключа:  requiredKey = null → открывается по E сразу.
/// С ключом:   requiredKey задан  → проверяет Inventory, потребляет ключ при открытии.
/// Стенд:      может открывать двери без ключа (interactable as usual),
///             с ключом — только игрок (ключ у него в инвентаре).
///
/// "Открытие" — отключает коллайдер(ы) и стреляет OnOpened.
/// Визуал (анимация, спрайт, исчезновение) — настрой через OnOpened в инспекторе.
/// </summary>
public class DoorInteractable : MonoBehaviour, IInteractable
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Interaction")]
    [Tooltip("true  — игрок/стенд могут открыть сами (E или командный полёт).\nfalse — открывается только рычагом/скриптом (ForceOpen).")]
    [SerializeField] private bool allowDirectInteraction = true;

    [Header("Lock")]
    [Tooltip("Нужный ключ. null = дверь без замка.")]
    [SerializeField] private ItemData requiredKey;

    [Header("Prompts")]
    [SerializeField] private string openPrompt   = "E — Открыть";
    [SerializeField] private string lockedPrompt = "Нужен ключ";

    [Header("Hint Bubble")]
    [Tooltip("Подсказка если дверь открывается только рычагом")]
    [SerializeField] private string leverHint    = "Где-то должен быть рычаг...";
    [Tooltip("Секунд до авто-закрытия подсказки")]
    [SerializeField] private float  hintDuration = 2.5f;
    [Tooltip("Минимальный интервал между подсказками (чтобы не спамило)")]
    [SerializeField] private float  hintCooldown = 4f;
    [Tooltip("Тег игрока для определения столкновения")]
    [SerializeField] private string playerTag    = "Player";

    [Header("On Open")]
    [Tooltip("Отключить эти коллайдеры при открытии (обычно — сам GameObject)")]
    [SerializeField] private Collider2D[] collidersToDisable;
    [Tooltip("Скрыть эти объекты при открытии (визуальная дверь)")]
    [SerializeField] private GameObject[] objectsToHide;

    [Header("Events")]
    public UnityEvent OnOpened;   // дверь открылась
    public UnityEvent OnLocked;   // попытка открыть без ключа

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private bool     _isOpen;
    private Animator _anim;
    private float    _hintHideAt;    // Time.time когда прятать подсказку
    private float    _nextHintAt;    // Time.time когда снова можно показывать

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
    }

    private void OnEnable()  => InteractableRegistry.Register(this);
    private void OnDisable() => InteractableRegistry.Unregister(this);

    private void Update()
    {
        // Авто-скрытие подсказки по таймеру
        if (_hintHideAt > 0f && Time.time >= _hintHideAt)
        {
            _hintHideAt = 0f;
            WorldBubble.Instance?.Hide();
        }
    }

    // ──────────────────────────────────────────
    // IInteractable
    // ──────────────────────────────────────────

    public string InteractionPrompt
    {
        get
        {
            if (!allowDirectInteraction) return string.Empty;
            if (_isOpen) return string.Empty;

            if (requiredKey != null)
            {
                bool hasKey = Inventory.Instance != null && Inventory.Instance.Has(requiredKey);
                return hasKey
                    ? $"E — Открыть ({requiredKey.itemName})"
                    : $"{lockedPrompt}: {requiredKey.itemName}";
            }

            return openPrompt;
        }
    }

    public void Interact(PlayerController player)
    {
        if (_isOpen) return;

        // Дверь без прямого взаимодействия — подсказать про рычаг
        if (!allowDirectInteraction)
        {
            ShowHint(leverHint);
            return;
        }

        // Проверка ключа
        if (requiredKey != null)
        {
            var inv = Inventory.Instance;
            if (inv == null || !inv.Has(requiredKey))
            {
                ShowHint($"{lockedPrompt}: {requiredKey.itemName}");
                OnLocked?.Invoke();
                return;
            }

            inv.Remove(requiredKey);
        }

        Open();
    }

    // ──────────────────────────────────────────
    // Физическое столкновение — подсказка при ударе в закрытую дверь
    // ──────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_isOpen) return;
        if (!col.gameObject.CompareTag(playerTag)) return;

        string hint = !allowDirectInteraction
            ? leverHint
            : requiredKey != null
                ? $"{lockedPrompt}: {requiredKey.itemName}"
                : string.Empty;

        if (!string.IsNullOrEmpty(hint))
            ShowHint(hint);
    }

    // ──────────────────────────────────────────
    // Открытие
    // ──────────────────────────────────────────

    private void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        WorldBubble.Instance?.Hide();

        foreach (var col in collidersToDisable)
            if (col != null) col.enabled = false;

        foreach (var go in objectsToHide)
            if (go != null) go.SetActive(false);

        _anim?.SetTrigger("Open");

        OnOpened?.Invoke();
    }

    /// <summary>
    /// Стенд принёс ключ физически (не через инвентарь).
    /// Возвращает true если ключ подошёл и дверь открылась.
    /// </summary>
    public bool TryOpenWithKey(ItemData key)
    {
        if (_isOpen) return false;

        // Дверь только по рычагу — ключ не поможет
        if (!allowDirectInteraction) return false;

        if (requiredKey != null && key != requiredKey)
        {
            OnLocked?.Invoke();
            return false;
        }

        Open();
        return true;
    }

    /// <summary>Открыть дверь из кода / события (без проверки ключа).</summary>
    public void ForceOpen() => Open();

    /// <summary>Закрыть дверь — включает коллайдеры и показывает визуал обратно.</summary>
    public void ForceClose()
    {
        if (!_isOpen) return;
        _isOpen = false;

        foreach (var col in collidersToDisable)
            if (col != null) col.enabled = true;

        foreach (var go in objectsToHide)
            if (go != null) go.SetActive(true);

        _anim?.SetTrigger("Close");
    }

    // ──────────────────────────────────────────
    // Подсказка
    // ──────────────────────────────────────────

    private void ShowHint(string text)
    {
        if (WorldBubble.Instance == null) return;
        if (Time.time < _nextHintAt) return;  // кулдаун

        _nextHintAt = Time.time + hintCooldown;
        _hintHideAt = Time.time + hintDuration;
        WorldBubble.Instance.Show(transform, text, hintDuration);
    }

    // ──────────────────────────────────────────
    // Свойства
    // ──────────────────────────────────────────

    public bool IsOpen => _isOpen;
}
