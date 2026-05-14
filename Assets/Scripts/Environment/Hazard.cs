using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Уроновая зона: шипы, огонь, кислота, падение в пропасть и т.д.
///
/// Режимы нанесения урона:
///   OnEnter      — однократно при входе (шипы при наступании)
///   OnStay       — непрерывно пока внутри (лава, кислота, энергополе)
///   Instakill    — моментальная смерть (пропасть)
///
/// Опционально: накладывает StatusEffect (Burn для огня, Slow для грязи).
///
/// Требования:
///   — Collider2D (IsTrigger = true)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    public enum HazardMode
    {
        OnEnter,   // однократно при входе
        OnStay,    // периодически пока внутри
        Instakill  // моментальная смерть
    }

    [Header("Damage")]
    [SerializeField] private HazardMode mode = HazardMode.OnStay;

    [Tooltip("Урон за срабатывание (для Instakill игнорируется)")]
    [SerializeField] private int damage = 10;

    [Tooltip("Интервал урона в режиме OnStay (сек)")]
    [SerializeField] private float tickInterval = 0.5f;

    [Tooltip("Источник урона (влияет на правила блокировки)")]
    [SerializeField] private DamageSource damageSource = DamageSource.Hazard;

    [Header("Filter")]
    [Tooltip("Кого может задеть (если пусто — всех с DamageHandler)")]
    [SerializeField] private LayerMask affectedLayers;

    [Tooltip("Применять урон к стендам? false = стенд проходит сквозь шипы")]
    [SerializeField] private bool affectsStand = false;

    [Header("Knockback")]
    [Tooltip("Сила отбрасывания при входе (0 = нет)")]
    [SerializeField] private float knockbackForce = 0f;

    [Tooltip("Направление отбрасывания — если задан, используется вместо радиального")]
    [SerializeField] private Vector2 knockbackDirection = Vector2.up;

    [Tooltip("true = всегда в knockbackDirection, false = от центра hazard")]
    [SerializeField] private bool useFixedKnockback = true;

    [Header("Status Effect (optional)")]
    [Tooltip("Эффект накладывается на цель при попадании")]
    [SerializeField] private StatusEffect onHitEffect;
    [SerializeField] private bool applyStatusEffect = false;

    [Header("Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioClip  hitSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Events")]
    public UnityEvent<DamageHandler> OnHit;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    // Таймеры для OnStay: для каждой DamageHandler отдельный
    private readonly Dictionary<DamageHandler, float> _tickTimers = new();

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAffected(other)) return;

        var handler = ResolveDamageHandler(other);
        if (handler == null) return;

        if (mode == HazardMode.OnEnter || mode == HazardMode.Instakill)
            ApplyHit(handler, other);

        if (mode == HazardMode.OnStay)
            _tickTimers[handler] = 0f; // удар сразу при входе
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (mode != HazardMode.OnStay) return;
        if (!IsAffected(other))       return;

        var handler = ResolveDamageHandler(other);
        if (handler == null) return;

        if (!_tickTimers.TryGetValue(handler, out float timer))
            timer = 0f;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            ApplyHit(handler, other);
            timer = tickInterval;
        }

        _tickTimers[handler] = timer;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var handler = ResolveDamageHandler(other);
        if (handler != null)
            _tickTimers.Remove(handler);
    }

    // ──────────────────────────────────────────
    // Логика
    // ──────────────────────────────────────────

    private bool IsAffected(Collider2D other)
    {
        // Слой-фильтр
        if (affectedLayers.value != 0 &&
            (affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;

        return true;
    }

    private DamageHandler ResolveDamageHandler(Collider2D other)
    {
        var handler = other.GetComponent<DamageHandler>()
                   ?? other.GetComponentInParent<DamageHandler>();
        if (handler == null) return null;

        // Не бьём стенды если явно запрещено
        if (!affectsStand && handler.IsStand)
            return null;

        return handler;
    }

    private void ApplyHit(DamageHandler handler, Collider2D other)
    {
        // Instakill — дожимаем максимальный урон для гарантированной смерти
        int amount = mode == HazardMode.Instakill ? int.MaxValue / 2 : damage;

        handler.ApplyDamage(amount, damageSource);

        // Нокбэк
        if (knockbackForce > 0f)
        {
            var rb = other.GetComponent<Rigidbody2D>() ?? other.GetComponentInParent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 dir = useFixedKnockback
                    ? knockbackDirection.normalized
                    : ((Vector2)other.transform.position - (Vector2)transform.position).normalized;

                if (dir == Vector2.zero) dir = Vector2.up;
                rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
            }
        }

        // Статус-эффект
        if (applyStatusEffect && onHitEffect != null)
        {
            var status = other.GetComponent<StatusEffectController>()
                      ?? other.GetComponentInParent<StatusEffectController>();
            status?.Apply(onHitEffect);
        }

        // Эффекты
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, other.transform.position, Quaternion.identity);

        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position, soundVolume);

        OnHit?.Invoke(handler);
    }

    // ──────────────────────────────────────────
    // Очистка
    // ──────────────────────────────────────────

    private void OnDisable()
    {
        // Сброс таймеров — при повторном включении начинаем с нуля
        _tickTimers.Clear();
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = mode == HazardMode.Instakill
            ? Color.red
            : new Color(1f, 0.4f, 0f, 0.5f); // оранжевый

        var col = GetComponent<Collider2D>();
        if (col is BoxCollider2D bc)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawWireCube(bc.offset, bc.size);
        }
        else if (col is CircleCollider2D cc)
        {
            Gizmos.DrawWireSphere(transform.position + (Vector3)cc.offset, cc.radius);
        }

        // Направление нокбэка
        if (knockbackForce > 0f && useFixedKnockback)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position,
                (Vector3)((Vector2)transform.position + knockbackDirection.normalized));
        }
    }
}
