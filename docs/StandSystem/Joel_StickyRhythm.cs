// ============================================================
//  СТЕНД ДЖОЭЛЯ ДЖОЙСА — девятый стресс-тест.
//  Новинка: СИЛА СТЕНДА = КАЧЕСТВО ИСПОЛНЕНИЯ ИГРОКА.
//
//  Механика Влада:
//   • пока Джоэль ОТБИВАЕТ РИТМ, стенд создаёт липкие мембраны
//     и запускает их вперёд
//   • каждая мембрана ТОНКАЯ и рвётся — поэтому нужен ритм:
//     поток многих мембран, наслаивающихся друг на друга
//   • мембрана коснулась поверхности ИЛИ связала два предмета →
//     СКЛЕИВАНИЕ
//   • мембрана обняла одиночную цель без второй точки контакта →
//     цель ВЫРЫВАЕТСЯ (вопрос времени и слоёв)
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. РИТМ-ДВИЖОК: сердце способности
// ------------------------------------------------------------

public class RhythmEngine : MonoBehaviour
{
    public float bpm = 100f;                 // темп трека уровня/боя
    public float PerfectWindow = 0.06f;      // сек от бита
    public float GoodWindow = 0.13f;

    public int Streak { get; private set; }
    public event Action<BeatQuality> OnBeatHit;

    float BeatInterval => 60f / bpm;
    float _songTime;

    void Update() => _songTime += Time.deltaTime;

    // Джоэль жмёт кнопку ритма:
    public BeatQuality Tap()
    {
        float toNearest = Mathf.Abs(
            _songTime % BeatInterval is var m && m > BeatInterval / 2
                ? m - BeatInterval : m);

        BeatQuality q =
            toNearest <= PerfectWindow ? BeatQuality.Perfect :
            toNearest <= GoodWindow    ? BeatQuality.Good :
                                         BeatQuality.Miss;

        Streak = q == BeatQuality.Miss ? 0 : Streak + 1;
        OnBeatHit?.Invoke(q);
        return q;
    }
}

public enum BeatQuality { Miss, Good, Perfect }

// Музыкальный слой: бит СЛЫШЕН в саундтреке (перкуссия проступает,
// когда стенд активен) и ВИДЕН в анимации Джоэля — он реально
// отбивает такт ногой/пальцами. Ритм-подсказка диегетична,
// без интерфейсных полосок: 2016-й, парень с плеером. Панк-метод.

// ------------------------------------------------------------
// 2. МЕМБРАНА: тонкая, летит, липнет
// ------------------------------------------------------------

public class Membrane : MonoBehaviour
{
    public float speed = 11f;
    public int layerPower = 1;              // Perfect-бит даёт 2 (плотнее)
    Vector2 _dir;
    bool _stuck;

    public void Launch(Vector2 from, Vector2 dir, int power)
    {
        transform.position = from;
        _dir = dir; layerPower = power;
    }

    void Update()
    {
        if (_stuck) return;
        transform.position += (Vector3)(_dir * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D hit)
    {
        if (_stuck) return;
        _stuck = true;

        // ПРАВИЛО КОНТАКТА:
        // ищем, есть ли ВТОРАЯ точка — поверхность или предмет рядом
        var second = FindSecondContact(hit);

        if (second != null)
            GlueBond.AddLayers(hit.transform, second, layerPower, transform.position);
        else if (hit.TryGetComponent<MembraneWrappable>(out var target))
            target.AddWrap(layerPower);      // одиночка: обёрнут, будет рваться
        // мимо всего → мембрана оседает на землю липкой кляксой
        //   (зона замедления на пару секунд — мелкий бонус промаха)

        Destroy(gameObject, 0.1f);
    }

    Transform FindSecondContact(Collider2D first)
    {
        var near = Physics2D.OverlapCircleAll(transform.position, 0.6f);
        foreach (var c in near)
            if (c != first && (c.CompareTag("Surface") || c.CompareTag("Glueable")))
                return c.transform;
        return null;
    }
}

// ------------------------------------------------------------
// 3. СКЛЕЙКА: слои = прочность
// ------------------------------------------------------------

public class GlueBond : MonoBehaviour
{
    public int Layers { get; private set; }
    const float StrengthPerLayer = 40f;      // breakForce джойнта
    FixedJoint2D _joint;

    static readonly Dictionary<(Transform, Transform), GlueBond> _bonds = new();

    public static void AddLayers(Transform a, Transform b, int layers, Vector2 at)
    {
        var key = (a, b);
        if (!_bonds.TryGetValue(key, out var bond))
        {
            var go = new GameObject($"Glue_{a.name}_{b.name}");
            go.transform.position = at;
            bond = go.AddComponent<GlueBond>();
            bond.CreateJoint(a, b);
            _bonds[key] = bond;
        }
        bond.Layers += layers;
        bond._joint.breakForce = bond.Layers * StrengthPerLayer;
        // ВОТ ЗАЧЕМ РИТМ: одна мембрана (40) — враг разорвёт рывком.
        // Восемь тактов чистого ритма (320+) — держит подвесной мост.
        // Прочность склейки = музыкальность игрока, буквально.
    }

    void CreateJoint(Transform a, Transform b)
    {
        var rbA = a.GetComponent<Rigidbody2D>();
        if (rbA == null) return;             // поверхность-стена: якорим к миру
        _joint = a.gameObject.AddComponent<FixedJoint2D>();
        _joint.connectedBody = b.GetComponent<Rigidbody2D>();  // null = к миру
    }

    // Расклейка: перелом силы, растворитель... и Crazy Diamond:
    // «прежнее состояние» вещей — не склеенное. Восстановление
    // Джоске расторгает склейку Джоэля. Система сама подружила их.
    public void Dissolve() { _bonds.Clear(); Destroy(gameObject); }
}

// Обёрнутая одиночная цель: рвётся наружу
public class MembraneWrappable : MonoBehaviour
{
    public float escapePerSec = 1.4f;        // сила вырывания (у боссов выше)
    float _wrapLayers;

    public bool Restrained => _wrapLayers > 0;

    public void AddWrap(int layers) => _wrapLayers += layers;

    void Update()
    {
        if (_wrapLayers <= 0) return;
        _wrapLayers -= escapePerSec * Time.deltaTime;
        // Пока Restrained: не двигается / двигается еле-еле.
        // Джоэль ДОЛЖЕН продолжать ритм, чтобы обгонять вырывание —
        // связывание врага это не выстрел, а ПАРТИЯ, которую надо
        // доиграть. Либо вторая точка: прижать врага к СТЕНЕ и
        // клеить — тогда это Bond, и он держит по-настоящему.
        // Тактика сама учит: липкое любит поверхности.
    }
}

// ------------------------------------------------------------
// 4. СПОСОБНОСТЬ: связка ритма и стенда
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Sticky Rhythm")]
public class StickyRhythmDefinition : StandAbilityDefinition
{
    public Membrane membranePrefab;
    public override IStandAbility CreateRuntime(StandEntity o) => new StickyRhythmAbility(this, o);
}

public class StickyRhythmAbility : IStandAbility
{
    readonly StickyRhythmDefinition _def;
    readonly StandEntity _e;
    RhythmEngine _rhythm;

    public StickyRhythmAbility(StickyRhythmDefinition d, StandEntity e)
    {
        _def = d; _e = e;
        _rhythm = e.Owner.GetComponent<RhythmEngine>();
        _rhythm.OnBeatHit += OnBeat;
    }

    public AbilityTag Tags => AbilityTag.Bond;   // новый тег: гейтинг связей
    public bool CanActivate => true;

    public void Activate(in StandCommand cmd) => _rhythm.Tap();  // кнопка = такт

    void OnBeat(BeatQuality q)
    {
        if (q == BeatQuality.Miss)
        {
            // Сбой ритма: стенд «спотыкается» — полсекунды без мембран.
            // Наказание мягкое, но слышное: перкуссия в треке хромает.
            return;
        }
        var m = UnityEngine.Object.Instantiate(_def.membranePrefab);
        m.Launch(
            ((FollowerEmbodiment)_e.Embodiment).BodyPosition,
            _e.Owner.FacingDir,
            power: q == BeatQuality.Perfect ? 2 : 1);

        // Серии — украшения, не ломающие простоту:
        if (_rhythm.Streak % 8 == 0)   // каждый 8-й такт чистой серии
            m.layerPower += 1;          // «золотая» мембрана потолще
    }

    public void Tick(float dt) { }
}

// ------------------------------------------------------------
// 5. ЧТО ЭТО ДАЁТ ИГРЕ
// ------------------------------------------------------------
//
//  Бой:
//   • враг у стены → ритм-серия → приклеен (Bond держит)
//   • враг в поле → обёртка рвётся → его надо ЗАГНАТЬ к поверхности
//     или бросить в него предмет и клеить К ПРЕДМЕТУ. Позиционка!
//   • склеить врага С ВРАГОМ — двое возятся, разрывая узы
//  Головоломки/гейтинг (Bond):
//   • мембранный мост: наклеить слоёв между уступами, пока
//     держит вес — и ПЕРЕБЕЖАТЬ, пока стык не порвался
//   • склеить ящик с движущейся платформой — уехал груз
//   • заклеить сопло/клапан/звонок: N слоёв глушат механизм
//   • приклеить СЕБЯ к стене (потолку?) — панк-стелс под потолком
//  Кросс-система:
//   • Crazy Diamond расторгает склейки (прежнее состояние!)
//   • у Джорджа: склеенная дверь = «удачное состояние» недоступно
//   • ритм-движок переиспользуем: хамон-дыхание Джонатана тоже
//     можно вести тактом — общий музыкальный скелет игры
//
//  Дополнения к базе: AbilityTag + Bond (1<<13)
// ============================================================
