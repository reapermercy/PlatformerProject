using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject с данными конкретного стенда.
/// Создаётся через Assets → Create → Stand → StandData.
/// </summary>
[CreateAssetMenu(fileName = "NewStand", menuName = "Stand/StandData")]
public class StandData : ScriptableObject
{
    // ──────────────────────────────────────────
    // Основные параметры
    // ──────────────────────────────────────────

    [Header("Identity")]
    public string standName;
    public Sprite standSprite;

    [Header("Behavior")]
    [Tooltip("Максимальный радиус удаления от персонажа")]
    public float maxRadius = 5f;

    [Tooltip("Интегрированный стенд (броня, крылья) или внешний")]
    public bool isPartOfUser;

    public StandMode defaultMode = StandMode.Attached;

    // ──────────────────────────────────────────
    // Способности и боевка
    // ──────────────────────────────────────────

    [Header("Abilities")]
    public List<StandAbility> abilities = new List<StandAbility>();

    [Header("Combat")]
    public StandCombatConfig combatConfig;

    [Header("Stand Interactions")]
    [Tooltip("Стенд может атаковать врагов")]
    public bool canAttack = true;
    [Tooltip("С какими объектами стенд может взаимодействовать")]
    public StandInteractionMask allowedInteractions = StandInteractionMask.All;
}

// ──────────────────────────────────────────
// Режимы стенда
// ──────────────────────────────────────────

public enum StandMode
{
    Attached,    // скрыт / слит с персонажем
    Free,        // призван, управляется мышью
    Automatic,   // AI логика, атакует сам
    SemiAuto     // игрок задаёт цель, стенд атакует
}

// ──────────────────────────────────────────
// Маска взаимодействий стенда
// ──────────────────────────────────────────

[System.Flags]
public enum StandInteractionMask
{
    None    = 0,
    Lever   = 1 << 0,   // рычаги
    Door    = 1 << 1,   // двери
    Sign    = 1 << 2,   // таблички
    NPC     = 1 << 3,   // диалог с NPC
    Note    = 1 << 4,   // записки
    All     = ~0        // всё
}

// ──────────────────────────────────────────
// Боевая конфигурация стенда
// ──────────────────────────────────────────

[System.Serializable]
public class StandCombatConfig
{
    [Tooltip("Базовый урон удара стенда")]
    public int baseDamage = 12;

    [Tooltip("Скорость передвижения стенда")]
    public float moveSpeed = 10f;

    [Tooltip("Радиус атаки стенда")]
    public float attackRadius = 0.8f;

    [Tooltip("Задержка между ударами")]
    public float attackCooldown = 0.3f;

    [Tooltip("Сила нокбэка")]
    public float knockbackForce = 3f;
}
