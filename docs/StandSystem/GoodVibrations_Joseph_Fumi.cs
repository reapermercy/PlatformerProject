// ============================================================
//  GOOD VIBRATIONS / ДЖОЗЕФ ⇄ ФУМИ — седьмой стресс-тест.
//
//  Три новых механики:
//   1. ЛИАНЫ: пронизывают тело владельца (integrated) И собираются
//      в гуманоида за спиной (assembled) — третий вариант гибрида.
//      Лианы — ПРОВОДНИКИ: хлысты, хватки, и главное — каналы
//      энергии до цели.
//   2. ДВЕ ШКОЛЫ: Хамон (Джозеф) и Спин (Фуми) — разные каналы
//      урона с разными правилами проводимости по среде.
//   3. РЕЗОНАНС: переключение между двумя альтернативными
//      Джозефами = переключение МИРОВ на одной геометрии.
//      Они не могут встретиться — но учатся друг у друга
//      через стенд: взаимная прокачка техник.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ЛИАНЫ: гибрид «в теле ↔ собран за спиной»
// ------------------------------------------------------------

// Та же пара режимов, что у Джолин/Джорджа, но со своим смыслом:
//  DORMANT (в теле): лианы проросли сквозь владельца —
//    хлысты из рук (Hermit Purple-наследие), проводка энергии
//    через СОБСТВЕННЫЕ удары владельца
//  MANIFESTED (собран): лианы сплетаются в гуманоида за спиной —
//    он дерётся сплетёнными конечностями, дальность выше,
//    и он может ЗАЯКОРИТЬСЯ: воткнуть лианы в поверхность
//    и стать живым проводом между точками

public class VineEmbodiment : IStandEmbodiment
{
    readonly StandEntity _e;
    GameObject _assembled;             // тело гуманоида (Manifested)
    readonly List<VineAnchor> _anchors = new();   // воткнутые лианы

    public bool SupportsDirectControl => _e.Presence == StandPresence.Manifested;

    public VineEmbodiment(StandEntity e) { _e = e; }

    public void Setup()
    {
        _assembled = UnityEngine.Object.Instantiate(_e.Definition.embodimentPrefab);
        _assembled.SetActive(false);
    }
    public void Teardown() { if (_assembled) UnityEngine.Object.Destroy(_assembled); }

    public void OnSummon()   // собраться за спиной
    {
        _assembled.SetActive(true);
        _e.Owner.View.SetVinePierceOverlay(false);   // лианы вышли из тела
    }
    public void OnDismiss()  // разобраться в тело
    {
        _assembled.SetActive(false);
        _e.Owner.View.SetVinePierceOverlay(true);    // проросли сквозь кожу
        foreach (var a in _anchors) a.Release();
        _anchors.Clear();
    }

    public void Tick(float dt)
    {
        if (_e.Presence != StandPresence.Manifested) return;
        Vector2 target = (Vector2)_e.Owner.transform.position + new Vector2(-0.8f, 0.6f);
        _assembled.transform.position =
            Vector2.Lerp(_assembled.transform.position, target, 10f * dt);
    }

    public void Execute(in StandCommand cmd)
    {
        switch (cmd.type)
        {
            case StandCommandType.LightAttack:   // хлыст (в обоих режимах!)
                VineWhip(cmd.direction);
                break;
            case StandCommandType.HeavyAttack:   // заякориться: воткнуть лиану
                TryAnchor(cmd.worldPoint);
                break;
            case StandCommandType.Barrage:       // гуманоид: шквал плетей
                if (_e.Presence == StandPresence.Manifested)
                    _assembled.GetComponent<StandBodyView>().EnterBarrage(cmd.direction);
                break;
        }
    }

    void VineWhip(Vector2 dir)
    {
        Vector2 origin = _e.Presence == StandPresence.Manifested
            ? (Vector2)_assembled.transform.position
            : (Vector2)_e.Owner.transform.position;   // из рук владельца

        var hit = Physics2D.Raycast(origin, dir, 6f, LayerMask.GetMask("Enemy", "Grapple", "Conduit"));
        if (!hit) return;

        // ХЛЫСТ — ЭТО ПРОВОД: если владелец держит заряд школы,
        // энергия течёт по лиане в цель (см. §2 проводимость)
        var charge = _e.Owner.Technique.ActiveCharge;
        EnergyConduction.Deliver(hit, charge, viaVine: true);

        if (hit.collider.CompareTag("GrapplePoint"))
            _e.Owner.Movement.StartRopeSwing(origin, hit.point, hit.distance);
    }

    void TryAnchor(Vector2 point)
    {
        if (_anchors.Count >= 2) { _anchors[0].Release(); _anchors.RemoveAt(0); }
        var a = VineAnchor.Plant(point, this);
        if (a != null) _anchors.Add(a);
        // Две заякоренные лианы = стенд-ПРОВОД между точками:
        // энергия школы течёт по линии якорь—якорь. Электрифицировать
        // перила хамоном, раскрутить механизм спином на расстоянии.
    }
}

public class VineAnchor
{
    public Vector2 Point;
    public static VineAnchor Plant(Vector2 p, VineEmbodiment owner) => new() { Point = p };
    public void Release() { }
}

// ------------------------------------------------------------
// 2. ДВЕ ШКОЛЫ: каналы с разной проводимостью
// ------------------------------------------------------------

// DamageChannel уже есть у Джонатана: Physical | Stand | Hamon.
// Добавляем: Spin = 1 << 3.
//
// ПРОВОДИМОСТЬ ПО СРЕДЕ (сердце головоломок этой части):
//   ХАМОН течёт по живому и по воде:
//     лоза, вода, люди, дерево(живое) — проводит; металл — глохнет
//   СПИН передаётся через вращение и металл:
//     тросы, шестерни, рельсы, металл — проводит; вода — гасит
//
// Один и тот же хлыст лианой в лужу: хамон растекается площадью,
// спин — бессилен. Хлыст в шестерню: спин раскручивает весь
// механизм, хамон — глохнет. Среда читается как электросхема.

public enum TechniqueSchool { None, Hamon, Spin }

public static class EnergyConduction
{
    public static void Deliver(RaycastHit2D hit, TechniqueSchool charge, bool viaVine)
    {
        if (charge == TechniqueSchool.None) return;

        var conduit = hit.collider.GetComponent<Conduit>();
        if (conduit != null && conduit.Conducts(charge))
            conduit.Propagate(charge);          // энергия бежит по цепи дальше!
        else if (hit.collider.TryGetComponent<Damageable>(out var d))
            d.TakeHit(charge == TechniqueSchool.Hamon
                ? DamageChannel.Hamon : DamageChannel.Spin);
    }
}

public class Conduit : MonoBehaviour
{
    public ConduitMaterial material;
    public List<Conduit> connected;             // цепь: лужа→перила? нет — по правилам

    public bool Conducts(TechniqueSchool s) => material switch
    {
        ConduitMaterial.Water   => s == TechniqueSchool.Hamon,
        ConduitMaterial.Organic => s == TechniqueSchool.Hamon,
        ConduitMaterial.Metal   => s == TechniqueSchool.Spin,
        ConduitMaterial.Rotor   => s == TechniqueSchool.Spin,
        _ => false
    };

    public void Propagate(TechniqueSchool s)
    {
        Activate(s);                            // урон/эффект в этой секции
        foreach (var c in connected)
            if (c.Conducts(s)) c.Propagate(s);  // и дальше по совместимой цепи
    }

    void Activate(TechniqueSchool s) { /* вспышка по воде / раскрутка ротора */ }
}

public enum ConduitMaterial { Water, Organic, Metal, Rotor, Stone }

// ------------------------------------------------------------
// 3. РЕЗОНАНС: два мира на одной геометрии
// ------------------------------------------------------------

// Джозеф и Фуми НЕ МОГУТ встретиться — они один человек в двух
// реальностях. Переключение = смена активного мира И персонажа
// В ТОЙ ЖЕ ТОЧКЕ. Уровни строятся парой слоёв на одной геометрии:
// стены совпадают, НАПОЛНЕНИЕ разное (1938 Нью-Йорк / SBR-мир).

public enum WorldSide { JosephWorld, FumiWorld }

public class ResonanceSwap : MonoBehaviour
{
    public StandUser joseph;        // школа: Hamon (родная)
    public StandUser fumi;          // школа: Spin  (родная)
    public WorldSide Active { get; private set; } = WorldSide.JosephWorld;

    public float swapCooldown = 1.5f;
    float _cd;

    public StandUser ActiveUser => Active == WorldSide.JosephWorld ? joseph : fumi;

    public void TrySwap()
    {
        if (_cd > 0) return;
        _cd = swapCooldown;

        var from = ActiveUser; 
        Active = Active == WorldSide.JosephWorld ? WorldSide.FumiWorld : WorldSide.JosephWorld;
        var to = ActiveUser;

        // Тот же X/Y, другой мир: позиция наследуется
        to.transform.position = from.transform.position;
        WorldLayers.Instance.SetActiveSide(Active);   // слои рендера/коллизий
        from.gameObject.SetActive(false);
        to.gameObject.SetActive(true);

        // РЕЗОНАНСНЫЙ ГЕЙТИНГ: препятствие есть в одном мире и
        // отсутствует в другом (стена 1938 ↔ пустырь SBR-мира).
        // Плюс СВЯЗАННЫЕ ПАРЫ: механизм, запущенный спином у Фуми,
        // резонансно сдвигает свой аналог в мире Джозефа.
    }

    void Update() => _cd -= Time.deltaTime;
}

// Связанная пара объектов между мирами — эхо действий:
public class ResonantPair : MonoBehaviour
{
    public ResonantPair counterpart;      // мой близнец в другом мире
    [Range(0f, 1f)] public float echoStrength = 0.5f;

    // Открыл дверь у Фуми → у Джозефа её близнец ПРИОТКРЫЛСЯ (эхо).
    // Головоломки на два мира: докрутить эхо с другой стороны.
    public void OnActivated(float amount)
        => counterpart?.ReceiveEcho(amount * echoStrength);

    public void ReceiveEcho(float amount) { /* частичный сдвиг состояния */ }
}

// ------------------------------------------------------------
// 4. ВЗАИМНОЕ ОБУЧЕНИЕ: отношения, записанные в прокачку
// ------------------------------------------------------------

// Каждый носитель имеет РОДНУЮ школу (мастерство = 1.0) и ЧУЖУЮ,
// которая растёт от действий ПАРТНЁРА. Стенд — учитель-посредник:
// то, что Джозеф делает хамоном, Фуми «слышит» через резонанс.

public class TechniqueLink : MonoBehaviour
{
    public TechniqueProfile joseph = new() { native = TechniqueSchool.Hamon };
    public TechniqueProfile fumi   = new() { native = TechniqueSchool.Spin  };

    // Вызывается при КАЖДОМ содержательном применении техники
    public void OnTechniqueUsed(StandUser who, TechniqueSchool school, float weight)
    {
        var (actor, listener) = who == GetJoseph() ? (joseph, fumi) : (fumi, joseph);
        if (school != actor.native) return;         // учат только родному

        listener.foreignMastery = Mathf.Min(1f, listener.foreignMastery + weight * 0.01f);
        // Пороги открывают чужие техники ступенями:
        //  0.25 — базовый заряд чужой школы (слабый, дорогой)
        //  0.50 — проводимость чужой школы по лианам
        //  0.75 — одна фирменная техника партнёра
        //  1.00 — СДВОЕННЫЙ ЗАРЯД: Hamon+Spin в одном хлысте,
        //         проводится по ЛЮБОЙ среде. Доступен обоим
        //         только когда ОБА доучились: финал части.
    }

    StandUser GetJoseph() => GetComponent<ResonanceSwap>().joseph;
}

[Serializable]
public class TechniqueProfile
{
    public TechniqueSchool native;
    public float foreignMastery;    // 0..1 — выучено от партнёра
    public TechniqueSchool ActiveCharge;   // чем сейчас заряжены удары
}

// Дизайн-смысл: игра НАГРАЖДАЕТ игру за обоих. Сидишь только в
// Джозефе — Фуми не растёт, и наоборот. Прокачка = поддержание
// отношений, которых «нет»: они никогда не встретятся, но каждый
// становится собой благодаря другому. Механика и есть сюжет.

// ------------------------------------------------------------
// 5. СБОРКА
// ------------------------------------------------------------
//
//  GoodVibrations.asset (HybridStandDefinition) — ОДИН ассет на двоих:
//   это буквально один стенд в двух мирах
//   ├─ dormant:  VineEmbodiment (в теле: хлысты из рук владельца)
//   ├─ default:  VineEmbodiment (собран: гуманоид, якоря, барраж)
//   └─ abilities: VineWhip/Anchor (в воплощении), заряды школ
//        через TechniqueProfile.ActiveCharge
//
//  Гейтинг части (всё из готовых тегов + проводимость):
//   Grapple  — хлыст-хук (наследие Hermit Purple)
//   Hamon-цепи — вода/живое: осветить, оживить, оглушить площадь
//   Spin-цепи  — металл/роторы: запустить механизмы, отпереть
//   Резонанс   — стена здесь / проход там; эхо-головоломки на пары
//   Сдвоенный заряд — финальные двери, требующие обеих школ
//
//  Что чувствует игрок:
//   • это метроидвания С ДВУМЯ КАРТАМИ НА ОДНОЙ ГЕОМЕТРИИ:
//     исследование = стереоскопия миров, тупик — приглашение
//     переключиться
//   • среда читается как схема: где вода — там Джозеф, где
//     металл — там Фуми, а лианы дотягивают заряд куда нужно
//   • прокачка партнёра через собственную игру: «яučусь — значит
//     он растёт». К финалу оба владеют обеими школами — и
//     сдвоенный заряд как механическое «мы всё-таки встретились»
//     в единственном месте, где могли: внутри стенда.
// ============================================================
