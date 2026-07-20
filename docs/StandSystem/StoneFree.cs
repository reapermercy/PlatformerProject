// ============================================================
//  STONE FREE — стресс-тест системы.
//  Ключевая идея: "убрать стенд" ≠ выключить его.
//  Для Джолин Dismiss = уйти в режим Integrated (нити из её тела),
//  Summon = выйти в режим Follower (гуманоид в 5 м).
//  Способности живут в ОБОИХ режимах и сами спрашивают,
//  откуда сейчас растёт нить.
// ============================================================

using System;
using UnityEngine;

// ------------------------------------------------------------
// 1. РАСШИРЕНИЕ DEFINITION: пара режимов вместо одного
//    (небольшая правка к базовой системе)
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Stand Definition (Hybrid)")]
public class HybridStandDefinition : StandDefinition
{
    [Header("Гибрид: чем стенд является, когда 'убран'")]
    public EmbodimentKind dormantEmbodiment = EmbodimentKind.Integrated;

    // Для Stone Free:
    //   defaultEmbodiment = Follower   (гуманоид, leashRange = 5)
    //   dormantEmbodiment = Integrated (часть тела Джолин)
}

// В StandEntity меняются ровно две функции (показываю дельту):
//
//  public void Summon()
//  {
//      if (Definition is HybridStandDefinition)
//          SetEmbodiment(Definition.defaultEmbodiment);   // выйти наружу
//      ...
//  }
//
//  public void Dismiss()
//  {
//      if (Definition is HybridStandDefinition h)
//      {
//          SetEmbodiment(h.dormantEmbodiment);            // уйти в тело
//          // ВАЖНО: IsSummoned остаётся true по сути "стенд активен",
//          // вводим отдельное свойство:
//      }
//      ...
//  }
//
// Чтобы не мутить булевы флаги, честнее ввести enum:

public enum StandPresence
{
    Inactive,    // стенд вообще не активен (оглушён, запечатан)
    Dormant,     // "убран", но работает как часть владельца (Джолин по умолчанию)
    Manifested   // проявлен как отдельное тело
}

// StandEntity.Presence заменяет IsSummoned.
// Для обычного стенда: Inactive ↔ Manifested (Dormant не используется).
// Для Джолин:          Dormant  ↔ Manifested (Inactive только сюжетно).
// Команды принимаются в Dormant И Manifested — вот в чём разница с базой.

// ------------------------------------------------------------
// 2. ТОЧКА ПРОИСХОЖДЕНИЯ НИТИ: единственный вопрос,
//    который способности задают воплощению
// ------------------------------------------------------------

public interface IStringSource
{
    // Откуда физически растёт нить прямо сейчас
    Vector2 StringOrigin { get; }
    // Кто "расплетается" при затратных способностях —
    // в Manifested страдает тело стенда, в Dormant само тело Джолин
    IUnravelTarget UnravelTarget { get; }
}

public interface IUnravelTarget
{
    // Сколько себя ещё можно размотать (ресурс способностей Stone Free)
    float UnravelBudget { get; }
    void ConsumeUnravel(float meters);
    void RestoreUnravel(float meters);
}

// FollowerEmbodiment и IntegratedEmbodiment для Stone Free
// реализуют IStringSource каждый по-своему:

public class StoneFreeFollower : FollowerEmbodiment, IStringSource
{
    // leashRange = 5 из definition — гуманоид не отходит дальше
    StandBodyView _bodyView;
    UnravelPool _standBodyPool;   // разматывается ТЕЛО СТЕНДА

    public StoneFreeFollower(StandEntity e) : base(e) { }

    public Vector2 StringOrigin => BodyPosition;          // нить из руки стенда
    public IUnravelTarget UnravelTarget => _standBodyPool;

    protected Vector2 BodyPosition { get; set; } // выставляется в Tick базового класса
}

public class StoneFreeIntegrated : IntegratedEmbodiment, IStringSource
{
    readonly StandEntity _e;
    UnravelPool _ownerBodyPool;   // разматывается ТЕЛО ДЖОЛИН

    public StoneFreeIntegrated(StandEntity e) : base(e) { _e = e; }

    public Vector2 StringOrigin => _e.Owner.transform.position; // нить из её пальцев
    public IUnravelTarget UnravelTarget => _ownerBodyPool;
}

// UnravelPool — общий счётчик "сколько метров себя размотано".
// Геймплейно: чем больше размотано, тем ниже max HP / скорость,
// пока нити не втянуты обратно. Это её уникальный ресурс-риск.
public class UnravelPool : IUnravelTarget
{
    public float MaxMeters = 20f;
    float _used;

    public float UnravelBudget => MaxMeters - _used;
    public void ConsumeUnravel(float m) => _used = Mathf.Min(MaxMeters, _used + m);
    public void RestoreUnravel(float m) => _used = Mathf.Max(0, _used - m);
}

// ------------------------------------------------------------
// 3. СПОСОБНОСТИ STONE FREE — работают в обоих режимах
// ------------------------------------------------------------

// --- 3a. Нить-хук: grapple + дальний удар ---
[CreateAssetMenu(menuName = "JoJo/Abilities/String Grapple")]
public class StringGrappleDefinition : StandAbilityDefinition
{
    public float maxLength = 8f;
    public float costPerMeter = 0.5f;
    public override IStandAbility CreateRuntime(StandEntity owner)
        => new StringGrappleAbility(this, owner);
}

public class StringGrappleAbility : IStandAbility
{
    readonly StringGrappleDefinition _def;
    readonly StandEntity _e;

    public StringGrappleAbility(StringGrappleDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Grapple;
    public bool CanActivate => Source != null && Source.UnravelTarget.UnravelBudget > 1f;

    // Способность НЕ хранит режим — она каждый раз спрашивает воплощение.
    // Сменился режим — сменился и источник нити, автоматически.
    IStringSource Source => _e.Embodiment as IStringSource;

    public void Activate(in StandCommand cmd)
    {
        if (!CanActivate) return;
        Vector2 origin = Source.StringOrigin;

        var hit = Physics2D.Raycast(origin, cmd.direction, _def.maxLength,
                                    LayerMask.GetMask("Grapple", "Enemy"));
        if (!hit) return;

        float length = hit.distance;
        Source.UnravelTarget.ConsumeUnravel(length * _def.costPerMeter);

        if (hit.collider.CompareTag("GrapplePoint"))
            _e.Owner.Movement.StartRopeSwing(origin, hit.point, length);
        else
            hit.collider.GetComponent<Damageable>()?.TakeHit(StoneFreeDamage.StringWhip);
        // Заметь: в Manifested нить летит из стенда, стоящего до 5 м
        // впереди — фактическая дальность способности РАЗНАЯ в двух
        // режимах, и это получилось само, без единого if про режим.
    }

    public void Tick(float dt) { }
}

// --- 3b. Расплетание: пролезть в щель (гейтинг Phase) ---
[CreateAssetMenu(menuName = "JoJo/Abilities/Unravel Self")]
public class UnravelSelfDefinition : StandAbilityDefinition
{
    public float costMeters = 6f;
    public override IStandAbility CreateRuntime(StandEntity owner)
        => new UnravelSelfAbility(this, owner);
}

public class UnravelSelfAbility : IStandAbility
{
    readonly UnravelSelfDefinition _def;
    readonly StandEntity _e;
    bool _unraveled;

    public UnravelSelfAbility(UnravelSelfDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Phase;

    // Лорно-точное ограничение: разматывать СЕБЯ Джолин может
    // только когда стенд в теле. Если Stone Free проявлен — сначала
    // втяни его. Это не хардкод персонажа, а проверка режима:
    public bool CanActivate =>
        _e.Embodiment is StoneFreeIntegrated src
        && src.UnravelTarget.UnravelBudget >= _def.costMeters
        && !_unraveled;

    public void Activate(in StandCommand cmd)
    {
        if (!CanActivate) return;
        var src = (IStringSource)_e.Embodiment;
        src.UnravelTarget.ConsumeUnravel(_def.costMeters);
        _unraveled = true;

        // Владелец превращается в "нить": другой коллайдер, другой спрайт,
        // проходит сквозь решётки, уязвим к огню/лезвиям
        _e.Owner.Movement.EnterStringForm();
        _e.Owner.View.SetStringForm(true);
    }

    public void Tick(float dt)
    {
        if (_unraveled && Input.GetButtonUp("Special")) // отпустил — собралась
        {
            _unraveled = false;
            _e.Owner.Movement.ExitStringForm();
            _e.Owner.View.SetStringForm(false);
            ((IStringSource)_e.Embodiment).UnravelTarget.RestoreUnravel(_def.costMeters);
        }
    }
}

// --- 3c. Лента Мёбиуса: поздняя защитная способность ---
// Лорно: поверхность с одной стороной — атака "проходит" и
// возвращается, не разорвав петлю. Геймплейно: парри-стойка,
// конвертирующая входящий урон.
[CreateAssetMenu(menuName = "JoJo/Abilities/Mobius Strip")]
public class MobiusStripDefinition : StandAbilityDefinition
{
    public float window = 0.4f;       // окно парри
    public float costMeters = 3f;
    public override IStandAbility CreateRuntime(StandEntity owner)
        => new MobiusStripAbility(this, owner);
}

public class MobiusStripAbility : IStandAbility, IDamageInterceptor
{
    readonly MobiusStripDefinition _def;
    readonly StandEntity _e;
    float _windowLeft;
    float _cooldownLeft;

    public MobiusStripAbility(MobiusStripDefinition d, StandEntity e)
    {
        _def = d; _e = e;
        e.Owner.Combat.RegisterInterceptor(this); // встраиваемся в конвейер урона
    }

    public AbilityTag Tags => AbilityTag.None;   // не гейтинг — чисто боевая
    public bool CanActivate => _cooldownLeft <= 0
        && ((IStringSource)_e.Embodiment).UnravelTarget.UnravelBudget >= _def.costMeters;

    public void Activate(in StandCommand cmd)
    {
        if (!CanActivate) return;
        _windowLeft = _def.window;
        _cooldownLeft = _def.cooldown;
        ((IStringSource)_e.Embodiment).UnravelTarget.ConsumeUnravel(_def.costMeters);
        _e.Owner.View.PlayMobiusWeave(); // нити сплетаются в петлю перед ней
    }

    // Конвейер урона спрашивает всех перехватчиков перед применением
    public bool TryIntercept(ref DamageInfo dmg)
    {
        if (_windowLeft <= 0) return false;
        // Атака "прошла по ленте" и вернулась: урон отражается,
        // Джолин не получает ничего
        dmg.reflected = true;
        _windowLeft = 0;
        return true;
    }

    public void Tick(float dt)
    {
        _windowLeft -= dt;
        _cooldownLeft -= dt;
    }
}

// ------------------------------------------------------------
// 4. КАК ЭТО СОБИРАЕТСЯ В РЕДАКТОРЕ (без кода)
// ------------------------------------------------------------
//
//  StoneFree.asset (HybridStandDefinition)
//    ├─ power B, speed A, range C (5m), precision C
//    ├─ defaultEmbodiment: Follower   → префаб гуманоида Stone Free
//    ├─ dormantEmbodiment: Integrated → (префаб не нужен)
//    ├─ leashRange: 5
//    └─ abilities:
//         ├─ StringGrapple.asset   (tags: Grapple)
//         ├─ UnravelSelf.asset     (tags: Phase)
//         └─ MobiusStrip.asset     (открывается сюжетно, ч. "Heavy Weather")
//
//  Jolyne.prefab
//    └─ StandUser.standDefinition = StoneFree.asset
//
//  Что игрок чувствует в игре:
//   • стенд убран → Джолин бьёт сама, нити из пальцев, может
//     размотать СЕБЯ и пролезть; расплетание жрёт её же "метраж"
//   • зажал Summon → гуманоид выходит на 5 м: барраж, нить из его
//     руки достаёт дальше, но размотать себя нельзя, пока он снаружи
//   • баланс сам подталкивает переключаться — это и есть Джолин.
//
// ------------------------------------------------------------
// Заглушки контрактов, которых не было в базовом файле
// ------------------------------------------------------------
public interface IDamageInterceptor { bool TryIntercept(ref DamageInfo dmg); }
public struct DamageInfo { public float amount; public bool reflected; }
public enum StoneFreeDamage { StringWhip }
public class Damageable : MonoBehaviour { public void TakeHit(StoneFreeDamage t) { } }

// Дополнения к заглушкам PlayerMovement / PlayerView / PlayerCombat:
//   Movement: StartRopeSwing(origin, anchor, length), EnterStringForm(), ExitStringForm()
//   View:     SetStringForm(bool), PlayMobiusWeave()
//   Combat:   RegisterInterceptor(IDamageInterceptor)
