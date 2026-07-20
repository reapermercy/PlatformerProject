// ============================================================
//  TUSK / ДЖОННИ ДЖОСТАР — третий стресс-тест.
//  Новые проблемы, которых не было у Джолин:
//   1. Акты = КОЛЛЕКЦИЯ профилей, между которыми игрок переключается
//      (2/3/4 попеременно), а не линейная замена.
//   2. У каждого акта УСЛОВИЯ: открытие (части Трупа) и
//      поддержание (Акт 4 жив только верхом).
//   3. Владелец не умеет ходить: локомоция — подключаемая стратегия
//      (ползание / лошадь), а лошадь — отдельная сущность.
//   4. До Акта 4 стенд визуально летает рядом, но НЕ дерётся:
//      боевой режим — стрельба ногтями самого Джонни.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. АКТ КАК ПРОФИЛЬ: мини-definition внутри definition
// ------------------------------------------------------------

[Serializable]
public class StandActProfile
{
    public string actName;                    // "Tusk Act 1" ... "Act 4"
    public EmbodimentKind embodiment;         // Act 1–3: Integrated (стенд-визуал пассивен)
                                              // Act 4:   Follower  (гуманоид дерётся сам)
    public GameObject embodimentPrefab;       // тело для Act 4; для 1–3 — только
                                              // декоративный "спутник" (см. п.5)
    public List<StandAbilityDefinition> abilities;

    [Header("Условия")]
    public List<ActCondition> unlockConditions;   // как акт ОТКРЫВАЕТСЯ (навсегда)
    public List<ActCondition> sustainConditions;  // что нужно, чтобы акт БЫЛ АКТИВЕН
                                                  // Act 4: [IsMountedCondition]
}

[CreateAssetMenu(menuName = "JoJo/Stand Definition (Multi-Act)")]
public class MultiActStandDefinition : StandDefinition
{
    public List<StandActProfile> acts;
}

// --- Условия как ассеты-предикаты: сюжет и среда, без хардкода ---

public abstract class ActCondition : ScriptableObject
{
    public abstract bool Evaluate(StandUser user);
    public abstract string FailHint { get; }   // для UI: "Требуется верховая езда"
}

[CreateAssetMenu(menuName = "JoJo/Conditions/Has Corpse Part")]
public class HasCorpsePartCondition : ActCondition
{
    public CorpsePart part;                    // Heart, Eyes, Spine, ...
    public override bool Evaluate(StandUser u) => u.Progress.HasCorpsePart(part);
    public override string FailHint => $"Нужна часть Святого Трупа: {part}";
}

[CreateAssetMenu(menuName = "JoJo/Conditions/Is Mounted")]
public class IsMountedCondition : ActCondition
{
    public override bool Evaluate(StandUser u) => u.Locomotion.CurrentMode == LocomotionMode.Mounted;
    public override string FailHint => "Золотое Вращение требует бег коня";
}

public enum CorpsePart { Heart, Eyes, Ears, Spine, RightArm, LeftArm, Skull }

// ------------------------------------------------------------
// 2. КОНТРОЛЛЕР АКТОВ: живёт рядом со StandEntity
// ------------------------------------------------------------

public class StandActController : MonoBehaviour
{
    StandEntity _entity;
    MultiActStandDefinition _def;
    StandUser _user;

    public int CurrentActIndex { get; private set; } = 0;
    public StandActProfile CurrentAct => _def.acts[CurrentActIndex];
    public event Action<int> OnActChanged;

    public void Initialize(StandEntity entity, StandUser user)
    {
        _entity = entity;
        _user = user;
        _def = (MultiActStandDefinition)entity.Definition;
    }

    // Открыт ли акт (сюжетно, навсегда)
    public bool IsUnlocked(int i)
    {
        foreach (var c in _def.acts[i].unlockConditions)
            if (!c.Evaluate(_user)) return false;
        return true;
    }

    // Может ли акт быть активен ПРЯМО СЕЙЧАС (среда)
    public bool CanSustain(int i)
    {
        foreach (var c in _def.acts[i].sustainConditions)
            if (!c.Evaluate(_user)) return false;
        return true;
    }

    // Игрок листает акты (D-pad / колесо). Джонни жонглирует 2/3/4 —
    // это просто выбор из открытых, у которых выполняется sustain.
    public bool TrySetAct(int i)
    {
        if (i < 0 || i >= _def.acts.Count) return false;
        if (!IsUnlocked(i) || !CanSustain(i))
        {
            UIHints.Flash(FirstFailHint(i));   // "Требуется верховая езда"
            return false;
        }
        ApplyAct(i);
        return true;
    }

    void ApplyAct(int i)
    {
        CurrentActIndex = i;
        var act = _def.acts[i];
        _entity.SetEmbodiment(act.embodiment);        // Integrated ↔ Follower
        _entity.ReplaceAbilities(act.abilities);      // лоадаут акта
        OnActChanged?.Invoke(i);
    }

    // Ключ к Акту 4: каждый кадр проверяем sustain текущего акта.
    // Слез с коня в Акте 4 → мгновенный откат на лучший доступный.
    void Update()
    {
        if (CanSustain(CurrentActIndex)) return;

        for (int i = CurrentActIndex - 1; i >= 0; i--)
        {
            if (IsUnlocked(i) && CanSustain(i))
            {
                UIHints.Flash($"{CurrentAct.actName} потерян — откат на {_def.acts[i].actName}");
                ApplyAct(i);
                return;
            }
        }
    }

    string FirstFailHint(int i)
    {
        foreach (var c in _def.acts[i].unlockConditions) if (!c.Evaluate(_user)) return c.FailHint;
        foreach (var c in _def.acts[i].sustainConditions) if (!c.Evaluate(_user)) return c.FailHint;
        return "";
    }
}

// В StandEntity добавляется один метод:
//   public void ReplaceAbilities(List<StandAbilityDefinition> defs)
//   { _abilities.Clear(); foreach (var d in defs) _abilities.Add(d.CreateRuntime(this)); }

// ------------------------------------------------------------
// 3. ЛОКОМОЦИЯ: владелец, который не ходит
//    PlayerMovement превращается в хаб стратегий передвижения
// ------------------------------------------------------------

public enum LocomotionMode { Walk, Crawl, Mounted, StringForm /* Джолин */ }

public interface ILocomotionStrategy
{
    LocomotionMode Mode { get; }
    void Enter(PlayerMovement host);
    void Exit(PlayerMovement host);
    void Tick(Vector2 input, float dt);
}

// --- Джонни на земле: медленно, низкий профиль, руки заняты ---
public class CrawlLocomotion : ILocomotionStrategy
{
    PlayerMovement _host;
    public LocomotionMode Mode => LocomotionMode.Crawl;
    public float speed = 2.2f;

    public void Enter(PlayerMovement host)
    {
        _host = host;
        _host.SetColliderProfile(ColliderProfile.Prone);  // низкий — пролезает там,
                                                          // где всадник не пройдёт!
        _host.View.SetAnimatorLayer("Crawl");
    }
    public void Exit(PlayerMovement host) { }

    public void Tick(Vector2 input, float dt)
    {
        // Ползание: рывковое, стрелять на ходу нельзя (руки — это ноги)
        _host.MoveHorizontal(input.x * speed);
        _host.Combat.SetCanAim(Mathf.Abs(input.x) < 0.1f); // стреляет только замерев
    }
}

// --- Верхом: лошадь — ОТДЕЛЬНАЯ сущность со своим состоянием ---
public class MountedLocomotion : ILocomotionStrategy
{
    PlayerMovement _host;
    public HorseEntity Horse { get; private set; }
    public LocomotionMode Mode => LocomotionMode.Mounted;

    public MountedLocomotion(HorseEntity horse) { Horse = horse; }

    public void Enter(PlayerMovement host)
    {
        _host = host;
        _host.transform.SetParent(Horse.SaddleAnchor);
        _host.transform.localPosition = Vector3.zero;
        _host.SetColliderProfile(ColliderProfile.Mounted);
        _host.Combat.SetCanAim(true);                     // верхом руки свободны
        Horse.SetRider(_host);
    }

    public void Exit(PlayerMovement host)
    {
        host.transform.SetParent(null);
        Horse.SetRider(null);
    }

    public void Tick(Vector2 input, float dt)
    {
        // Инпут идёт ЛОШАДИ: у неё свой разгон, инерция, прыжок, выносливость
        Horse.Drive(input, dt);
    }
}

// Лошадь (Slow Dancer): самостоятельный агент, а не "скин скорости".
// Её галоп — это ещё и ресурс: Golden Rotation Акта 4 требует
// момента вращения от бега (см. условие в способности ниже).
public class HorseEntity : MonoBehaviour
{
    public Transform SaddleAnchor;
    public float GallopSpeed01 { get; private set; }  // 0..1 — нормированный галоп
    PlayerMovement _rider;

    public void SetRider(PlayerMovement r) => _rider = r;

    public void Drive(Vector2 input, float dt)
    {
        // разгон/инерция; прыжки через пропасти — гейтинг мира верхом
        GallopSpeed01 = Mathf.MoveTowards(GallopSpeed01, Mathf.Abs(input.x), dt * 0.6f);
        // ... физика движения
    }
}

// ------------------------------------------------------------
// 4. РЕСУРС: ногти (аналог UnravelPool Джолин)
// ------------------------------------------------------------

public class NailPool
{
    public int Max = 10;                 // 10 ногтей — рук и ног
    public int Current { get; private set; }
    float _regrow;                        // отрастают со временем/от вращения

    public bool TrySpend(int n)
    {
        if (Current < n) return false;
        Current -= n;
        return true;
    }

    public void Tick(float dt, float gallop01)
    {
        // Лорная деталь: вращение ускоряет отрастание
        _regrow += dt * (0.2f + gallop01 * 0.6f);
        if (_regrow >= 1f && Current < Max) { Current++; _regrow = 0; }
    }
}

// ------------------------------------------------------------
// 5. СПОСОБНОСТИ ПО АКТАМ
// ------------------------------------------------------------

// --- Акт 1–3: стреляет САМ ДЖОННИ. Один класс, разные данные ---
[CreateAssetMenu(menuName = "JoJo/Abilities/Nail Shot")]
public class NailShotDefinition : StandAbilityDefinition
{
    public int nailCost = 1;
    public float damage = 10f;
    public NailShotBehavior behavior;     // чем отличаются акты:
    // Act1: Straight        — прямой выстрел
    // Act2: LeaveWound      — оставляет "дыру"-ловушку на поверхности/враге
    // Act3: WoundTeleport   — Джонни может втянуться в свою дыру (гейтинг!)
    public override IStandAbility CreateRuntime(StandEntity o) => new NailShotAbility(this, o);
}

public enum NailShotBehavior { Straight, LeaveWound, WoundTeleport }

public class NailShotAbility : IStandAbility
{
    readonly NailShotDefinition _def;
    readonly StandEntity _e;

    public NailShotAbility(NailShotDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => _def.behavior == NailShotBehavior.WoundTeleport
        ? AbilityTag.Phase        // Акт 3 открывает "пролезть" — через дыры!
        : AbilityTag.None;

    public bool CanActivate =>
        _e.Owner.Combat.CanAim                       // ползёт — стрелять нельзя
        && _e.Owner.Nails.TrySpendCheck(_def.nailCost);

    public void Activate(in StandCommand cmd)
    {
        if (!_e.Owner.Nails.TrySpend(_def.nailCost)) return;
        var nail = ProjectilePool.Spawn("Nail", _e.Owner.transform.position, cmd.direction);
        nail.damage = _def.damage;
        nail.behavior = _def.behavior;   // Straight / LeaveWound / WoundTeleport
        // Стенд в актах 1–3 НЕ участвует в атаке: его "спутник"-визуал
        // (маленький Tusk рядом) — чистая декорация IntegratedEmbodiment,
        // companionVisualPrefab без хитбоксов. Летает — но не дерётся.
    }

    public void Tick(float dt) { }
}

// --- Акт 4: двойной боевой режим. Гуманоид молотит, Джонни стреляет ---
[CreateAssetMenu(menuName = "JoJo/Abilities/Golden Rotation Barrage")]
public class GoldenBarrageDefinition : StandAbilityDefinition
{
    public float minGallop = 0.5f;        // барраж требует НАБРАННОГО галопа
    public override IStandAbility CreateRuntime(StandEntity o) => new GoldenBarrageAbility(this, o);
}

public class GoldenBarrageAbility : IStandAbility
{
    readonly GoldenBarrageDefinition _def;
    readonly StandEntity _e;

    public GoldenBarrageAbility(GoldenBarrageDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.HeavyImpact;   // Акт 4 ломает стены

    public bool CanActivate =>
        _e.Owner.Locomotion.CurrentStrategy is MountedLocomotion m
        && m.Horse.GallopSpeed01 >= _def.minGallop;
    // Само существование Акта 4 гарантирует Mounted (sustain-условие),
    // но БАРРАЖ требует ещё и скорости: стоя на месте Акт 4 есть,
    // а бесконечное вращение — нет. Точно по канону.

    public void Activate(in StandCommand cmd)
    {
        // Гуманоидный Tusk (FollowerEmbodiment Акта 4) выполняет барраж...
        _e.IssueCommand(new StandCommand(StandCommandType.Barrage, cmd.direction));
        // ...а Джонни МОЖЕТ параллельно стрелять — DualControlBridge
        // в Акте 4 не блокирует его атаку во время барража стенда.
        // Вот где двойное управление раскрывается полностью.
    }

    public void Tick(float dt) { }
}

// ------------------------------------------------------------
// 6. СБОРКА В РЕДАКТОРЕ
// ------------------------------------------------------------
//
//  Tusk.asset (MultiActStandDefinition)
//   acts:
//    [0] "Act 1"  Integrated  abilities: NailShot(Straight)
//        unlock: —            sustain: —
//    [1] "Act 2"  Integrated  abilities: NailShot(LeaveWound)
//        unlock: HasCorpsePart(Eyes)      sustain: —
//    [2] "Act 3"  Integrated  abilities: NailShot(WoundTeleport)  ← Phase-гейтинг
//        unlock: HasCorpsePart(Spine)     sustain: —
//    [3] "Act 4"  Follower    abilities: GoldenBarrage, NailShot(WoundTeleport)
//        unlock: HasCorpsePart(RightArm)  sustain: IsMounted      ← ключ
//
//  Johnny.prefab
//   ├─ StandUser (standDefinition = Tusk.asset)
//   ├─ StandActController
//   └─ PlayerMovement.locomotion: Crawl (default) / Mounted(SlowDancer)
//
//  Что чувствует игрок:
//   • пеший Джонни медленный и уязвимый, но низкий — свои лазы в мире
//   • верхом быстрый, стреляет на скаку, конь прыгает пропасти
//   • открыл Акт 4 → соблазн всегда быть в нём, но он живёт только
//     в седле: спешился (или сбили с коня!) — авто-откат на Акт 3.
//     Сбивание с лошади становится осмысленной атакой боссов.
//   • жонглирование 2/3/4 — на D-pad, как листание, с подсказками
//     почему акт недоступен.
//
// ------------------------------------------------------------
// Заглушки
// ------------------------------------------------------------
public static class UIHints { public static void Flash(string s) { } }
public static class ProjectilePool
{
    public static NailProjectile Spawn(string id, Vector2 pos, Vector2 dir) => new();
}
public class NailProjectile { public float damage; public NailShotBehavior behavior; }
public enum ColliderProfile { Standing, Prone, Mounted }

// Дополнения к контрактам базовой системы:
//   StandUser:      Progress (сюжетные флаги), Nails (NailPool), Locomotion
//   PlayerMovement: SetColliderProfile(), MoveHorizontal(), View, Combat,
//                   CurrentStrategy, CurrentMode, SetStrategy(ILocomotionStrategy)
//   PlayerCombat:   SetCanAim(bool), CanAim
//   NailPool:       TrySpendCheck(int) — проверка без списания
