using UnityEngine;

/// <summary>
/// Абстрактный базовый класс способности стенда (ScriptableObject).
/// Наследники: RapidPunchAbility, TimeSlowAbility, ProjectileAbility.
/// Создаётся как ассет, ссылается из StandData.abilities.
/// </summary>
public abstract class StandAbility : ScriptableObject
{
    // ──────────────────────────────────────────
    // Данные
    // ──────────────────────────────────────────

    [Header("Ability Info")]
    public string abilityName;
    public Sprite icon;

    [Header("Cooldown")]
    public float cooldown = 3f;

    // ──────────────────────────────────────────
    // Runtime (не сериализуется)
    // ──────────────────────────────────────────

    [System.NonSerialized] private float _lastUsedTime = -999f;

    // ──────────────────────────────────────────
    // API
    // ──────────────────────────────────────────

    /// <summary>Готова ли способность (кулдаун прошёл)</summary>
    public bool IsReady => Time.time >= _lastUsedTime + cooldown;

    /// <summary>Оставшееся время кулдауна (0 если готова)</summary>
    public float CooldownRemaining => Mathf.Max(0f, (_lastUsedTime + cooldown) - Time.time);

    /// <summary>Прогресс кулдауна 0..1 (1 = готова)</summary>
    public float CooldownProgress => cooldown > 0f
        ? Mathf.Clamp01((Time.time - _lastUsedTime) / cooldown)
        : 1f;

    /// <summary>
    /// Попытка использовать способность. Проверяет кулдаун.
    /// </summary>
    public bool TryExecute(StandController stand)
    {
        if (!IsReady) return false;

        _lastUsedTime = Time.time;
        Execute(stand);
        return true;
    }

    /// <summary>Сбросить кулдаун (при смене стенда, воскрешении и т.д.)</summary>
    public void ResetCooldown()
    {
        _lastUsedTime = -999f;
    }

    /// <summary>Реализация способности — переопределяется в наследниках</summary>
    protected abstract void Execute(StandController stand);
}
