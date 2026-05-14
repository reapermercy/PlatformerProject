using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Единый компонент врага: HP, параметры боя, смерть, тип.
/// Тип выбирается в инспекторе — специфичная логика включается автоматически.
///
/// Human     — обычный враг. Получает урон от всего.
/// Vampire   — регенерирует HP; ×hamonMultiplier урона от Hamon.
/// StandUser — человек со стендом. По правилам урона = Human (не бьёт чужой стенд).
/// Stand     — вражеский стенд. Привязан к владельцу (ownerStats); умирает вместе с ним.
///             Атаки бьют и игрока, и его стенд (DamageSource.Stand).
///             Иммунен к атакам игрока — только стенд игрока наносит ему урон.
///             DamageHandler на этом объекте должен иметь isStand = true.
///
/// EnemyAI подписывается на OnDamageTaken / OnDeath.
/// DamageHandler вызывает TakeDamageFromSource.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(DamageHandler))]
public class EnemyStats : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Identity")]
    [SerializeField] private EnemyType enemyType  = EnemyType.Human;
    [SerializeField] private int       pointValue = 100;

    [Header("Health")]
    [SerializeField] private int   maxHP        = 50;
    [Tooltip("Задержка перед Destroy после смерти (время на анимацию)")]
    [SerializeField] private float destroyDelay = 2f;

    [Header("Combat")]
    [SerializeField] private float moveSpeed      = 3f;
    [SerializeField] private int   attackDamage   = 10;
    [SerializeField] private float attackRange    = 1f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Vampire (только при EnemyType.Vampire)")]
    [Tooltip("Множитель урона от DamageSource.Hamon")]
    [SerializeField] private float hamonMultiplier = 3f;
    [Tooltip("HP в секунду при регенерации")]
    [SerializeField] private float regenPerSecond  = 2f;

    [Header("Stand (только при EnemyType.Stand)")]
    [Tooltip("EnemyStats владельца-человека. Стенд умирает вместе с ним.")]
    [SerializeField] private EnemyStats ownerStats;

    [Header("Status Effects on Hit")]
    [Tooltip("Эффект, накладываемый на цель при успешной атаке (например, зомби → Slow / Burn).")]
    [SerializeField] private StatusEffect onHitEffect;
    [Tooltip("true = накладывать onHitEffect на врагов.")]
    [SerializeField] private bool hasOnHitEffect = false;

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent<int> OnDamageTaken;  // EnemyAI подписывается для stagger
    public UnityEvent      OnDeath;        // EnemyAI подписывается для перехода в Dead
    public UnityEvent      OnRevive;       // EnemyAI подписывается для возврата в Idle

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private int  _currentHP;
    private bool _isDead;

    // Аккумулятор дробной регенерации (чтобы regenPerSecond < 60 работал)
    private float _regenAccumulator;

    // Кеш компонента — смена скорости / стан
    private StatusEffectController _status;

    // Coroutine смерти (отложенный Destroy) — можно прервать для Revive
    private Coroutine _destroyCoroutine;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _currentHP = maxHP;
        _status    = GetComponent<StatusEffectController>();

        // Стенд: подписываемся на смерть владельца
        if (enemyType == EnemyType.Stand && ownerStats != null)
            ownerStats.OnDeath.AddListener(OnOwnerDied);
    }

    private void OnDestroy()
    {
        if (enemyType == EnemyType.Stand && ownerStats != null)
            ownerStats.OnDeath.RemoveListener(OnOwnerDied);
    }

    private void Update()
    {
        // Регенерация вампира (аккумулятор чтобы дробные значения не терялись)
        if (enemyType == EnemyType.Vampire && !_isDead && regenPerSecond > 0f)
        {
            _regenAccumulator += regenPerSecond * Time.deltaTime;
            int heal = Mathf.FloorToInt(_regenAccumulator);
            if (heal > 0)
            {
                Heal(heal);
                _regenAccumulator -= heal;
            }
        }
    }

    // ──────────────────────────────────────────
    // Урон
    // ──────────────────────────────────────────

    /// <summary>Применить урон напрямую (источник неизвестен).</summary>
    public void TakeDamage(int amount)
    {
        if (_isDead || amount <= 0) return;

        _currentHP = Mathf.Max(0, _currentHP - amount);
        OnDamageTaken?.Invoke(amount);

        if (_currentHP <= 0)
            Die();
    }

    /// <summary>
    /// Применить урон с учётом источника.
    /// Вызывается из DamageHandler.ApplyDamage().
    /// Vampire: Hamon умножается на hamonMultiplier.
    /// </summary>
    public void TakeDamageFromSource(int amount, DamageSource source)
    {
        if (enemyType == EnemyType.Vampire && source == DamageSource.Hamon)
            amount = Mathf.RoundToInt(amount * hamonMultiplier);

        TakeDamage(amount);
    }

    // ──────────────────────────────────────────
    // Лечение
    // ──────────────────────────────────────────

    public void Heal(int amount)
    {
        if (_isDead || amount <= 0) return;
        _currentHP = Mathf.Min(maxHP, _currentHP + amount);
    }

    // ──────────────────────────────────────────
    // Смерть
    // ──────────────────────────────────────────

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Очки в ScoreManager (за стенд очки не начисляются — владелец даст свои)
        if (enemyType != EnemyType.Stand)
            ScoreManager.Instance?.AddKillPoints(enemyType, pointValue);

        // Событие — EnemyAI переходит в Dead, проигрывает анимацию
        OnDeath?.Invoke();

        // Отключаем коллайдер чтобы тело не мешало
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Отложенный Destroy через корутину (можно прервать для Revive)
        _destroyCoroutine = StartCoroutine(DestroyAfterDelay(destroyDelay));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    /// <summary>
    /// Воскресить врага с указанным HP (0..1 от maxHP).
    /// Используется для зомби — подъём из мёртвых.
    /// Отменяет запланированный Destroy и сбрасывает _isDead.
    /// </summary>
    public void Revive(float hpPercent)
    {
        if (!_isDead) return;

        // Отменяем запланированный Destroy
        if (_destroyCoroutine != null)
        {
            StopCoroutine(_destroyCoroutine);
            _destroyCoroutine = null;
        }

        _isDead           = false;
        _regenAccumulator = 0f;
        _currentHP = Mathf.Clamp(
            Mathf.RoundToInt(maxHP * Mathf.Clamp01(hpPercent)),
            1, maxHP);

        // Возвращаем коллайдер
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        // Сбрасываем все статусы — зомби встаёт "чистым"
        _status?.ClearAll();

        OnRevive?.Invoke();
    }

    /// <summary>Вызывается когда умирает владелец-человек — стенд тоже исчезает.</summary>
    private void OnOwnerDied()
    {
        // Стенд отзывается: без очков, без анимации смерти
        if (_isDead) return;
        _isDead = true;
        OnDeath?.Invoke();
        Destroy(gameObject, destroyDelay * 0.5f);
    }

    // ──────────────────────────────────────────
    // Свойства
    // ──────────────────────────────────────────

    public int       CurrentHP      => _currentHP;
    public int       MaxHP          => maxHP;
    public bool      IsDead         => _isDead;
    public float     HPPercent      => maxHP > 0 ? (float)_currentHP / maxHP : 0f;

    /// <summary>Базовая скорость (без модификаторов статусов).</summary>
    public float     BaseMoveSpeed  => moveSpeed;

    /// <summary>Фактическая скорость с учётом Slow / Freeze. EnemyAI использует её.</summary>
    public float     MoveSpeed
    {
        get
        {
            if (_status == null) return moveSpeed;
            if (_status.IsIncapacitated) return 0f;
            return moveSpeed * _status.SlowMultiplier;
        }
    }

    public int       AttackDamage   => attackDamage;
    public float     AttackRange    => attackRange;
    public float     AttackCooldown => attackCooldown;
    public EnemyType Type           => enemyType;
    public int       PointValue     => pointValue;

    /// <summary>Эффект, который надо наложить на цель при атаке. null если нет.</summary>
    public StatusEffect OnHitEffect => hasOnHitEffect ? onHitEffect : null;
}

// ──────────────────────────────────────────
// Тип врага
// ──────────────────────────────────────────

public enum EnemyType
{
    Human,
    Vampire,
    StandUser,  // человек со стендом — по правилам урона как Human
    Stand,      // вражеский стенд — только стенд игрока наносит урон
    Zombie      // зомби — медленный, при атаке накладывает Slow / Burn "инфекцию"
}
