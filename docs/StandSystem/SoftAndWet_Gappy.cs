// ============================================================
//  SOFT & WET / ГАППИ — двенадцатый, финальный стресс-тест.
//  Стенд, чья способность = архитектура нашей системы:
//  свойства мира — это компоненты, и их МОЖНО УКРАСТЬ.
//
//   • лопающийся пузырь ОТНИМАЕТ свойство у цели
//   • украденное хранится в пузыре и ПЕРЕНОСИТСЯ: лопни его
//     на другой цели — свойство приживётся там
//   • пузыри умеют и просто ВЗРЫВАТЬСЯ (боевой вариант)
//   • GO BEYOND: пузырь, вращением ИСЧЕЗНУВШИЙ из реальности —
//     не отнимает свойство, а ЛИШАЕТ КАК ТАКОВОГО. Дырка.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. СЛОВАРЬ СВОЙСТВ: чем мир владеет — то можно отнять
// ------------------------------------------------------------

// Всё, что мы строили весь день, стало добычей:
public enum StealableKind
{
    Friction,       // пол → каток (канон!); враг → не может стоять
    Sound,          // шаги → тишина (стелс); механизм → нем;
                    //   а у ДЖОЭЛЯ звук — это РИТМ: украсть звук
                    //   у его боя = отнять оружие. Кросс-контра!
    Sight,          // враг слеп... а для СТЕНДА ДЖОРДЖА слепой —
                    //   не наблюдатель: кража зрения = подарок удаче
    Moisture,       // вода из лужи → сухой проход; и Хамон-проводка
                    //   Джозефа ГЛОХНЕТ без влаги. Свойства связаны!
    Heat,           // огонь холоден → пройти; двигатель встал
    Light,          // лампа至 темнота — дорога частям Under Pressure
    Weight,         // тяжёлое летает: мост-плита как плот
    Smell,          // след пропал — змеи Джорно теряют дорогу
    Stickiness,     // отнять липкость у мембран Джоэля...
    Momentum        // у снаряда — висит (эхо Хватки Джотаро)
}

// Свойство как компонент: система, честно знающая о себе
public class StealableProperty : MonoBehaviour
{
    public StealableKind kind;
    public bool stolen;

    public event Action OnStolen;
    public event Action OnRestored;

    public void Steal()   { stolen = true;  OnStolen?.Invoke();  Apply(); }
    public void Restore() { stolen = false; OnRestored?.Invoke(); Apply(); }

    void Apply()
    {
        // каждая пара вид+носитель знает, что значит «без этого»:
        // Friction у пола → PhysicsMaterial скользкий
        // Sound у существа → шаги/крики не создают NoiseSystem-событий
        // Sight у врага → VisionCone.blinded (реестр Джорджа замечает!)
        // Moisture у лужи → Conduit.material больше не Water
        // Momentum у снаряда → TimeAuthority.FreezeObject навсегда
    }
}

// ------------------------------------------------------------
// 2. ПУЗЫРЬ: вор, курьер и бомба в одном
// ------------------------------------------------------------

public enum BubbleMode { Steal, Carry, Blast }

public class SoftWetBubble : MonoBehaviour
{
    public BubbleMode mode = BubbleMode.Steal;
    public StealableKind? payload;          // украденное внутри (Carry)
    public StealableProperty payloadSource; // откуда украдено — для возврата
    public float speed = 8f;
    Vector2 _dir;

    public void Launch(Vector2 from, Vector2 dir)
    { transform.position = from; _dir = dir; }

    void Update() => transform.position += (Vector3)(_dir * speed * Time.deltaTime);

    void OnTriggerEnter2D(Collider2D hit)
    {
        switch (mode)
        {
            case BubbleMode.Steal:
                var prop = PickProperty(hit);
                if (prop != null)
                {
                    prop.Steal();
                    // пузырь НЕ лопается — он теперь несёт добычу
                    mode = BubbleMode.Carry;
                    payload = prop.kind; payloadSource = prop;
                    ReturnToOrbit();        // кружит вокруг Гаппи слотом
                }
                break;

            case BubbleMode.Carry:
                // Лопнут на новой цели → свойство ПРИЖИВАЕТСЯ там:
                // трение пола — на лёд (дорога!), звук — на немой
                // механизм (заработал), вес — на лёгкую платформу.
                TransplantTo(hit);
                Pop();
                break;

            case BubbleMode.Blast:
                Explode();                  // просто бой: урон+отброс
                break;
        }
    }

    StealableProperty PickProperty(Collider2D c)
        => c.GetComponent<StealableProperty>();   // при нескольких — выбор
                                                  // колесом перед выстрелом
    void TransplantTo(Collider2D c) { /* AddComponent-пересадка */ }
    void ReturnToOrbit() { }
    void Pop() { payloadSource = null; Destroy(gameObject); }
    void Explode() { Destroy(gameObject); }
}

// Экономика: у Гаппи 3-5 орбитальных слотов. Каждый занятый слот —
// украденное свойство при себе. ВЕРНУТЬ свойство (лопнуть пузырь
// на источнике) — тоже ход: мир без трения/света/звука не должен
// оставаться таким навсегда, и головоломки строятся в обе стороны:
// сначала укради, чтобы пройти, потом верни, чтобы прошло другое.

// ------------------------------------------------------------
// 3. СПОСОБНОСТЬ И ГЕЙТИНГ
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Plunder Bubble")]
public class PlunderBubbleDefinition : StandAbilityDefinition
{
    public SoftWetBubble bubblePrefab;
    public int orbitSlots = 4;
    public override IStandAbility CreateRuntime(StandEntity o) => new PlunderBubbleAbility(this, o);
}

public class PlunderBubbleAbility : IStandAbility
{
    readonly PlunderBubbleDefinition _def;
    readonly StandEntity _e;
    readonly List<SoftWetBubble> _orbit = new();

    public PlunderBubbleAbility(PlunderBubbleDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Plunder;   // финальный тег словаря
    public bool CanActivate => _orbit.Count < _def.orbitSlots;

    public void Activate(in StandCommand cmd)
    {
        var b = UnityEngine.Object.Instantiate(_def.bubblePrefab);
        b.mode = cmd.abilityIndex == 1 ? BubbleMode.Blast : BubbleMode.Steal;
        b.Launch(((FollowerEmbodiment)_e.Embodiment).BodyPosition, cmd.direction);
    }

    public void Tick(float dt) { }
}

// ------------------------------------------------------------
// 4. GO BEYOND: не отнять свойство — отнять «есть»
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Go Beyond")]
public class GoBeyondDefinition : StandAbilityDefinition
{
    public bool unlocked;                   // финал части
    public override IStandAbility CreateRuntime(StandEntity o) => new GoBeyondAbility(this, o);
}

public class GoBeyondAbility : IStandAbility
{
    readonly GoBeyondDefinition _def;
    readonly StandEntity _e;
    float _cd;

    public GoBeyondAbility(GoBeyondDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.None;
    public bool CanActivate => _def.unlocked && _cd <= 0
        && _e.Owner.Technique?.SpinMastery > 0.5f;   // ВРАЩЕНИЕ: тот самый
                                                     // Spin из мира Фуми —
                                                     // теология техник сомкнулась

    public void Activate(in StandCommand cmd)
    {
        _cd = _def.cooldown;
        // Пузырь, ИСЧЕЗНУВШИЙ ИЗ РЕАЛЬНОСТИ за счёт вращения:
        //  • невидим и НЕвзаимодействуем: не блок, не стенд-защита,
        //    не заморозка времени — его просто НЕТ в мире, пока летит
        //  • для системы: не Collider — чистая траектория,
        //    проверяемая только в момент «прибытия»
        var path = new GhostTrajectory(
            ((FollowerEmbodiment)_e.Embodiment).BodyPosition, cmd.direction);
        path.OnArrive += MakeHole;
    }

    void MakeHole(GameObject target, Vector2 at)
    {
        // НЕ кража свойства. ЛИШЕНИЕ КАК ТАКОВОЕ:
        if (target.TryGetComponent<StealableProperty>(out _)) { }
        // — у стены: сегмент ИСЧЕЗ. Остаётся ДЫРА — и дыра это
        //   ОБЪЕКТ: постоянный проход, новый элемент уровня
        // — у врага/босса: часть его больше не существует.
        //   Не хил-бар: отнятое не лечится, не чинится
        //   (даже Crazy Diamond: «прежнее состояние» ЧЕГО?
        //    восстанавливать нечего — вот предел Джоске,
        //    прописанный столкновением двух способностей)
        // — у снаряда/способности: она не «отменена» (GER)
        //   и не «заморожена» (Джотаро) — её не стало
        HoleEntity.Create(at, target);
    }

    public void Tick(float dt) => _cd -= dt;
}

public class HoleEntity : MonoBehaviour
{
    // Дырка как постоянный житель мира: край, за который можно
    // схватиться; окно, через которое видно; рана на боссе,
    // которая НЕ закроется. Go Beyond не наносит урон —
    // он редактирует список существующего.
    public static void Create(Vector2 at, GameObject victim) { }
}

// ------------------------------------------------------------
// 5. ПОЧЕМУ ГАППИ — ПРАВИЛЬНЫЙ ФИНАЛ
// ------------------------------------------------------------
//
//  Иерархия отрицаний, к которой пришла вся игра:
//   Джотаро останавливает   (свойство «движется» — на паузе)
//   Джоске возвращает       (свойства — к прежним значениям)
//   GER отменяет            (причина свойства — вспять)
//   Гаппи ОТНИМАЕТ          (свойство — изъято и переносимо)
//   Go Beyond ЛИШАЕТ        (носителя свойств больше нет)
//
//  Каждый следующий — более глубокий уровень «нет». Финальный
//  протагонист играет против самого словаря игры, и все
//  двенадцать частей оказываются главами одной книги о том,
//  сколькими способами воля может сказать миру «нет» — и
//  сколькими сказать «да»: свет Джонатана, удача Джорджа,
//  резонанс Джозефов, ритм Джоэля, жизнь Джорно.
//
//  Дополнения к базе: AbilityTag + Plunder (1<<16);
//  StealableProperty вешается на всё, чем мир уже владел;
//  GhostTrajectory — незарегистрированная в физике линия
// ============================================================
