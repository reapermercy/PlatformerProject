using UnityEngine;

/// <summary>
/// Условие для автоматической смены стенда.
/// Тип условия + целевой стенд.
/// Создаётся через Assets → Create → Stand → Condition.
/// </summary>
[CreateAssetMenu(fileName = "NewCondition", menuName = "Stand/Condition")]
public class StandCondition : ScriptableObject
{
    public ConditionType type;

    [Header("HPBelow")]
    [Tooltip("Порог HP в процентах (0..1). Только для HPBelow.")]
    public float hpThreshold = 0.3f;

    [Header("ZoneEntered / ItemPickedUp")]
    [Tooltip("ID зоны или предмета. Только для ZoneEntered / ItemPickedUp.")]
    public string identifier;

    [Header("Target")]
    public StandData targetStand;

    /// <summary>Проверить выполнение условия</summary>
    public bool Evaluate(PlayerStats stats, string triggerId = null)
    {
        switch (type)
        {
            case ConditionType.HPBelow:
                return stats != null && stats.HPPercent <= hpThreshold;

            case ConditionType.ZoneEntered:
                return triggerId != null && triggerId == identifier;

            case ConditionType.ItemPickedUp:
                return triggerId != null && triggerId == identifier;

            default:
                return false;
        }
    }
}

public enum ConditionType
{
    HPBelow,
    ZoneEntered,
    ItemPickedUp
}
