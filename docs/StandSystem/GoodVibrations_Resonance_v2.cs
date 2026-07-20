// ============================================================
//  GOOD VIBRATIONS — РЕЗОНАНС v2 (правки Влада)
//
//  Три степени связи миров:
//   0. ТЕЛО НЕ РЕЗОНИРУЕТ. Джозеф и Фуми идут каждый по своему
//      миру со своей позицией. Стена Фуми остаётся стеной Фуми,
//      сколько бы Джозеф ни гулял за ней. Переключается только
//      УПРАВЛЕНИЕ.
//   1. ТЕХНИКА РЕЗОНИРУЕТ ПОЛНОСТЬЮ — через стенд. Парный
//      механизм, решённый одной школой в одном мире, стенд
//      воспроизводит во втором В ТОЧНОСТИ как это сделал партнёр:
//      Фуми крутанул спин — у Джозефа лианы Good Vibrations
//      повторяют его движение, хоть Джозеф спина не знает.
//   2. ОСТАЛЬНОЕ РЕЗОНИРУЕТ СУДЬБОЙ. Нетехнические парные
//      препятствия открываются во втором мире через УДАЧНОЕ
//      СОВПАДЕНИЕ: кто-то вышиб дверь с той стороны, телега
//      снесла засов, порыв ветра. Не магия — вероятность,
//      переписанная стендом. Совпадение всегда ДИЕГЕТИЧНО.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ДВА ПАРАЛЛЕЛЬНЫХ АВАТАРА: переключается управление
// ------------------------------------------------------------

public class ResonanceControl : MonoBehaviour
{
    public StandUser joseph;    // живёт в JosephWorld, позиция своя
    public StandUser fumi;      // живёт в FumiWorld, позиция своя
    public WorldSide Active { get; private set; } = WorldSide.JosephWorld;

    public StandUser ActiveUser   => Active == WorldSide.JosephWorld ? joseph : fumi;
    public StandUser InactiveUser => Active == WorldSide.JosephWorld ? fumi : joseph;

    public float swapCooldown = 1.0f;
    float _cd;

    public void TrySwap()
    {
        if (_cd > 0) return;
        _cd = swapCooldown;

        Active = Active == WorldSide.JosephWorld ? WorldSide.FumiWorld : WorldSide.JosephWorld;
        // ПОЗИЦИИ НЕ ТРОГАЕМ: каждый стоит там, где его оставили.
        WorldLayers.Instance.SetActiveSide(Active);       // рендер/камера/коллизии
        InactiveUser.EnterIdleMode();                     // см. ниже
        ActiveUser.ExitIdleMode();

        // Камера перелетает к позиции нового активного — и этот
        // перелёт сам по себе рассказывает, насколько миры
        // разошлись или совпали в маршруте. Дешёвая нарративная
        // деталь: чем ближе точки — тем сильнее резонанс сейчас.
    }

    void Update() => _cd -= Time.deltaTime;
}

// Неактивный партнёр: миры работают ОДНОВРЕМЕННО, но игра не
// должна наказывать за переключение. Дизайн-ручка (выбери):
//  A. Безопасная пауза: неактивный мир замирает для угроз
//     (враги теряют к партнёру интерес — «его мир расфокусирован»)
//  B. Автоопека стенда: лианы пассивно отводят удары от
//     неактивного (лорно: стенд-то ОДИН на двоих, он всегда при обоих)
//  C. Жёсткий режим для арен: особые ко-оп бои, где партнёра
//     нельзя бросать надолго — таймер давления
// Рекомендация: B как база (красиво лорно), C точечно в боссах.
public partial class StandUser
{
    public void EnterIdleMode() { /* стенд прикрывает, AI-уклонение */ }
    public void ExitIdleMode() { }
}

// ------------------------------------------------------------
// 2. ПАРНЫЕ ОБЪЕКТЫ: три вида эха
// ------------------------------------------------------------

public enum EchoKind
{
    None,               // стены, обрывы, ландшафт: тело не резонирует
    TechniqueReplay,    // механизмы школ: стенд повторяет технику партнёра
    FateCoincidence     // нейтральные: судьбоносное совпадение
}

public class ResonantPair : MonoBehaviour
{
    public ResonantPair counterpart;
    public EchoKind echo = EchoKind.None;

    [Header("TechniqueReplay")]
    public TechniqueSchool requiredSchool;      // чем открывается механизм

    [Header("FateCoincidence")]
    public List<CoincidenceEvent> coincidencePool;  // чем «повезёт» близнецу
    public float coincidenceDelay = 2.5f;           // судьба не мгновенна

    bool _resolved;

    // Вызывается, когда АКТИВНЫЙ игрок решает препятствие штатно
    public void ResolveByPlayer(StandUser who, TechniqueRecord record = null)
    {
        if (_resolved) return;
        _resolved = true;
        Open();

        switch (echo)
        {
            case EchoKind.None:
                break;  // близнец остаётся закрыт: иди решай сам

            case EchoKind.TechniqueReplay:
                // Стенд ЗАПОМНИЛ, как партнёр это сделал, и
                // воспроизводит у близнеца — лианами, в точности
                counterpart?.ResolveByStandReplay(record);
                break;

            case EchoKind.FateCoincidence:
                counterpart?.ScheduleCoincidence();
                break;
        }
    }

    // --- Эхо 1: стенд-исполнитель ---
    public void ResolveByStandReplay(TechniqueRecord record)
    {
        if (_resolved) return;
        _resolved = true;
        // Сцена: лианы Good Vibrations выползают из тела владельца,
        // оплетают механизм и ПОВТОРЯЮТ движение партнёра — тот же
        // ритм, тот же нажим. Владелец смотрит на руки стенда,
        // делающие то, чего он не умеет. Обучение (TechniqueLink)
        // капает и отсюда: наблюдение за «своими» руками — тоже урок.
        StartCoroutine(PlayReplay(record));
    }

    System.Collections.IEnumerator PlayReplay(TechniqueRecord r)
    {
        VineReplayFX.Play(transform.position, r);
        yield return new WaitForSeconds(r?.duration ?? 1.5f);
        Open();
        TechniqueLinkStatic.GrantObservationXP(this, r);
    }

    // --- Эхо 2: судьба ---
    public void ScheduleCoincidence()
    {
        if (_resolved) return;
        Invoke(nameof(FireCoincidence), coincidenceDelay);
    }

    void FireCoincidence()
    {
        if (_resolved) return;
        _resolved = true;
        // Выбираем совпадение из пула ЭТОЙ пары — оно всегда
        // диегетично и поставлено руками, не «дверь мигнула»:
        var ev = coincidencePool[UnityEngine.Random.Range(0, coincidencePool.Count)];
        ev.Play(this);   // телега сносит засов; прохожий вышибает
                         // дверь С ТОЙ СТОРОНЫ; ржавая петля сдаётся
                         // под порывом ветра — и Open() в конце сцены
    }

    void Open() { /* физически открыть/сдвинуть/включить */ }
}

// Совпадение как маленькая постановка (ассет на каждую пару):
[CreateAssetMenu(menuName = "JoJo/Resonance/Coincidence Event")]
public class CoincidenceEvent : ScriptableObject
{
    public string description;      // «пьяный извозчик снёс ворота»
    public GameObject scenePrefab;  // акторы сценки: телега, прохожий...
    public AudioClip sfx;

    public void Play(ResonantPair at)
    {
        UnityEngine.Object.Instantiate(scenePrefab, at.transform.position, Quaternion.identity);
        // Сценка сама доигрывает и дёргает at.Open() по завершении.
        // Игрок ВИДИТ причину: мир объясним, просто невероятен.
    }
}

// ------------------------------------------------------------
// 3. ЗАПИСЬ ТЕХНИКИ: что именно стенд «выучил» для повтора
// ------------------------------------------------------------

// Когда игрок решает механизм школой, мы фиксируем НЕ факт,
// а ИСПОЛНЕНИЕ — чтобы у близнеца повтор был «в точности как Фуми»:
[Serializable]
public class TechniqueRecord
{
    public TechniqueSchool school;
    public float duration;
    public List<Vector2> gesture;      // траектория/ритм вращения или дыхания
    public float intensity;
}

// PlayerCombat/механизм при решении собирают record и передают в
// ResolveByPlayer. Это же красиво стыкуется с мини-играми школ:
// если спин-механизм — это мини-игра вращения стиком, то record —
// буквально запись того, КАК игрок её прошёл, и лианы у близнеца
// зеркалят его собственное движение. Игрок узнаёт СВОЙ почерк.

// ------------------------------------------------------------
// 4. СЛЕДСТВИЯ ДЛЯ ДИЗАЙНА УРОВНЕЙ
// ------------------------------------------------------------
//
//  • EchoKind.None — все стены, пропасти, ландшафт: маршруты
//    расходятся по-настоящему. Уровень = две сплетающиеся нити,
//    а не одна с фильтром. Точки, где позиции сходятся —
//    драматические узлы («мы стоим в одном месте»).
//  • TechniqueReplay — школьные механизмы: спин-ворот, хамон-росток.
//    Правило честности: повтор доступен, только если пара УЖЕ
//    решена партнёром. Нельзя «попросить» стенд сделать наперёд.
//  • FateCoincidence — сюжетные двери, завалы, мосты: судьба
//    помогает второму, но с задержкой и всегда с ПРИЧИНОЙ в кадре.
//    Пул совпадений на пару — ручная работа, это мини-сценки,
//    и именно они продают тему стенда лучше любого туториала.
//  • Баланс переключений держится сам: тело не резонирует, значит
//    оба маршрута надо ПРОЙТИ; техника резонирует, значит скучных
//    повторов головоломок нет; судьба резонирует со зрелищем,
//    значит каждый «подарок» мира — событие, а не галочка.
// ============================================================
