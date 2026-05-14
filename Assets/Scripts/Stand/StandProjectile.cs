using UnityEngine;

/// <summary>
/// MonoBehaviour на prefab снаряда стенда.
///
/// Настройка prefab:
///   - SpriteRenderer (любой спрайт)
///   - CircleCollider2D (IsTrigger = true)
///   - Rigidbody2D (Body Type = Kinematic, Gravity Scale = 0)
///   - StandProjectile (этот скрипт)
///   - Layer: StandGhost (или отдельный Projectile)
///
/// Инициализируется из ProjectileAbility.Execute() через Init().
/// При попадании во врага вызывает DamageHandler.ApplyDamage().
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class StandProjectile : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Заполняется через Init()
    // ──────────────────────────────────────────

    private Vector2   _dir;
    private float     _speed;
    private int       _damage;
    private bool      _isHoming;
    private float     _homingStrength; // градусов/сек
    private LayerMask _enemyLayers;
    private bool      _hasHit;

    private Rigidbody2D _rb;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    // ──────────────────────────────────────────
    // API — вызывается сразу после Instantiate
    // ──────────────────────────────────────────

    public void Init(Vector2 direction, float speed, int damage, float lifetime,
                     bool isHoming, float homingStrength, LayerMask enemyLayers)
    {
        _dir            = direction.normalized;
        _speed          = speed;
        _damage         = damage;
        _isHoming       = isHoming;
        _homingStrength = homingStrength;
        _enemyLayers    = enemyLayers;

        _rb.gravityScale = 0f;

        Destroy(gameObject, lifetime);
    }

    // ──────────────────────────────────────────
    // Движение
    // ──────────────────────────────────────────

    private void FixedUpdate()
    {
        if (_hasHit) return;

        if (_isHoming)
            UpdateHoming();

        _rb.linearVelocity = _dir * _speed;
    }

    /// <summary>
    /// Медленно поворачивает снаряд к ближайшему врагу.
    /// Используется для Tusk Act 2 (tracking bullet).
    /// </summary>
    private void UpdateHoming()
    {
        const float searchRadius = 8f;

        Collider2D nearest = Physics2D.OverlapCircle(transform.position, searchRadius, _enemyLayers);
        if (nearest == null) return;

        Vector2 toTarget = ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized;

        float angle   = Vector2.SignedAngle(_dir, toTarget);
        float maxDeg  = _homingStrength * Time.fixedDeltaTime;
        float rotated = Mathf.MoveTowardsAngle(0f, angle, maxDeg);

        _dir = Quaternion.Euler(0f, 0f, rotated) * _dir;
    }

    // ──────────────────────────────────────────
    // Попадание
    // ──────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;

        var handler = other.GetComponent<DamageHandler>();
        if (handler == null) return;

        // Не бьём самого игрока / стенда
        if (handler.IsStand && !handler.CanBeHitByHumans) return;

        _hasHit = true;
        handler.ApplyDamage(_damage, DamageSource.Stand);
        Destroy(gameObject);
    }
}
