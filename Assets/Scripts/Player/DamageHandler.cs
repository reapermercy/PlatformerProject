using UnityEngine;

/// <summary>
/// Компонент на любом объекте принимающем урон — персонаж, стенд, враг.
/// Проверяет правила (человек не бьёт стенд) и форвардит в PlayerStats или EnemyStats.
/// </summary>
public class DamageHandler : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Этот объект является стендом")]
    [SerializeField] private bool isStand = false;

    [Tooltip("Исключение: этот стенд можно бить людьми")]
    [SerializeField] private bool canBeHitByHumans = false;

    [Header("References")]
    [Tooltip("Для игрока и его стенда — один общий PlayerStats")]
    [SerializeField] private PlayerStats playerStats;

    [Tooltip("Для врагов")]
    [SerializeField] private EnemyStats enemyStats;

    // ──────────────────────────────────────────
    // Основной метод
    // ──────────────────────────────────────────

    /// <summary>
    /// Попытка нанести урон.
    /// source определяет кто бьёт — проверяем иммунитет стенда.
    /// </summary>
    public void ApplyDamage(int amount, DamageSource source)
    {
        if (amount <= 0) return;

        // Правило: человек не может бить стенд (если нет исключения)
        if (isStand && !canBeHitByHumans && source == DamageSource.Human)
            return;

        // Роутинг урона
        if (playerStats != null)
        {
            playerStats.TakeDamage(amount, source);
            return;
        }

        if (enemyStats != null)
        {
            enemyStats.TakeDamageFromSource(amount, source);
        }
    }

    /// <summary>Урон без правил иммунитета. Для DoT, яда, скриптовых событий.</summary>
    public void ApplyDamageRaw(int amount)
    {
        if (amount <= 0) return;

        playerStats?.TakeDamageRaw(amount);
        enemyStats?.TakeDamage(amount);
    }

    // ──────────────────────────────────────────
    // Свойства
    // ──────────────────────────────────────────

    public bool IsStand          => isStand;
    public bool CanBeHitByHumans => canBeHitByHumans;
}

// ──────────────────────────────────────────
// Перечисление источников урона
// ──────────────────────────────────────────

public enum DamageSource
{
    Human,      // обычный человек — не может бить стенд
    Stand,      // стенд — может бить всех
    Vampire,    // вампир — особые правила (хамон наносит больше)
    Hazard,     // зона урона, шипы, падение — не зависит от правил стендов
    Hamon,      // хамон — усиленный против вампиров
    Script      // скриптовый урон — всегда проходит
}
