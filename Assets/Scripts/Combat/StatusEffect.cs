using UnityEngine;

/// <summary>
/// Данные одного статус-эффекта.
/// Сериализуем — можно задавать в инспекторе EnemyAI (onHitEffect) или в ScriptableObject способности.
/// </summary>
[System.Serializable]
public class StatusEffect
{
    [Tooltip("Тип эффекта")]
    public StatusEffectType type = StatusEffectType.Stun;

    [Tooltip("Длительность в секундах. <= 0 = бесконечно (снимается только вручную).")]
    public float duration = 2f;

    [Tooltip("Сила эффекта. Slow: множитель скорости (0.5 = половина). Burn: урон за тик. MassIncrease: множитель массы.")]
    public float magnitude = 1f;

    [Tooltip("Для Burn — интервал между тиками урона.")]
    public float tickInterval = 1f;

    /// <summary>Создаёт копию (чтобы runtime-таймер не портил инспекторный шаблон).</summary>
    public StatusEffect Clone()
    {
        return new StatusEffect
        {
            type         = type,
            duration     = duration,
            magnitude    = magnitude,
            tickInterval = tickInterval
        };
    }
}
