// ============================================================
//  GOLD EXPERIENCE — v2 (правки Влада)
//
//   1. ГЛАВНОЕ ВЛЕЧЕНИЕ: созданное существо стремится ВЕРНУТЬСЯ
//      НА СВОЁ МЕСТО / К СВОЕМУ ВЛАДЕЛЬЦУ. Природные стимулы —
//      вторичны (окраска пути), дом — первичен.
//   2. ВРЕМЯ СОЗДАНИЯ растёт со сложностью существа.
//   3. GER — ультимативная воля: ВОЗВРАЩАЕТ ЭФФЕКТЫ ВСПЯТЬ,
//      атаки противника аннулируются.
//   4. Прямой удар по существу отражается в ударившего (было).
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ДОМ КАК ГЛАВНЫЙ DRIVE
// ------------------------------------------------------------

public class LivingCreationV2 : MonoBehaviour
{
    public CreatureNature nature;
    public GameObject sourceObject;
    public Transform homeOwner;      // владелец предмета (может быть ВРАГ!)
    public Vector2 homePosition;     // или место, где предмет лежал

    // Иерархия воли:
    //  1) ДОМОЙ — постоянный вектор к владельцу/месту
    //  2) природные стимулы — лишь ИСКРИВЛЯЮТ путь (обход света/шума)
    //
    // Геймплейный алмаз: преврати ПРЕДМЕТ ВРАГА — существо само
    // поползёт К НЕМУ. Кобура снайпера → змея → ползёт к снайперу
    // на крышу, показывая дорогу. Отнял у босса перчатку → бабочки
    // летят к боссу сквозь лабиринт — живой указатель на цель.
    // А вернув жизнь в момент прибытия — предмет у него в упор.
    void Update()
    {
        Vector2 to = homeOwner ? (Vector2)homeOwner.position : homePosition;
        Vector2 dir = (to - (Vector2)transform.position).normalized;
        dir += NatureDetour() * 0.4f;          // природа окрашивает маршрут
        Move(dir);
    }

    Vector2 NatureDetour() => Vector2.zero;    // стимулы из старой версии
    void Move(Vector2 d) { }

    public void TakeHit(float dmg, GameObject attacker)
        => attacker?.GetComponent<Damageable>()?.TakeReflected(dmg);
}

// ------------------------------------------------------------
// 2. ЦЕНА СЛОЖНОСТИ: время создания
// ------------------------------------------------------------

// В CreatureNature добавляется:
//   public ComplexityTier tier;
public enum ComplexityTier
{
    Swarm,      // бабочки, мухи — МГНОВЕННО (боевой рефлекс)
    Simple,     // лягушка, мышь, змея — секунды канала
    Structure,  // дерево, плющ-мост — заметный канал
    Organ       // конечности, органы — ~5 минут игрового времени:
                // лечение тяжёлых ран — это ПРИВАЛ, решение
                // «мы можем себе позволить встать здесь», а не кнопка
}

public class LifeChanneling
{
    public bool InProgress { get; private set; }

    public void Begin(ComplexityTier tier, Action onDone)
    {
        float t = tier switch
        {
            ComplexityTier.Swarm     => 0f,
            ComplexityTier.Simple    => 3f,
            ComplexityTier.Structure => 8f,
            ComplexityTier.Organ     => 300f,   // реальные цифры — ручки баланса
            _ => 1f
        };
        // Канал: Джорно уязвим, прерывание портит заготовку.
        // Иерархия боевых решений сама выстроилась:
        //  рой — рефлекс, простое — тактика, структура — подготовка,
        //  орган — стратегия. Сложность жизни = горизонт планирования.
        InProgress = t > 0;
        // ... таймер, onDone
    }
}

// ------------------------------------------------------------
// 3. GER: ВОЛЯ, ОТМЕНЯЮЩАЯ ПРИЧИНЫ (ультимативная форма)
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Requiem Nullification")]
public class RequiemDefinition : StandAbilityDefinition
{
    public bool unlocked;                    // сюжет: стрела, финал
    public float fieldDuration = 8f;
    public override IStandAbility CreateRuntime(StandEntity o) => new RequiemAbility(this, o);
}

public class RequiemAbility : IStandAbility, IDamageInterceptor
{
    readonly RequiemDefinition _def;
    readonly StandEntity _e;
    float _fieldLeft;

    public RequiemAbility(RequiemDefinition d, StandEntity e)
    {
        _def = d; _e = e;
        e.Owner.Combat.RegisterInterceptor(this);
    }

    public AbilityTag Tags => AbilityTag.None;
    public bool CanActivate => _def.unlocked;

    public void Activate(in StandCommand cmd) => _fieldLeft = _def.fieldDuration;

    // Пока поле активно: ЛЮБОЕ действие против Джорно
    // ВОЗВРАЩАЕТСЯ ВСПЯТЬ — не блок, не уклон, а отмена причины:
    public bool TryIntercept(ref DamageInfo dmg)
    {
        if (_fieldLeft <= 0) return false;

        dmg.nullified = true;
        // 1) снаряд летит ОБРАТНО по своей траектории (перемотка)
        // 2) способность врага уходит в откат, как не применённая
        // 3) урон, УЖЕ нанесённый в этом окне, откатывается через
        //    Rewindable.RestoreToRecent — фундамент Джоске работает
        //    на GER: «вернуть вспять» у нас уже был построен.
        RewindAttackCause(dmg);
        return true;
    }

    void RewindAttackCause(DamageInfo dmg) { }

    public void Tick(float dt) => _fieldLeft -= dt;

    // Дизайн-предохранитель: GER не фарм-кнопка. Поле короткое,
    // откат сюжетно-длинный (раз за босса), и оно ЗАЩИТНОЕ:
    // отменяет направленное НА Джорно, не переписывает мир.
    // «Ультимативная воля» = последняя правда в споре, не пулемёт.
}
// ============================================================
