using System.Collections.Generic;
using UnityEngine;
// EnergyRechargeCondition определён в EnergySystem.cs

/// <summary>
/// ScriptableObject — описывает игрового персонажа, его характеристики и стенд(ы).
///
/// Примеры:
///   Jotaro  → stands: [StarPlatinum],   canManuallySwitch: false, initialUnlocked: 1
///   Koichi  → stands: [Act1,Act2,Act3], canManuallySwitch: true,  initialUnlocked: 3
///   Pucci   → stands: [Whitesnake,C-Moon,MiH], canManuallySwitch: false, initialUnlocked: 1
///             (C-Moon и MiH разблокируются через StandSwitchManager.UnlockNextStand())
///
/// Характеристики (Stats) пока задаются здесь, но применяются вручную через
/// PlayerStats.ApplyCharacterStats() — когда придёт время.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "JoJo/Character Data")]
public class CharacterData : ScriptableObject
{
    // ──────────────────────────────────────────
    // Персонаж
    // ──────────────────────────────────────────

    [Header("Identity")]
    public string characterName;
    public Sprite characterPortrait;

    // ──────────────────────────────────────────
    // Характеристики (будут применяться к PlayerStats / PlayerController)
    // ──────────────────────────────────────────

    [Header("Stats")]
    [Tooltip("Максимальное здоровье")]
    public int   maxHP         = 100;

    [Tooltip("Скорость передвижения")]
    public float moveSpeed     = 7f;

    [Tooltip("Сила прыжка")]
    public float jumpForce     = 14f;

    [Tooltip("Длина комбо-цепочки (1-3+)")]
    [Min(1)]
    public int   comboLength   = 3;

    // ──────────────────────────────────────────
    // Энергия (Хамон / Спин)
    // ──────────────────────────────────────────

    [Header("Energy (Hamon / Spin)")]
    [Tooltip("Хамон — заряжается при блоке. Задай enabled = true для персонажей с Хамоном.")]
    public EnergyConfig hamonConfig = new();
    [Tooltip("Спин — заряжается пассивно или при движении. Для персонажей с Spin.")]
    public EnergyConfig spinConfig  = new();

    [System.Serializable]
    public class EnergyConfig
    {
        [Tooltip("Активировать энергосистему для этого персонажа")]
        public bool                    enabled          = false;
        public float                   maxEnergy        = 100f;
        [Tooltip("Единиц энергии в секунду при выполнении условия зарядки")]
        public float                   rechargeRate     = 10f;
        public EnergyRechargeCondition rechargeCondition = EnergyRechargeCondition.Passive;
    }

    // ──────────────────────────────────────────
    // Стенды
    // ──────────────────────────────────────────

    [Header("Stands")]
    [Tooltip("Все стенды персонажа. Первый — активный по умолчанию.")]
    public List<StandData> stands = new();

    [Tooltip(
        "Сколько стендов разблокировано с самого начала.\n" +
        "Koichi = 3 (все акты сразу).\n" +
        "Pucci  = 1 (остальные открываются через UnlockNextStand).")]
    [Min(1)]
    public int initialUnlockedCount = 1;

    // ──────────────────────────────────────────
    // Переключение стендов
    // ──────────────────────────────────────────

    [Header("Stand Switching")]
    [Tooltip(
        "true  → игрок переключает стенды вручную (Коичи: Echoes Act 1/2/3).\n" +
        "false → переключение только через события (Pucci) или недоступно (Jotaro).")]
    public bool canManuallySwitch;

    // ──────────────────────────────────────────
    // Хелперы
    // ──────────────────────────────────────────

    /// <summary>Первый стенд персонажа.</summary>
    public StandData DefaultStand => stands != null && stands.Count > 0 ? stands[0] : null;
}
