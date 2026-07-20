using UnityEngine;

/// <summary>
/// Воплощение стенда — КАК стенд существует в мире.
///
/// Разделение: StandController = сущность (данные, способности, боёвка,
/// внешний API), воплощение = тело (позиция, движение, видимость, поводок).
/// Контроллер не знает, есть ли у стенда тело вообще: Integrated-воплощение
/// (стенд-кожа) реализует те же методы пустышками.
///
/// Шаг 1 миграции: интерфейс введён, реализация одна — FollowerEmbodiment.
/// Остальные воплощения (Integrated, Autonomous, Deployable, Swarm, Overhead,
/// Vine, Unseen) добавляются рядом, не трогая контроллер.
/// </summary>
public interface IStandEmbodiment
{
    /// <summary>Привязать воплощение к телу. Вызывается один раз из Start().</summary>
    void Setup(StandBody body);

    /// <summary>Отвязать: погасить визуал, отпустить ссылки.</summary>
    void Teardown();

    /// <summary>Стенд призван (переход в Free/Automatic/SemiAuto).</summary>
    void OnSummon();

    /// <summary>Стенд отозван (переход в Attached).</summary>
    void OnDismiss();

    /// <summary>Логика по кадрам — таймеры, анимация. Из Update().</summary>
    void Tick(float dt);

    /// <summary>Физика — движение тела. Из FixedUpdate().</summary>
    void FixedTick(float dt);

    /// <summary>Дотягивание позиции после всех Update. Из LateUpdate().</summary>
    void LateTick(float dt);

    // ──────────────────────────────────────────
    // Намерения. Контроллер говорит ЧТО, воплощение решает КАК.
    // Для Integrated/Autonomous часть из них — no-op.
    // ──────────────────────────────────────────

    /// <summary>Ручное ведение к точке (hold ПКМ).</summary>
    void MoveTo(Vector2 worldPos);

    /// <summary>Ручное ведение отпущено.</summary>
    void StopMoving();

    /// <summary>
    /// Командный полёт: лететь в точку, по прибытии дёрнуть OnArrived.
    /// <paramref name="returnAfter"/> — вернуться за спину после действия.
    /// </summary>
    void CommandFlyTo(Vector2 worldPos, bool returnAfter);

    /// <summary>Повернуться лицом к точке по горизонтали.</summary>
    void FaceTowards(float worldX);

    /// <summary>Прибытие командного полёта — контроллер решает, что делать на месте.</summary>
    event System.Action OnArrived;

    /// <summary>Может ли игрок вести это воплощение напрямую (Integrated → false).</summary>
    bool SupportsDirectControl { get; }

    /// <summary>Текущая позиция тела в мире.</summary>
    Vector2 Position { get; }

    /// <summary>Тело сейчас движется к цели.</summary>
    bool IsMoving { get; }
}
