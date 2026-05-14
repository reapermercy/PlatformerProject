using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Мгновенный хил-пикап: при касании игрока восстанавливает HP и исчезает.
/// Не идёт в инвентарь — в отличие от ItemData с ItemType.Heal.
/// Классический Zelda-Heart / Metroid-energy.
///
/// Требования:
///   — Collider2D (IsTrigger = true)
///   — на игроке тэг "Player" или слой из playerLayer
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HealthPickup : MonoBehaviour
{
    [Header("Heal")]
    [Tooltip("Сколько HP восстанавливается")]
    [SerializeField] private int healAmount = 25;

    [Tooltip("true = лечит в процентах от maxHP (healPercent), иначе — фиксированное значение")]
    [SerializeField] private bool usePercent = false;

    [Tooltip("Процент от maxHP (0..1), если usePercent = true")]
    [SerializeField, Range(0f, 1f)] private float healPercent = 0.25f;

    [Header("Filter")]
    [Tooltip("Если задан — только объекты из этих слоёв могут подбирать")]
    [SerializeField] private LayerMask playerLayer;

    [Tooltip("Игнорировать если HP уже полное")]
    [SerializeField] private bool ignoreIfFull = true;

    [Header("Score")]
    [Tooltip("Очки игроку за подбор (0 = без очков)")]
    [SerializeField] private int scoreValue = 0;

    [Header("Effects")]
    [Tooltip("Опциональный префаб частиц при подборе")]
    [SerializeField] private GameObject pickupEffectPrefab;

    [Tooltip("Опциональный звук при подборе")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("Громкость звука")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Events")]
    public UnityEvent<int> OnPickedUp;   // (amountHealed)

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        // Авто-настройка: коллайдер должен быть триггером
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // На случай если игрок заспавнился прямо на пикапе — OnTriggerEnter2D мог не сработать
        if (ignoreIfFull) return;
        TryPickup(other);
    }

    // ──────────────────────────────────────────
    // Логика
    // ──────────────────────────────────────────

    private void TryPickup(Collider2D other)
    {
        // Проверка слоя (если задан)
        if (playerLayer.value != 0 && (playerLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        var stats = other.GetComponent<PlayerStats>() ?? other.GetComponentInParent<PlayerStats>();
        if (stats == null || stats.IsDead) return;

        if (ignoreIfFull && stats.CurrentHP >= stats.MaxHP) return;

        int amount = usePercent
            ? Mathf.Max(1, Mathf.RoundToInt(stats.MaxHP * healPercent))
            : healAmount;

        stats.Heal(amount);

        // Начислить очки
        if (scoreValue > 0)
            ScoreManager.Instance?.AddKillPoints(default, scoreValue);

        // Эффекты
        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);

        OnPickedUp?.Invoke(amount);

        Destroy(gameObject);
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.4f);
        var col = GetComponent<Collider2D>();
        if (col is CircleCollider2D cc)
            Gizmos.DrawWireSphere(transform.position + (Vector3)cc.offset, cc.radius);
        else
            Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
