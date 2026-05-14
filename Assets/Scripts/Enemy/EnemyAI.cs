using System.Collections;
using UnityEngine;

/// <summary>
/// Конечный автомат AI врага.
/// Состояния: Idle, Patrol, Chase, Attack, Staggered, Dead.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class EnemyAI : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 6f;
    [SerializeField] private float loseRadius      = 9f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitTime = 1.5f;

    [Header("Stagger")]
    [SerializeField] private float staggerDuration = 0.4f;

    [Header("Line of Sight")]
    [Tooltip("true = враг не видит игрока сквозь стены/пол")]
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Слои стен/пола которые блокируют зрение (Ground, Walls, Door...)")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask attackTargetLayers;
    [Tooltip("Задержка между началом анимации атаки и нанесением урона")]
    [SerializeField] private float attackHitDelay = 0.3f;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private EnemyState _state = EnemyState.Idle;
    private EnemyStats _stats;
    private Rigidbody2D _rb;
    private Animator    _anim;
    private StatusEffectController _status;

    // Patrol
    private int   _patrolIndex;
    private float _waitTimer;

    // Chase
    private Transform _target;

    // Attack
    private float _attackTimer;

    // Stagger
    private float _staggerTimer;

    // Facing
    private bool _facingRight = true;

    // Переиспользуемый буфер + фильтр (Unity 6 — OverlapCircle с ContactFilter2D)
    private static readonly Collider2D[] s_overlapBuffer = new Collider2D[16];
    private static ContactFilter2D s_overlapFilter = new ContactFilter2D { useTriggers = true };

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _stats  = GetComponent<EnemyStats>();
        _rb     = GetComponent<Rigidbody2D>();
        _anim   = GetComponentInChildren<Animator>();
        _status = GetComponent<StatusEffectController>();
    }

    private void OnEnable()
    {
        _stats.OnDamageTaken.AddListener(OnHit);
        _stats.OnDeath.AddListener(OnDied);
        _stats.OnRevive.AddListener(OnRevived);
    }

    private void OnDisable()
    {
        _stats.OnDamageTaken.RemoveListener(OnHit);
        _stats.OnDeath.RemoveListener(OnDied);
        _stats.OnRevive.RemoveListener(OnRevived);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Статусы: Freeze / Stun полностью обрывают AI-тик
        if (_status != null && _status.IsIncapacitated && _state != EnemyState.Dead)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            UpdateAnimator();
            return;
        }

        switch (_state)
        {
            case EnemyState.Idle:    UpdateIdle();    break;
            case EnemyState.Patrol:  UpdatePatrol();  break;
            case EnemyState.Chase:   UpdateChase();   break;
            case EnemyState.Attack:  UpdateAttack();  break;
            case EnemyState.Staggered: UpdateStagger(); break;
            case EnemyState.Dead: break;
        }

        UpdateAnimator();
    }

    // ──────────────────────────────────────────
    // States
    // ──────────────────────────────────────────

    private void SetState(EnemyState newState)
    {
        _state = newState;
    }

    // ── Idle ──

    private void UpdateIdle()
    {
        if (TryDetectPlayer()) return;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
                SetState(EnemyState.Patrol);
        }
    }

    // ── Patrol ──

    private void UpdatePatrol()
    {
        if (TryDetectPlayer()) return;
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            SetState(EnemyState.Idle);
            return;
        }

        Transform point = patrolPoints[_patrolIndex];
        Vector2 dir = ((Vector2)point.position - _rb.position).normalized;
        _rb.linearVelocity = new Vector2(dir.x * _stats.MoveSpeed, _rb.linearVelocity.y);
        FaceDirection(dir.x);

        if (Vector2.Distance(_rb.position, point.position) < 0.3f)
        {
            _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
            _waitTimer   = patrolWaitTime;
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            SetState(EnemyState.Idle);
        }
    }

    // ── Chase ──

    private void UpdateChase()
    {
        // Ослеп или потеряли Line-of-Sight — теряем цель
        bool blind   = _status != null && _status.IsBlind;
        bool lostLOS = requireLineOfSight && !HasLineOfSight(_target);

        if (blind || lostLOS || _target == null || !IsPlayerInRange(loseRadius))
        {
            _target = null;
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            SetState(EnemyState.Idle);
            return;
        }

        float dist = Vector2.Distance(_rb.position, _target.position);

        if (dist <= _stats.AttackRange)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            SetState(EnemyState.Attack);
            return;
        }

        Vector2 dir = ((Vector2)_target.position - _rb.position).normalized;
        _rb.linearVelocity = new Vector2(dir.x * _stats.MoveSpeed, _rb.linearVelocity.y);
        FaceDirection(dir.x);
    }

    // ── Attack ──

    private void UpdateAttack()
    {
        if (_target == null)
        {
            SetState(EnemyState.Idle);
            return;
        }

        float dist = Vector2.Distance(_rb.position, _target.position);
        if (dist > _stats.AttackRange * 1.2f)
        {
            SetState(EnemyState.Chase);
            return;
        }

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            StartCoroutine(AttackRoutine());
            _attackTimer = _stats.AttackCooldown;
        }
    }

    // ── Stagger ──

    private void UpdateStagger()
    {
        _staggerTimer -= Time.deltaTime;
        if (_staggerTimer <= 0f)
        {
            SetState(_target != null ? EnemyState.Chase : EnemyState.Idle);
        }
    }

    // ──────────────────────────────────────────
    // Detection
    // ──────────────────────────────────────────

    private bool TryDetectPlayer()
    {
        // Blind: враг не видит игрока и остаётся в Idle/Patrol
        if (_status != null && _status.IsBlind) return false;

        if (!IsPlayerInRange(detectionRadius)) return false;
        if (!HasLineOfSight(_target)) return false;   // ← LOS проверка

        SetState(EnemyState.Chase);
        return true;
    }

    private bool HasLineOfSight(Transform target)
    {
        if (!requireLineOfSight || target == null) return true;
        Vector2 dir = (Vector2)target.position - _rb.position;
        var     hit = Physics2D.Raycast(_rb.position, dir.normalized, dir.magnitude, obstacleLayer);
        return hit.collider == null;
    }

    private bool IsPlayerInRange(float radius)
    {
        Collider2D hit = Physics2D.OverlapCircle(_rb.position, radius, playerLayer);
        if (hit != null)
        {
            _target = hit.transform;
            return true;
        }
        return false;
    }

    // ──────────────────────────────────────────
    // Attack execution
    // ──────────────────────────────────────────

    /// <summary>
    /// Запускает анимацию атаки, затем через attackHitDelay наносит урон.
    /// </summary>
    private IEnumerator AttackRoutine()
    {
        // Сначала анимация
        if (_anim != null) _anim.SetTrigger(HashAttack);

        // Ждём момента удара в анимации
        yield return new WaitForSeconds(attackHitDelay);

        // Только потом урон
        if (_state == EnemyState.Dead) yield break;
        PerformAttack();
    }

    /// <summary>Применить урон в текущей позиции. Можно вызвать и через Animation Event.</summary>
    public void PerformAttack()
    {
        Vector2 origin = attackPoint != null ? (Vector2)attackPoint.position : _rb.position;

        s_overlapFilter.SetLayerMask(attackTargetLayers);
        int count  = Physics2D.OverlapCircle(origin, _stats.AttackRange, s_overlapFilter, s_overlapBuffer);
        var onHit  = _stats.OnHitEffect;

        for (int i = 0; i < count; i++)
        {
            var hit = s_overlapBuffer[i];
            if (hit == null) continue;

            var handler = hit.GetComponent<DamageHandler>();
            if (handler != null)
                handler.ApplyDamage(_stats.AttackDamage, GetDamageSource());

            // Накладываем статус-эффект (если настроен)
            if (onHit != null)
            {
                var victimStatus = hit.GetComponentInParent<StatusEffectController>();
                victimStatus?.Apply(onHit);
            }
        }
    }

    /// <summary>Источник урона определяется по типу врага в EnemyStats.</summary>
    private DamageSource GetDamageSource() => _stats.Type switch
    {
        EnemyType.Vampire => DamageSource.Vampire,
        EnemyType.Stand   => DamageSource.Stand,   // вражеский стенд бьёт как стенд
        _                 => DamageSource.Human     // Human и StandUser — как человек
    };

    // ──────────────────────────────────────────
    // Callbacks
    // ──────────────────────────────────────────

    private void OnHit(int damage)
    {
        if (_state == EnemyState.Dead) return;

        _staggerTimer = staggerDuration;
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        SetState(EnemyState.Staggered);

        if (_anim != null) _anim.SetTrigger(HashHit);
    }

    private void OnDied()
    {
        SetState(EnemyState.Dead);
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        if (_anim != null)
            _anim.SetTrigger(HashDie);
    }

    /// <summary>Вызывается EnemyStats.Revive (зомби, сюжетное воскрешение).</summary>
    private void OnRevived()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _target      = null;
        _attackTimer = 0f;
        SetState(EnemyState.Idle);
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    private void FaceDirection(float dirX)
    {
        if (dirX > 0.01f && !_facingRight)
        {
            Flip();
        }
        else if (dirX < -0.01f && _facingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        _facingRight = !_facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }

    private void UpdateAnimator()
    {
        if (_anim == null) return;
        _anim.SetFloat(HashSpeed, Mathf.Abs(_rb.linearVelocity.x));
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, loseRadius);

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            float r = _stats != null ? _stats.AttackRange : 1f;
            Gizmos.DrawWireSphere(attackPoint.position, r);
        }
    }

    // ──────────────────────────────────────────
    // Свойства
    // ──────────────────────────────────────────

    public EnemyState CurrentState => _state;

    // ──────────────────────────────────────────
    // Animator параметры (хэши вместо строк)
    // ──────────────────────────────────────────

    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashHit    = Animator.StringToHash("Hit");
    private static readonly int HashDie    = Animator.StringToHash("Die");
    private static readonly int HashSpeed  = Animator.StringToHash("Speed");
}

public enum EnemyState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Staggered,
    Dead
}
