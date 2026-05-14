using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Контрольная точка: при касании игрока запоминает позицию для респавна
/// и (опционально) сохраняет игру в указанный слот.
///
/// Требования:
///   — Collider2D (IsTrigger = true)
///   — на игроке тэг "Player" или слой из playerLayer
///
/// Использование:
///   — CheckpointManager.Instance.RespawnPosition — позиция последнего чекпоинта.
///   — При смерти игрока (GameManager) можно дернуть CheckpointManager для респавна
///     вместо перезагрузки сцены.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Если задан — только объекты из этих слоёв могут активировать")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Behaviour")]
    [Tooltip("Запоминать эту точку как точку респавна")]
    [SerializeField] private bool setRespawnPoint = true;

    [Tooltip("Восстановить HP игрока до максимума при активации")]
    [SerializeField] private bool healToFull = true;

    [Tooltip("Использовать один раз — после активации отключиться")]
    [SerializeField] private bool singleUse = false;

    [Tooltip("Автосохранение в слот. -1 = не сохранять.")]
    [SerializeField] private int autoSaveSlot = -1;

    [Header("Respawn Offset")]
    [Tooltip("Смещение точки респавна относительно позиции чекпоинта")]
    [SerializeField] private Vector2 respawnOffset = Vector2.zero;

    [Header("Visuals (optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color activeColor   = new Color(1f, 0.9f, 0.3f, 1f);

    [Tooltip("Аниматор-триггер при активации (опционально)")]
    [SerializeField] private string activationTrigger = "Activate";

    [Header("Effects")]
    [SerializeField] private GameObject activationEffectPrefab;
    [SerializeField] private AudioClip  activationSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Events")]
    public UnityEvent OnActivated;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private bool     _activated;
    private Animator _anim;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        _anim = GetComponentInChildren<Animator>();

        // Внешний вид "не активен"
        if (spriteRenderer != null)
            spriteRenderer.color = inactiveColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_activated && singleUse) return;

        // Слой-фильтр
        if (playerLayer.value != 0 && (playerLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        var stats = other.GetComponent<PlayerStats>() ?? other.GetComponentInParent<PlayerStats>();
        if (stats == null || stats.IsDead) return;

        Activate(stats);
    }

    // ──────────────────────────────────────────
    // Активация
    // ──────────────────────────────────────────

    private void Activate(PlayerStats stats)
    {
        _activated = true;

        // 1. Лечение
        if (healToFull && stats != null)
            stats.HealToFull();

        // 2. Обновить респавн-точку в менеджере
        if (setRespawnPoint && CheckpointManager.Instance != null)
        {
            Vector2 respawnPos = (Vector2)transform.position + respawnOffset;
            CheckpointManager.Instance.SetRespawnPoint(respawnPos);
        }

        // 3. Автосохранение
        if (autoSaveSlot >= 0)
            SaveLoadManager.Instance?.Save(autoSaveSlot);

        // 4. Визуал
        if (spriteRenderer != null)
            spriteRenderer.color = activeColor;

        if (_anim != null && !string.IsNullOrEmpty(activationTrigger))
            _anim.SetTrigger(activationTrigger);

        // 5. Эффекты
        if (activationEffectPrefab != null)
            Instantiate(activationEffectPrefab, transform.position, Quaternion.identity);

        if (activationSound != null)
            AudioSource.PlayClipAtPoint(activationSound, transform.position, soundVolume);

        OnActivated?.Invoke();
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + (Vector3)respawnOffset, 0.2f);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)respawnOffset);
    }
}
