// ============================================================
//  NOVEMBER RAIN / ДЖОДИО — четвёртый стресс-тест.
//  Новый архетип: атака-ПОЛЕ вместо атаки-события.
//   1. Стенд висит НАД владельцем (вертикальный якорь, не боковой).
//   2. Оружие — зона дождя под его телом: существует непрерывно,
//      пока включена; урон тиками, не кадрами удара.
//   3. Мощность — дроссель (морось → ливень → потоп), меняющий
//      урон, радиус и взаимодействия со средой.
//   4. "Прицеливание" = перемещение тела стенда в пределах поводка.
//   5. Дождь оставляет след в мире: лужи как персистентное
//      состояние (скольжение, проводимость, тушение огня).
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ВОПЛОЩЕНИЕ: Follower с вертикальным якорем
//    Наследуем — меняем только геометрию следования
// ------------------------------------------------------------

public class OverheadFollowerEmbodiment : FollowerEmbodiment
{
    // База держала стенд сбоку-сзади. Здесь якорь — над головой,
    // а leashRange из definition ограничивает ГОРИЗОНТАЛЬНЫЙ вынос:
    // Джодио может "выставить" дождь вперёд, но недалеко.

    readonly StandEntity _e;
    float _hoverHeight = 3.2f;      // 3-метровый силуэт нависает
    float _horizontalOffset;        // текущий вынос (управляется игроком)

    public OverheadFollowerEmbodiment(StandEntity e) : base(e) { _e = e; }

    public Vector2 GroundFootprintCenter =>
        new(BodyPosition.x, _e.Owner.transform.position.y); // проекция на землю

    public override void Tick(float dt)
    {
        if (_e.Presence != StandPresence.Manifested) return;
        Vector2 owner = _e.Owner.transform.position;
        Vector2 target = new(owner.x + _horizontalOffset, owner.y + _hoverHeight);
        BodyPosition = Vector2.Lerp(BodyPosition, target, 10f * dt);
        SyncBody();
    }

    public override void Execute(in StandCommand cmd)
    {
        switch (cmd.type)
        {
            case StandCommandType.Move:
                // Стик в фокусе стенда двигает ВЫНОС, а не тело напрямую:
                // дождевая колонна скользит по земле влево-вправо
                _horizontalOffset = Mathf.Clamp(
                    _horizontalOffset + cmd.direction.x * 6f * Time.deltaTime,
                    -_e.Definition.leashRange, _e.Definition.leashRange);
                break;

            // Кулаки у November Rain есть, но вторичны — обычный удар
            // сверху вниз (добить придавленного дождём):
            case StandCommandType.LightAttack:
                PlayAttack(0, Vector2.down);
                break;
        }
    }
}

// Регистрация в фабрике: новый kind ИЛИ параметр анкера в definition.
// Чистый вариант — расширить enum:
//   EmbodimentKind.OverheadFollower => new OverheadFollowerEmbodiment(e)

// ------------------------------------------------------------
// 2. ДОЖДЬ КАК ДРОССЕЛЬ: уровни мощности
// ------------------------------------------------------------

public enum RainIntensity { Off, Drizzle, Shower, Downpour }

[Serializable]
public class RainTier
{
    public RainIntensity intensity;
    public float radius;            // Drizzle шире, Downpour уже
    public float dps;               // Downpour — тонны на каплю
    public float slowFactor;        // насколько вязнут враги в зоне
    public float meterDrainPerSec;  // расход "туч" (ресурс)
    public bool crushesObjects;     // Downpour ломает хрупкое  → HeavyImpact
    public bool dousesFlame;        // любой уровень тушит огонь → DouseFire
    public float puddleGrowthRate;  // как быстро копится лужа
}

[CreateAssetMenu(menuName = "JoJo/Abilities/Rain Field")]
public class RainFieldDefinition : StandAbilityDefinition
{
    public List<RainTier> tiers;    // Drizzle / Shower / Downpour
    public float meterMax = 100f;
    public float meterRegenPerSec = 8f;   // тучи собираются, пока дождь выключен
    public override IStandAbility CreateRuntime(StandEntity o) => new RainFieldAbility(this, o);
}

// ------------------------------------------------------------
// 3. СПОСОБНОСТЬ-ПОЛЕ: живёт, пока включена
// ------------------------------------------------------------

public class RainFieldAbility : IStandAbility
{
    readonly RainFieldDefinition _def;
    readonly StandEntity _e;

    RainIntensity _current = RainIntensity.Off;
    float _meter;
    RainZone _zone;                 // физическое поле в мире

    public RainFieldAbility(RainFieldDefinition d, StandEntity e)
    {
        _def = d; _e = e;
        _meter = d.meterMax;
    }

    // Теги ЗАВИСЯТ ОТ ДОСТУПНОЙ МОЩНОСТИ — гейтинг спрашивает
    // "можешь ли ты В ПРИНЦИПЕ", поэтому отдаём максимум тиров:
    public AbilityTag Tags
    {
        get
        {
            var t = AbilityTag.None;
            foreach (var tier in _def.tiers)
            {
                if (tier.dousesFlame) t |= AbilityTag.DouseFire;
                if (tier.crushesObjects) t |= AbilityTag.HeavyImpact;
            }
            return t;
        }
    }

    public bool CanActivate => _e.Presence == StandPresence.Manifested && _meter > 5f;

    // Activate = провернуть дроссель на ступень (или выключить)
    public void Activate(in StandCommand cmd)
    {
        _current = _current switch
        {
            RainIntensity.Off      => RainIntensity.Drizzle,
            RainIntensity.Drizzle  => RainIntensity.Shower,
            RainIntensity.Shower   => RainIntensity.Downpour,
            RainIntensity.Downpour => RainIntensity.Off,
            _ => RainIntensity.Off
        };
        // Отдельная кнопка "заглушить" может сразу ставить Off —
        // важно для стелса: Джодио выключает дождь мгновенно.
    }

    public void Tick(float dt)
    {
        // Ресурс: тучи копятся в тишине, тратятся под ливнем
        if (_current == RainIntensity.Off)
        {
            _meter = Mathf.Min(_def.meterMax, _meter + _def.meterRegenPerSec * dt);
            _zone?.SetActive(false);
            return;
        }

        var tier = _def.tiers.Find(t => t.intensity == _current);
        _meter -= tier.meterDrainPerSec * dt;
        if (_meter <= 0) { _current = RainIntensity.Off; return; }

        // Поле следует за проекцией тела стенда на землю
        var overhead = (OverheadFollowerEmbodiment)_e.Embodiment;
        _zone ??= RainZone.Create(_e);
        _zone.SetActive(true);
        _zone.Configure(overhead.GroundFootprintCenter, tier);
        _zone.Tick(dt);
    }
}

// ------------------------------------------------------------
// 4. ЗОНА В МИРЕ: непрерывный хитбокс + среда
// ------------------------------------------------------------

public class RainZone : MonoBehaviour
{
    StandEntity _owner;
    RainTier _tier;
    readonly List<PuddleCell> _touchedCells = new();

    public static RainZone Create(StandEntity owner)
    {
        var go = new GameObject("RainZone");
        var z = go.AddComponent<RainZone>();
        z._owner = owner;
        return z;
    }

    public void SetActive(bool on) => gameObject.SetActive(on);

    public void Configure(Vector2 groundCenter, RainTier tier)
    {
        _tier = tier;
        transform.position = groundCenter;
        // визуал: колонна дождя от тела стенда до земли, ширина = radius*2
    }

    public void Tick(float dt)
    {
        // --- Урон и замедление: непрерывное поле, тики раз в 0.25с ---
        var hits = Physics2D.OverlapBoxAll(
            transform.position + Vector3.up * 1.5f,
            new Vector2(_tier.radius * 2f, 3f), 0f,
            LayerMask.GetMask("Enemy", "Destructible", "Interactive"));

        foreach (var h in hits)
        {
            if (h.TryGetComponent<Damageable>(out var dmg))
                dmg.TakeContinuous(_tier.dps * dt, DamageKind.CrushingRain);

            if (h.TryGetComponent<MovementDebuffable>(out var mv))
                mv.ApplySlow(_tier.slowFactor, duration: 0.3f);

            if (_tier.crushesObjects && h.TryGetComponent<Crushable>(out var cr))
                cr.AccumulateCrush(_tier.dps * dt);   // хрупкий пол проламывается!

            if (_tier.dousesFlame && h.TryGetComponent<Flammable>(out var fl))
                fl.Extinguish();
        }

        // --- Персистентный след: лужи ---
        var cell = PuddleGrid.Instance.GetCellAt(transform.position);
        cell.AddWater(_tier.puddleGrowthRate * dt);
        if (!_touchedCells.Contains(cell)) _touchedCells.Add(cell);
    }
}

// Лужи — состояние МИРА, не стенда: остаются после выключения дождя,
// высыхают со временем, взаимодействуют с чужими способностями.
public class PuddleCell
{
    public float Water { get; private set; }   // 0..1

    public bool IsSlippery   => Water > 0.3f;  // враги (и Джодио!) скользят
    public bool IsConductive => Water > 0.5f;  // электро-атаки бьют по площади
    public bool IsDeep       => Water > 0.8f;  // гасит падение, топит мелких врагов

    public void AddWater(float v) => Water = Mathf.Min(1f, Water + v);
    public void Tick(float dt)    => Water = Mathf.Max(0f, Water - 0.01f * dt); // высыхание
}

public class PuddleGrid : MonoBehaviour
{
    public static PuddleGrid Instance;
    public PuddleCell GetCellAt(Vector2 pos) => new(); // сетка по X вдоль уровня
}

// ------------------------------------------------------------
// 5. СБОРКА
// ------------------------------------------------------------
//
//  NovemberRain.asset (StandDefinition)
//   ├─ embodiment: OverheadFollower (префаб 3-метрового силуэта)
//   ├─ leashRange: 2.5   ← горизонтальный вынос дождевой колонны
//   └─ abilities: RainField.asset
//        tiers:
//         Drizzle:  radius 3.0, dps 2,  slow 15%, drain 4/с,  puddle 0.05
//         Shower:   radius 2.2, dps 12, slow 40%, drain 12/с, puddle 0.12
//         Downpour: radius 1.4, dps 45, slow 80%, drain 30/с, puddle 0.25
//                   crushes: да  ← HeavyImpact-гейтинг (проломить пол!)
//         все тиры: douses: да  ← DouseFire-гейтинг (пройти сквозь пожар)
//
//  Дизайн-логика дросселя: чем мощнее — тем УЖЕ и ПРОЖОРЛИВЕЕ.
//  Морось — контроль толпы широким конусом; потоп — точечный пресс,
//  который надо ещё и навести выносом стенда. Прицеливание телом
//  стенда = двойное управление работает буквально каждым боем.
//
//  Что чувствует игрок:
//   • бой = менеджмент дросселя и позиции колонны: вязнущие в мороси
//     враги добиваются кулаком стенда сверху или подводятся под потоп
//   • тучи кончились — Джодио уязвим, пока "молчит" и копит метр:
//     естественный ритм давление/пауза
//   • лужи остаются: прошёл с дождём по коридору — за спиной
//     скользкая полоса, тормозящая погоню. А электро-враг превращает
//     твою же лужу в опасность. Среда помнит бой.
//   • гейтинг: пожар в проходе (DouseFire), хрупкий пол в шахту
//     (Downpour = HeavyImpact) — всё через те же теги, что у всех.
//
// ------------------------------------------------------------
// Заглушки новых контрактов
// ------------------------------------------------------------
public enum DamageKind { Hit, CrushingRain }
public class MovementDebuffable : MonoBehaviour { public void ApplySlow(float f, float duration) { } }
public class Crushable : MonoBehaviour { public void AccumulateCrush(float v) { } }
public class Flammable : MonoBehaviour { public void Extinguish() { } }

// Дополнения к базе:
//   AbilityTag: + DouseFire = 1 << 7
//   EmbodimentKind: + OverheadFollower
//   FollowerEmbodiment: BodyPosition/Tick/Execute становятся virtual/protected,
//     добавляется protected SyncBody()
//   Damageable: + TakeContinuous(float dps, DamageKind kind)
