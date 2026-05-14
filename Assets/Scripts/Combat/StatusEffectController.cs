using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Управляет активными статус-эффектами на объекте (игрок, враг, стенд).
/// Каждый тип эффекта — уникален: применение нового того же типа
/// перезаписывает длительность/силу (берётся максимум).
///
/// Использование:
///   var ctrl = enemy.GetComponent&lt;StatusEffectController&gt;();
///   ctrl.Apply(new StatusEffect { type = StatusEffectType.Stun, duration = 1.5f });
///
/// Другие скрипты читают состояние через свойства:
///   IsStunned, IsFrozen, IsBlind, SlowMultiplier и т.д.
/// </summary>
public class StatusEffectController : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("References (auto-find if blank)")]
    [SerializeField] private DamageHandler damageHandler;  // для Burn
    [SerializeField] private Rigidbody2D   rb;             // для MassIncrease
    [SerializeField] private Transform     rotateTarget;   // для InfiniteRotation (если null — this.transform)

    [Header("InfiniteRotation")]
    [Tooltip("Скорость вращения, градус/сек, при InfiniteRotation")]
    [SerializeField] private float rotationSpeed = 180f;

    [Header("Events")]
    public UnityEvent<StatusEffectType> OnEffectApplied;
    public UnityEvent<StatusEffectType> OnEffectExpired;

    // ──────────────────────────────────────────
    // Runtime
    // ──────────────────────────────────────────

    /// <summary>Активные эффекты: один на тип (уникальны).</summary>
    private readonly Dictionary<StatusEffectType, RuntimeEffect> _active = new();

    private float _originalMass = 1f;

    private class RuntimeEffect
    {
        public StatusEffect data;
        public float        timeRemaining;
        public float        nextTickAt;   // для Burn
    }

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (damageHandler == null) damageHandler = GetComponent<DamageHandler>();
        if (rb            == null) rb            = GetComponent<Rigidbody2D>();
        if (rotateTarget  == null) rotateTarget  = transform;

        if (rb != null) _originalMass = rb.mass;
    }

    private void Update()
    {
        if (_active.Count == 0) return;

        float dt  = Time.deltaTime;
        float now = Time.time;

        // Копируем ключи, чтобы безопасно удалять во время итерации
        var keys = new List<StatusEffectType>(_active.Keys);
        foreach (var key in keys)
        {
            if (!_active.TryGetValue(key, out var eff)) continue;

            // Burn — DoT тик
            if (eff.data.type == StatusEffectType.Burn && now >= eff.nextTickAt)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(eff.data.magnitude));
                damageHandler?.ApplyDamageRaw(dmg);
                eff.nextTickAt = now + Mathf.Max(0.05f, eff.data.tickInterval);
            }

            // InfiniteRotation — продолжается каждый кадр
            if (eff.data.type == StatusEffectType.InfiniteRotation && rotateTarget != null)
            {
                rotateTarget.Rotate(0f, 0f, rotationSpeed * dt);
            }

            // Таймер
            if (eff.data.duration > 0f)
            {
                eff.timeRemaining -= dt;
                if (eff.timeRemaining <= 0f)
                    Remove(key);
            }
        }
    }

    // ──────────────────────────────────────────
    // API
    // ──────────────────────────────────────────

    /// <summary>Применить эффект. Существующий того же типа — перезаписывается (берётся максимум по длительности и силе).</summary>
    public void Apply(StatusEffect effect)
    {
        if (effect == null) return;

        var copy = effect.Clone();

        if (_active.TryGetValue(copy.type, out var existing))
        {
            // Берём максимум — эффект не слабеет при повторном наложении
            existing.data.duration     = Mathf.Max(existing.data.duration, copy.duration);
            existing.data.magnitude    = Mathf.Max(existing.data.magnitude, copy.magnitude);
            existing.timeRemaining     = Mathf.Max(existing.timeRemaining, copy.duration);
            return;
        }

        var rt = new RuntimeEffect
        {
            data          = copy,
            timeRemaining = copy.duration,
            nextTickAt    = Time.time + Mathf.Max(0.05f, copy.tickInterval)
        };
        _active[copy.type] = rt;

        ApplyStartSideEffects(rt);
        OnEffectApplied?.Invoke(copy.type);
    }

    /// <summary>Снять эффект вручную.</summary>
    public void Remove(StatusEffectType type)
    {
        if (!_active.ContainsKey(type)) return;
        ApplyEndSideEffects(type);
        _active.Remove(type);
        OnEffectExpired?.Invoke(type);
    }

    /// <summary>Снять все эффекты.</summary>
    public void ClearAll()
    {
        var keys = new List<StatusEffectType>(_active.Keys);
        foreach (var key in keys) Remove(key);
    }

    public bool Has(StatusEffectType type) => _active.ContainsKey(type);

    // ──────────────────────────────────────────
    // Побочные эффекты при старте/окончании
    // ──────────────────────────────────────────

    private void ApplyStartSideEffects(RuntimeEffect eff)
    {
        switch (eff.data.type)
        {
            case StatusEffectType.MassIncrease:
                if (rb != null)
                    rb.mass = _originalMass * Mathf.Max(1f, eff.data.magnitude);
                break;
        }
    }

    private void ApplyEndSideEffects(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.MassIncrease:
                if (rb != null) rb.mass = _originalMass;
                break;
        }
    }

    // ──────────────────────────────────────────
    // Свойства для чтения другими системами
    // ──────────────────────────────────────────

    public bool IsStunned => _active.ContainsKey(StatusEffectType.Stun);
    public bool IsFrozen  => _active.ContainsKey(StatusEffectType.Freeze);
    public bool IsBlind   => _active.ContainsKey(StatusEffectType.Blind);
    public bool IsBurning => _active.ContainsKey(StatusEffectType.Burn);

    /// <summary>true если цель не может действовать (двигаться, атаковать).</summary>
    public bool IsIncapacitated => IsStunned || IsFrozen;

    /// <summary>Множитель скорости с учётом Slow. 1f = без изменений.</summary>
    public float SlowMultiplier
    {
        get
        {
            if (!_active.TryGetValue(StatusEffectType.Slow, out var eff)) return 1f;
            return Mathf.Clamp(eff.data.magnitude, 0.05f, 1f);
        }
    }
}
