// ============================================================
//  CRAZY DIAMOND / ДЖОСКЕ — восьмой стресс-тест.
//  Фундаментальная новинка: ОБЪЕКТЫ ПОМНЯТ СВОЮ ИСТОРИЮ.
//
//  Канонические правила:
//   • возвращает вещи/людей в прежнее состояние
//   • НЕ лечит самого Джоске
//   • осколок, будучи восстановлен, ЛЕТИТ К СВОЕМУ ЦЕЛОМУ
//     (навигация и трекинг — прямо из манги)
//   • злой Джоске чинит НЕПРАВИЛЬНО (кривая сборка)
//   • может СЛИТЬ объекты: вплавить камень во врага, себя в стену
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ПАМЯТЬ ОБЪЕКТА: снапшоты + исходное состояние
// ------------------------------------------------------------

[Serializable]
public struct StateSnapshot
{
    public float time;
    public Vector2 position;
    public float integrity;         // 1 = цел, 0 = разрушен
    public string structureId;      // «мост_целый», «мост_обломки»
    public int variant;             // для кривой сборки
}

public class Rewindable : MonoBehaviour
{
    [Header("Исходное состояние (что значит «прежнее»)")]
    public string originStructureId;        // задаётся левел-дизайном
    public Vector2 originPosition;          // где объект «должен быть»

    readonly List<StateSnapshot> _history = new();
    const int MaxSnapshots = 64;
    const float SnapshotEvery = 0.5f;
    float _timer;

    public float Integrity { get; private set; } = 1f;
    public string CurrentStructureId { get; private set; }

    void Start()
    {
        CurrentStructureId = originStructureId;
        originPosition = transform.position;
        TakeSnapshot();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= SnapshotEvery) { _timer = 0; TakeSnapshot(); }
    }

    void TakeSnapshot()
    {
        if (_history.Count >= MaxSnapshots) _history.RemoveAt(0);
        _history.Add(new StateSnapshot
        {
            time = Time.time,
            position = transform.position,
            integrity = Integrity,
            structureId = CurrentStructureId
        });
    }

    public void Damage(float amount, string brokenStructureId = null)
    {
        Integrity = Mathf.Max(0, Integrity - amount);
        if (Integrity <= 0 && brokenStructureId != null)
            SetStructure(brokenStructureId);
    }

    // ВОССТАНОВЛЕНИЕ: не «полечить», а вернуть К ИСХОДНОМУ.
    // variant != 0 — кривая сборка (см. §4)
    public void RestoreToOrigin(int variant = 0)
    {
        Integrity = 1f;
        SetStructure(originStructureId, variant);
        TakeSnapshot();
    }

    // Откат на N секунд назад — для головоломок с «недавним прошлым»
    public void RestoreToRecent(float secondsAgo)
    {
        var target = _history.FindLast(s => s.time <= Time.time - secondsAgo);
        Integrity = target.integrity;
        SetStructure(target.structureId, target.variant);
        transform.position = target.position;
    }

    void SetStructure(string id, int variant = 0)
    {
        CurrentStructureId = id;
        // подмена визуала/коллайдеров: целый мост ↔ куча обломков;
        // variant влияет на префаб сборки (кривые варианты)
    }
}

// ------------------------------------------------------------
// 2. ОСКОЛОК ЛЕТИТ К ЦЕЛОМУ: навигация из манги
// ------------------------------------------------------------

public class Fragment : MonoBehaviour
{
    public Rewindable wholeObject;          // чьё это «целое»
    public bool restored;

    // Джоске коснулся осколка → тот ВОЗВРАЩАЕТСЯ к целому:
    // летит через уровень по своему пути. Три применения:
    //  1. ТРЕКИНГ: осколок ведёт к скрытому целому — «где дверь
    //     от этой ручки?» Осколок-проводник вместо маркера квеста.
    //  2. ТРАНСПОРТ: Джоске ХВАТАЕТСЯ за крупный осколок и летит
    //     с ним (канон: поездка на восстановленном). Гейтинг Grapple
    //     без крюка — на обломке моста через пропасть.
    //  3. СНАРЯД: осколок вазы, восстановленный У ЛИЦА врага,
    //     летит к вазе СКВОЗЬ врага.
    public void Restore(bool grabable)
    {
        restored = true;
        StartCoroutine(FlyHome(grabable));
    }

    System.Collections.IEnumerator FlyHome(bool grabable)
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = !grabable;
        while (Vector2.Distance(transform.position, wholeObject.originPosition) > 0.1f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position, wholeObject.originPosition, 9f * Time.deltaTime);
            // по пути: урон врагам на линии, пассажир на борту
            yield return null;
        }
        wholeObject.AbsorbFragment(this);
        Destroy(gameObject);
    }
}

public static class RewindableExtensions
{
    public static void AbsorbFragment(this Rewindable whole, Fragment f)
    {
        // целое собирается по кускам; все осколки дома → RestoreToOrigin
    }
}

// ------------------------------------------------------------
// 3. СПОСОБНОСТИ CRAZY DIAMOND
// ------------------------------------------------------------

// Воплощение — классический Follower (кулачный, барраж), атаки
// из базовой системы. Уникальность целиком в способностях:

[CreateAssetMenu(menuName = "JoJo/Abilities/Restoration")]
public class RestorationDefinition : StandAbilityDefinition
{
    public float touchRange = 1.5f;
    public override IStandAbility CreateRuntime(StandEntity o) => new RestorationAbility(this, o);
}

public class RestorationAbility : IStandAbility
{
    readonly RestorationDefinition _def;
    readonly StandEntity _e;

    public RestorationAbility(RestorationDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Restore;   // новый тег гейтинга:
                                                    // разрушенные мосты, лестницы,
                                                    // механизмы — «почини, чтобы пройти»
    public bool CanActivate => true;                // канон: без ресурса, но КАСАНИЕМ

    public void Activate(in StandCommand cmd)
    {
        var hit = Physics2D.OverlapCircle(StandHandPos(), _def.touchRange,
            LayerMask.GetMask("Rewindable", "Fragment", "Ally"));
        if (hit == null) return;

        // ЖЁСТКОЕ ПРАВИЛО КАНОНА: себя не чинит.
        if (hit.transform == _e.Owner.transform) return;

        if (hit.TryGetComponent<Fragment>(out var frag))
            frag.Restore(grabable: true);
        else if (hit.TryGetComponent<Rewindable>(out var rw))
            rw.RestoreToOrigin(variant: _e.Owner.Emotion.AngerVariant); // §4
        else if (hit.TryGetComponent<AllyHealth>(out var ally))
            ally.FullRestore();                     // союзников — лечит
    }

    public void Tick(float dt) { }
    Vector2 StandHandPos() => ((FollowerEmbodiment)_e.Embodiment).BodyPosition;
}

// ------------------------------------------------------------
// 4. ЗЛОСТЬ = КРИВАЯ СБОРКА
// ------------------------------------------------------------

public class EmotionState : MonoBehaviour
{
    public float Anger { get; private set; }        // растёт от урона, насмешек
                                                    // над причёской (сюжетные триггеры)
    public int AngerVariant => Anger > 0.7f ? UnityEngine.Random.Range(1, 4) : 0;
    // variant 0 — правильная сборка; 1..3 — кривые префабы.
    // Головоломки на этом: иногда КРИВАЯ сборка полезна —
    // криво восстановленная решётка оставляет щель, криво
    // собранная лестница достаёт туда, куда прямая не доставала.
    // Управлять злостью Джоске = управлять качеством ремонта.

    public void AddAnger(float v) => Anger = Mathf.Clamp01(Anger + v);
    public void Calm(float v)     => Anger = Mathf.Clamp01(Anger - v);
}

// ------------------------------------------------------------
// 5. СЛИЯНИЕ: восстановить ДВА объекта КАК ОДИН
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Fusion Repair")]
public class FusionRepairDefinition : StandAbilityDefinition
{
    public override IStandAbility CreateRuntime(StandEntity o) => new FusionRepairAbility(this, o);
}

public class FusionRepairAbility : IStandAbility
{
    readonly StandEntity _e;
    public FusionRepairAbility(FusionRepairDefinition d, StandEntity e) { _e = e; }

    public AbilityTag Tags => AbilityTag.Fuse;      // гейтинг: вплавить рычаг
                                                    // в гнездо, себя в стену (стелс!),
                                                    // ключ во врага-носильщика
    public bool CanActivate => true;

    public void Activate(in StandCommand cmd)
    {
        // Два пересекающихся объекта в зоне касания → FusionBond:
        // жёсткая связь «как будто всегда были одним предметом».
        // Расторгается ТОЛЬКО повторным восстановлением Джоске
        // (вернуть «прежнее» = разделить) — эксклюзивный ключ.
        var hits = Physics2D.OverlapCircleAll(
            ((FollowerEmbodiment)_e.Embodiment).BodyPosition, 1.2f,
            LayerMask.GetMask("Rewindable", "Enemy", "Player"));
        if (hits.Length < 2) return;
        FusionBond.Create(hits[0].transform, hits[1].transform);
    }

    public void Tick(float dt) { }
}

public class FusionBond : MonoBehaviour
{
    public static void Create(Transform a, Transform b)
    {
        // FixedJoint2D + пометка «слито Crazy Diamond»
    }
    public void Undo() { /* восстановление разделяет */ }
}

// ------------------------------------------------------------
// 6. ЧТО ЭТО ДАЁТ ИГРЕ
// ------------------------------------------------------------
//
//  Бой (файтинг-слой, как ты и хотел для Джотаро/Джоске):
//   • классический барраж-дуэлянт из базовой системы
//   • ремонт в комбо: разбил укрытие врага → врag открыт →
//     восстановил укрытие ЗА его спиной → заперт
//   • осколок-снаряд: сломай вазу об одного, восстанови у лица
//     другого — рикошет сквозь строй к «дому»
//  Головоломки:
//   • Restore-гейтинг: обломки моста = мост, если Джоске рядом
//   • осколок-проводник: находка ведёт к своему целому через уровень
//   • транспорт на летящем обломке
//   • злость как ключ: кривые сборки открывают то, что прямые — нет
//  Фундамент для остальных:
//   • Rewindable/история пригодится Джоэлю (расклеить = откат),
//     Гаппи (вернуть украденное свойство), ловушкам, боссам
//   • FusionBond — первый шаг к механике СВЯЗЕЙ, которую Джоэль
//     разовьёт мембранами
//
//  Дополнения к базе: AbilityTag + Restore, Fuse (1<<11..12)
// ============================================================
