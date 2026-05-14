using System.Collections;
using UnityEngine;

/// <summary>
/// Летающий враг: висит на hoverHeight над стартовой точкой,
/// при обнаружении игрока пикирует к нему, наносит удар и отступает обратно.
/// Состояния: Hover → Dive → Retreat → (Staggered) → Hover.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class FlyingEnemyAI : MonoBehaviour
{
    public enum FlyState { Hover, Dive, Retreat, Staggered, Dead }

    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Detection")]
    [SerializeField] private float     detectionRadius = 7f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayer;       // стены/пол для line-of-sight
    [SerializeField] private bool      requireLOS = true;

    [Header("Flight")]
    [Tooltip("Высота парения над стартовой позицией")]
    [SerializeField] private float hoverHeight    = 3f;
    [Tooltip("Амплитуда синусоидального покачивания")]
    [SerializeField] private float hoverAmplitude = 0.3f;
    [Tooltip("Частота покачивания")]
    [SerializeField] private float hoverFrequency = 1.2f;
    [SerializeField] private float diveSpeed      = 6f;
    [SerializeField] private float retreatSpeed   = 4f;

    [Header("Attack")]
    [SerializeField] private float     attackRange       = 0.8f;
    [SerializeField] private float     attackCooldown    = 1.5f;
    [SerializeField] private LayerMask attackTargetLayers;
    [SerializeField] private float     attackHitDelay    = 0.2f;

    [Header("Stagger")]
    [SerializeField] private float staggerDuration = 0.3f;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private FlyState                _state = FlyState.Hover;
    private EnemyStats              _stats;
    private Rigidbody2D             _rb;
    private Animator                _anim;
    private StatusEffectController  _status;

    private Vector2   _homePosition;    // стартовая позиция + hoverHeight
    private Transform _target;
    private float     _attackTimer;
    private float     _staggerTimer;
    private float     _hoverTimer;
    private bool      _facingRight = true;

    // Переиспользуемый буфер (без аллокаций на атаку)
    private static readonly Collider2D[]  s_hitBuffer = new Collider2D[8];
    private static          ContactFilter2D s_hitFilter = new ContactFilter2D { useTriggers = true };

    // ──────────────────────────────────────────
    // Animator — кэшированные хэши
    // ──────────────────────────────────────────

    private static readonly int HashAttack   = Animator.StringToHash("Attack");
    private static readonly int HashHit      = Animator.StringToHash("Hit");
    private static readonly int HashDie      = Animator.StringToHash("Die");
    private static readonly int HashSpeed    = Animator.StringToHash("Speed");
    private static readonly int HashIsDiving = Animator.StringToHash("IsDiving");

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _stats  = GetComponent<EnemyStats>();
        _rb     = GetComponent<Rigidbody2D>();
        _anim   = GetComponentInChildren<Animator>();
        _status = GetComponent<StatusEffectController>();

        _rb.gravityScale   = 0f;   // летит — гравитация не нужна
        _rb.freezeRotation = true;
    }

    private void Start()
    {
        // Запоминаем домашнюю позицию один раз после всех Awake
        _homePosition   = _rb.position;
        _homePosition.y += hoverHeight;
    }

    private void OnEnable()
    {
        _stats.OnDamageTaken.AddListener(OnHit);
        _stats.OnDeath.AddListener(OnDied);
    }

    private void OnDisable()
    {
        _stats.OnDamageTaken.RemoveListener(OnHit);
        _stats.OnDeath.RemoveListener(OnDied);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Статусы: Freeze/Stun — стоим на месте
        if (_status != null && _status.IsIncapacitated && _state != FlyState.Dead)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (_state)
        {
            case FlyState.Hover:     UpdateHover();    break;
            case FlyState.Dive:      UpdateDive();     break;
            case FlyState.Retreat:   UpdateRetreat();  break;
            case FlyState.Staggered: UpdateStagger();  break;
            case FlyState.Dead:      break;
        }

        _anim?.SetFloat(HashSpeed,    _rb.linearVelocity.magnitude);
        _anim?.SetBool (HashIsDiving, _state == FlyState.Dive);
    }

    // ──────────────────────────────────────────
    // States
    // ──────────────────────────────────────────

    /// <summary>Плавает на hoverHeight, покачивается, ищет игрока.</summary>
    private void UpdateHover()
    {
        _hoverTimer += Time.deltaTime;
        float   sineOffset  = Mathf.Sin(_hoverTimer * hoverFrequency) * hoverAmplitude;
        Vector2 hoverTarget = _homePosition + Vector2.up * sineOffset;

        Vector2 toHover = hoverTarget - _rb.position;
        _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, toHover * 4f, Time.deltaTime * 5f);

        if (TryDetectPlayer())
            _state = FlyState.Dive;
    }

    /// <summary>Пикирует к игроку; при достижении attackRange — атакует и уходит.</summary>
    private void UpdateDive()
    {
        if (_target == null || _stats.IsDead)
        {
            _state = FlyState.Retreat;
            return;
        }

        float slowMult = _status != null ? _status.SlowMultiplier : 1f;
        Vector2 dir    = ((Vector2)_target.position - _rb.position).normalized;
        _rb.linearVelocity = dir * (diveSpeed * slowMult);
        FaceDir(dir.x);

        _attackTimer -= Time.deltaTime;

        if (Vector2.Distance(_rb.position, _target.position) <= attackRange
            && _attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            StartCoroutine(AttackRoutine());
            _state = FlyState.Retreat;
        }
    }

    /// <summary>Возвращается на домашнюю высоту.</summary>
    private void UpdateRetreat()
    {
        Vector2 toHome = _homePosition - _rb.position;
        _rb.linearVelocity = Vector2.Lerp(
            _rb.linearVelocity,
            toHome.normalized * retreatSpeed,
            Time.deltaTime * 4f);

        if (toHome.magnitude < 0.4f)
        {
            _rb.linearVelocity = Vector2.zero;
            _state = FlyState.Hover;
        }
    }

    private void UpdateStagger()
    {
        _rb.linearVelocity = Vector2.zero;
        _staggerTimer -= Time.deltaTime;
        if (_staggerTimer <= 0f)
            _state = FlyState.Hover;
    }

    // ──────────────────────────────────────────
    // Detection + Line-of-Sight
    // ──────────────────────────────────────────

    private bool TryDetectPlayer()
    {
        if (_status != null && _status.IsBlind) return false;

        var col = Physics2D.OverlapCircle(_rb.position, detectionRadius, playerLayer);
        if (col == null) return false;
        if (requireLOS && !HasLOS(col.transform)) return false;

        _target = col.transform;
        return true;
    }

    private bool HasLOS(Transform target)
    {
        Vector2 dir  = (Vector2)target.position - _rb.position;
        float   dist = dir.magnitude;
        var     hit  = Physics2D.Raycast(_rb.position, dir.normalized, dist, obstacleLayer);
        return hit.collider == null;
    }

    // ──────────────────────────────────────────
    // Attack
    // ──────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        _anim?.SetTrigger(HashAttack);
        yield return new WaitForSeconds(attackHitDelay);

        if (_state == FlyState.Dead) yield break;

        s_hitFilter.SetLayerMask(attackTargetLayers);
        int count = Physics2D.OverlapCircle(_rb.position, attackRange, s_hitFilter, s_hitBuffer);
        var onHit = _stats.OnHitEffect;

        for (int i = 0; i < count; i++)
        {
            var col = s_hitBuffer[i];
            if (col == null) continue;

            col.GetComponent<DamageHandler>()
               ?.ApplyDamage(_stats.AttackDamage, DamageSource.Vampire);

            if (onHit != null)
                col.GetComponentInParent<StatusEffectController>()?.Apply(onHit);
        }
    }

    // ──────────────────────────────────────────
    // Callbacks
    // ──────────────────────────────────────────

    private void OnHit(int _)
    {
        if (_state == FlyState.Dead) return;

        _staggerTimer      = staggerDuration;
        _rb.linearVelocity = Vector2.zero;
        _state             = FlyState.Staggered;
        _anim?.SetTrigger(HashHit);
    }

    private void OnDied()
    {
        _state             = FlyState.Dead;
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale   = 1f;   // падает после смерти
        _anim?.SetTrigger(HashDie);
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    private void FaceDir(float x)
    {
        if ((x > 0.01f && !_facingRight) || (x < -0.01f && _facingRight))
        {
            _facingRight = !_facingRight;
            Vector3 s = transform.localScale;
            s.x *= -1f;
            transform.localScale = s;
        }
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Линия до позиции парения
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * hoverHeight);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * hoverHeight, 0.15f);
    }
}
