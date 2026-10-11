// ============================================================
//  LET IT BE / ДЖОНАТАН — шестой стресс-тест.
//  «Путь падающей звезды»
//
//  Новые вызовы для системы:
//   1. МЕТР С ГЕОГРАФИЕЙ: заряжается только у источников света
//      (окна, свечи), тратится очень быстро. Позже хамон-дыхание
//      освобождает экономику — рост персонажа записан в ресурс.
//   2. СТЕНД-НЕБОЕЦ: дерётся сам Джонатан. Стенд получает атаку
//      только через ПРЕДМЕТ — меч Luck and Pluck, кочующий между
//      ножнами на спине Джонатана и руками стенда.
//   3. Прогрессия аддитивна (не акты!): способности копятся
//      сюжетными флагами, старые не заменяются.
//
//  Прогрессия: Свет/Ослепление → Иллюзии → Хамон (дыхание +
//  удары по стендам) → Меч стенду → Телепорт к иллюзиям
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. СВЕТОВОЙ МЕТР: ресурс с географией
// ------------------------------------------------------------

public class LightSource : MonoBehaviour
{
    public float radius = 3f;
    public float chargeRate = 25f;     // окно днём — щедрое, свеча — скупая
    public bool lit = true;            // свечу можно задуть (или враг задует!)

    void OnEnable()  => LightRegistry.All.Add(this);
    void OnDisable() => LightRegistry.All.Remove(this);
}

public static class LightRegistry { public static readonly List<LightSource> All = new(); }

public class LightMeter
{
    public float Max = 100f;
    public float Current { get; private set; } = 100f;

    public bool BreathingUnlocked;             // сюжетный флаг: хамон освоен

    public bool TrySpend(float v)
    {
        if (Current < v) return false;
        Current -= v; return true;
    }
    public void Drain(float perSec, float dt) => Current = Mathf.Max(0, Current - perSec * dt);

    public void Tick(Vector2 ownerPos, float dt, bool isBreathing)
    {
        // РАННЯЯ ИГРА: заряд только у света. Планирование маршрута
        // от окна к свече — часть боя и исследования.
        foreach (var s in LightRegistry.All)
        {
            if (!s.lit) continue;
            if (Vector2.Distance(ownerPos, s.transform.position) <= s.radius)
                Current = Mathf.Min(Max, Current + s.chargeRate * dt);
        }

        // ПОЗДНЯЯ ИГРА: дыхание хамона — свет внутри себя.
        // Канал: Джонатан стоит, дышит, уязвим — но свободен от карты.
        if (BreathingUnlocked && isBreathing)
            Current = Mathf.Min(Max, Current + 40f * dt);
    }
}

// Дизайн-смысл: рост Джонатана записан В ЭКОНОМИКУ.
// Сначала он привязан к окнам и свечам — джентльмен при свете дня.
// Освоив хамон, он носит солнце в лёгких. Та же шкала, другой человек.

// ------------------------------------------------------------
// 2. СЛОИ УРОНА: чем можно бить стенды
// ------------------------------------------------------------

[Flags]
public enum DamageChannel
{
    Physical = 1 << 0,   // кулаки, меч, пули
    Stand    = 1 << 1,   // атаки стендов
    Hamon    = 1 << 2,   // солнечная энергия
}

// Damageable получает поле vulnerableTo:
//   человек:  Physical | Stand | Hamon
//   стенд:    Stand | Hamon      ← голые кулаки Джонатана бессильны,
//                                  ХАМОН-заряженные — нет. Весь смысл
//                                  тренировки в одном флаге.

// ------------------------------------------------------------
// 3. ВОПЛОЩЕНИЕ: follower, который не умеет бить (пока без меча)
// ------------------------------------------------------------

public class LetItBeFollower : FollowerEmbodiment
{
    readonly StandEntity _e;
    public bool HoldsSword { get; private set; }

    public LetItBeFollower(StandEntity e) : base(e) { _e = e; }

    public override void Execute(in StandCommand cmd)
    {
        switch (cmd.type)
        {
            case StandCommandType.LightAttack:
            case StandCommandType.HeavyAttack:
            case StandCommandType.Barrage:
                // СТЕНД НЕ БОЕЦ. Без меча атаки просто не существуют.
                if (!HoldsSword) return;
                base.Execute(cmd);          // с мечом — фехтует
                break;
            default:
                base.Execute(cmd);          // движение и т.п. — как у всех
                break;
        }
    }

    public void TakeSword()   { HoldsSword = true;  /* видно меч в руках */ }
    public void ReturnSword() { HoldsSword = false; /* меч в ножны на спине */ }
}

// ------------------------------------------------------------
// 4. СПОСОБНОСТИ ПО ПРОГРЕССИИ
// ------------------------------------------------------------

// === СТУПЕНЬ 1: Свет и Ослепление ===
[CreateAssetMenu(menuName = "JoJo/Abilities/Radiance")]
public class RadianceDefinition : StandAbilityDefinition
{
    public float drainPerSec = 18f;        // «тратится очень быстро»
    public float lightRadius = 5f;
    public float blindConeAngle = 40f;
    public float blindDuration = 2.5f;
    public override IStandAbility CreateRuntime(StandEntity o) => new RadianceAbility(this, o);
}

public class RadianceAbility : IStandAbility
{
    readonly RadianceDefinition _def;
    readonly StandEntity _e;
    public bool Shining { get; private set; }

    public RadianceAbility(RadianceDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Illuminate;   // гейтинг: тёмные зоны мира
    public bool CanActivate => _e.Owner.LightMeter.Current > 5f;

    // Режим 1: СИЯНИЕ (удержание) — стенд светит вокруг себя.
    //  Расход постоянный. Открывает тёмные проходы, отгоняет
    //  светобоязненных существ. И — синергия: освещённая зона
    //  ПРОСМАТРИВАЕМА → если рядом Джордж, свет Джонатана может
    //  и мешать его стенду. Свет — обоюдоострый.
    public void Activate(in StandCommand cmd)
    {
        if (cmd.type == StandCommandType.UseAbility && cmd.abilityIndex == 1)
            Flash(cmd.direction);
        else
            Shining = !Shining;
    }

    // Режим 2: ВСПЫШКА (разовая) — конус ослепления.
    //  Все VisionCone в конусе → blinded на N секунд.
    //  Боевой стан + стелс-инструмент. И КЛЮЧ К ДУЭТУ:
    //  ослеплённый враг — НЕ наблюдатель. Вспышка Джонатана
    //  буквально РАЗМОРАЖИВАЕТ Under Pressure Джорджа и
    //  открывает его частям дороги. Один жест — двое сильнее.
    void Flash(Vector2 dir)
    {
        if (!_e.Owner.LightMeter.TrySpend(25f)) return;
        foreach (var eye in ObservationRegistry.Instance.EyesInCone(
                     StandPos(), dir, _def.blindConeAngle, _def.lightRadius * 2f))
            eye.Blind(_def.blindDuration);
        // + светобоязненные враги: урон каналом Hamon-лайт? нет —
        // оставим чистым контролем, урон у Джонатана свой.
    }

    public void Tick(float dt)
    {
        if (!Shining) return;
        _e.Owner.LightMeter.Drain(_def.drainPerSec, dt);
        if (_e.Owner.LightMeter.Current <= 0) Shining = false;
        else IlluminationGrid.Instance.LightArea(StandPos(), _def.lightRadius);
    }

    Vector2 StandPos() => ((FollowerEmbodiment)_e.Embodiment).BodyPosition;
}

// === СТУПЕНЬ 2: Иллюзии — копии Джонатана ===
[CreateAssetMenu(menuName = "JoJo/Abilities/Mirror Self")]
public class MirrorSelfDefinition : StandAbilityDefinition
{
    public GameObject clonePrefab;
    public int maxClones = 2;              // растёт: 1 → 2 → 3
    public float cloneLifetime = 20f;
    public override IStandAbility CreateRuntime(StandEntity o) => new MirrorSelfAbility(this, o);
}

public class MirrorSelfAbility : IStandAbility
{
    readonly MirrorSelfDefinition _def;
    readonly StandEntity _e;
    readonly List<JonathanIllusion> _clones = new();
    public IReadOnlyList<JonathanIllusion> Clones => _clones;

    public MirrorSelfAbility(MirrorSelfDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Decoy;
    public bool CanActivate => _e.Owner.LightMeter.Current >= _e.Owner.LightMeter.Max * 0.99f;
    // «тратя метр ПОЛНОСТЬЮ» — иллюзия стоит ВСЁ. Ранней игрой это
    // страшное решение: слить весь свет на обманку. Дыхание позже
    // сделает цену переносимой — прогрессия опять в экономике.

    public void Activate(in StandCommand cmd)
    {
        if (!CanActivate) return;
        _e.Owner.LightMeter.TrySpend(_e.Owner.LightMeter.Max);

        if (_clones.Count >= _def.maxClones)
        { var old = _clones[0]; _clones.RemoveAt(0); old.Dissolve(); }

        var c = UnityEngine.Object.Instantiate(_def.clonePrefab,
            _e.Owner.transform.position, Quaternion.identity).GetComponent<JonathanIllusion>();
        c.Init(_e.Owner, _def.cloneLifetime, onGone: () => _clones.Remove(c));
        _clones.Add(c);
    }

    public void Tick(float dt) { }
}

// Иллюзия: выглядит и «пахнет» как Джонатан для вражеского AI
public class JonathanIllusion : MonoBehaviour
{
    Action _onGone;

    public void Init(StandUser original, float lifetime, Action onGone)
    {
        _onGone = onGone;
        ThreatSystem.RegisterDecoy(this);      // враги целятся в неё как в него
        Invoke(nameof(Expire), lifetime);
    }

    // Иллюзия — свет без плоти: любое КАСАНИЕ развеивает.
    // Враг «убил Джонатана» — и понял, что смотрел не туда.
    void OnTriggerEnter2D(Collider2D other) { Dissolve(); }

    public void Dissolve() { _onGone?.Invoke(); Destroy(gameObject); /* FX распада света */ }
    void Expire() { Dissolve(); }
}

// Дуэтная заметка: враги, ПАСУЩИЕ иллюзию, тратят взгляд.
// Каждая обманка Джонатана — украденный у Джорджа наблюдатель.

// === СТУПЕНЬ 3: Хамон ===
// (а) Дыхание — уже в LightMeter.BreathingUnlocked: канал восстановления.
//     Механика канала: кнопка удержанием, Джонатан не двигается,
//     получает +50% урона при прерывании ударом. Дышать в бою —
//     навык позиционирования, как перезарядка в шутерах.

// (б) Хамон-удар: кулаки обретают канал Hamon
[CreateAssetMenu(menuName = "JoJo/Abilities/Hamon Strike")]
public class HamonStrikeDefinition : StandAbilityDefinition
{
    public float meterCostPerHit = 8f;
    public override IStandAbility CreateRuntime(StandEntity o) => new HamonStrikeAbility(this, o);
}

public class HamonStrikeAbility : IStandAbility
{
    readonly HamonStrikeDefinition _def;
    readonly StandEntity _e;
    public bool Charged { get; private set; }

    public HamonStrikeAbility(HamonStrikeDefinition d, StandEntity e) { _e = e; _def = d; }

    public AbilityTag Tags => AbilityTag.StandTouch;   // гейтинг: враги-стенды,
                                                       // хамон-препятствия (лоза?)
    public bool CanActivate => _e.Owner.LightMeter.Current >= _def.meterCostPerHit;

    public void Activate(in StandCommand cmd) => Charged = !Charged;  // стойка-переключатель

    public void Tick(float dt)
    {
        // Пока стойка активна, PlayerCombat добавляет канал Hamon
        // к ударам Джонатана и списывает метр за попадание:
        _e.Owner.Combat.SetExtraChannel(
            Charged && CanActivate ? DamageChannel.Hamon : 0,
            onHit: () => _e.Owner.LightMeter.TrySpend(_def.meterCostPerHit));
    }
}

// === СТУПЕНЬ 4: Luck and Pluck — меч как кочующее снаряжение ===
[CreateAssetMenu(menuName = "JoJo/Abilities/Luck and Pluck")]
public class LuckAndPluckDefinition : StandAbilityDefinition
{
    public override IStandAbility CreateRuntime(StandEntity o) => new LuckAndPluckAbility(this, o);
}

public class LuckAndPluckAbility : IStandAbility
{
    readonly StandEntity _e;
    public SwordHolder Holder { get; private set; } = SwordHolder.SheathOnBack;

    public LuckAndPluckAbility(LuckAndPluckDefinition d, StandEntity e) { _e = e; }

    public AbilityTag Tags => AbilityTag.None;
    public bool CanActivate => true;

    // Одна кнопка — передача меча:
    //  ножны → руки стенда: стенд ВПЕРВЫЕ становится бойцом
    //   (LetItBeFollower.TakeSword) → двойное управление наконец
    //   работает как у всех: Джонатан кулаками, стенд клинком
    //  руки стенда → ножны: Джонатан может взять меч САМ
    //   (мувсет фехтования вместо кулаков — но тогда хамон-стойка
    //   идёт В МЕЧ: хамон по клинку, как у Цепеллиного стиля)
    public void Activate(in StandCommand cmd)
    {
        var f = (LetItBeFollower)_e.Embodiment;
        if (Holder == SwordHolder.SheathOnBack)
        { Holder = SwordHolder.StandHands; f.TakeSword(); }
        else
        { Holder = SwordHolder.SheathOnBack; f.ReturnSword(); }
    }

    public void Tick(float dt) { }
}

public enum SwordHolder { SheathOnBack, StandHands, JonathanHands }

// === СТУПЕНЬ 5: Телепорт к иллюзиям ===
[CreateAssetMenu(menuName = "JoJo/Abilities/Step Into Light")]
public class StepIntoLightDefinition : StandAbilityDefinition
{
    public float meterCost = 30f;
    public override IStandAbility CreateRuntime(StandEntity o) => new StepIntoLightAbility(this, o);
}

public class StepIntoLightAbility : IStandAbility
{
    readonly StepIntoLightDefinition _def;
    readonly StandEntity _e;

    public StepIntoLightAbility(StepIntoLightDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Phase;   // гейтинг-развязка: иллюзию
                                                  // можно поставить ЗА решётку
                                                  // (она свет — проходит) и шагнуть
    public bool CanActivate =>
        _e.GetAbility<MirrorSelfAbility>()?.Clones.Count > 0
        && _e.Owner.LightMeter.Current >= _def.meterCost;

    public void Activate(in StandCommand cmd)
    {
        var mirrors = _e.GetAbility<MirrorSelfAbility>();
        if (!CanActivate) return;
        _e.Owner.LightMeter.TrySpend(_def.meterCost);

        // Шаг в ближайшую (или выбранную) иллюзию: ОБМЕН местами —
        // Джонатан там, иллюзия здесь. Враг бил «Джонатана» в углу,
        // а настоящий уже за спиной. Свет решает, где правда.
        var target = mirrors.Clones[mirrors.Clones.Count - 1];
        Vector2 here = _e.Owner.transform.position;
        _e.Owner.transform.position = target.transform.position;
        target.transform.position = here;
    }

    public void Tick(float dt) { }
}

// ------------------------------------------------------------
// 5. СБОРКА
// ------------------------------------------------------------
//
//  LetItBe.asset (StandDefinition)
//   ├─ embodiment: LetItBeFollower (поводок 3м; без меча НЕ атакует)
//   └─ abilities (аддитивно, сюжетные флаги):
//        гл. 1  Radiance        (Illuminate; сияние + вспышка)
//        гл. ?  MirrorSelf      (Decoy; цена — ВЕСЬ метр)
//        гл. ?  Дыхание         (LightMeter.BreathingUnlocked = true)
//        гл. ?  HamonStrike     (StandTouch; кулаки бьют стенды)
//        гл. ?  LuckAndPluck    (меч кочует: ножны ↔ стенд ↔ Джонатан)
//        гл. ?  StepIntoLight   (Phase; телепорт-обмен с иллюзией)
//
//  Арка в экономике (главное изящество этого персонажа):
//   старт — свет чужой: привязан к окнам и свечам, тратится мигом,
//           иллюзия стоит ВСЮ шкалу — каждая трата как исповедь
//   рост  — свет свой: дыхание сняло географию, хамон дал кулакам
//           смысл, меч дал стенду руки
//   финал — свет и есть он: шагает между своими отражениями
//
//  Дуэт с Джорджем (если сойдутся в одной главе):
//   вспышка Джонатана гасит наблюдателей → части Under Pressure
//   отмирают и входят в предметы; иллюзии воруют взгляды с дорог
//   стенда Джорджа; НО сияние Джонатана делает зоны просматриваемыми
//   и может заморозить союзника. Свет и удача должны договариваться —
//   готовая динамика на совместный бой.
//
// ------------------------------------------------------------
// Дополнения к базе
// ------------------------------------------------------------
//  AbilityTag: + Illuminate, + Decoy, + StandTouch (1<<8..10)
//  Damageable: + vulnerableTo (DamageChannel), TakeHit учитывает канал
//  PlayerCombat: + SetExtraChannel(DamageChannel, Action onHit)
//  ObservationRegistry: + EyesInCone(pos, dir, halfAngle, range)
//  VisionCone: + Blind(duration) — таймерная слепота
//  IlluminationGrid: сетка освещённости для тёмных зон и гейтинга
//  StandUser: + LightMeter
//  ThreatSystem: RegisterDecoy — приманка для вражеского AI
