// ============================================================
//  СТЕНД ДЖОРДЖА — ВЕРСИЯ 2 (правки Влада)
//
//  Изменения против v1:
//   1. УДЕРЖАНИЕ вместо мгновенного щелчка: стенд входит в предмет,
//      становится ПЕРВЫМ НАБЛЮДАТЕЛЕМ удачного состояния и держит
//      его, пока другой наблюдатель не ЗАФИКСИРУЕТ его в реальность
//      (или пока удача не "выстрелит" при использовании).
//   2. ЁМКОСТЬ: стенд один — он распределён по удерживаемым точкам.
//      Ресурс = сколько позиций он может держать одновременно.
//   3. РАЗДЕЛЕНИЕ ЛОРА: врождённая удача Джостаров работает на
//      неизвестное (пассив, без стенда); стенд — инструмент,
//      привязанный к восприятию Джорджа (правило памяти).
//   4. Передвижение = скрытность: стенд ползёт к цели через
//      непросматриваемое пространство ("взгляд-стены" остаётся,
//      но теперь читается как "он крадётся").
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. УДЕРЖАНИЕ: стенд внутри предмета
// ------------------------------------------------------------

public enum HoldOutcome
{
    Fired,      // удача сработала при использовании (дверь толкнули — открыта,
                // курок нажали — осечка). Стенд освобождается, эффект случился.
    Fixed,      // другой наблюдатель увидел предмет: состояние ЗАФИКСИРОВАНО
                // в реальность навсегда. Стенд освобождается. "Всегда так было."
    Abandoned   // стенд отозван (нужен в другом месте) ДО фиксации:
                // состояние РАССЛАБЛЯЕТСЯ обратно — никто не наблюдал,
                // значит, ничего и не было.
}

public class StandHold
{
    public QuantumMutable Target;
    public string HeldStateId;
    public string PreviousStateId;
    public float StartedAt;

    public event Action<StandHold, HoldOutcome> OnReleased;

    public void Release(HoldOutcome outcome)
    {
        if (outcome == HoldOutcome.Abandoned)
            Target.RevertTo(PreviousStateId);       // не наблюдалось = не было
        else
            Target.Commit(HeldStateId);             // стало реальностью

        Target.CurrentHold = null;
        OnReleased?.Invoke(this, outcome);
    }
}

// ------------------------------------------------------------
// 2. ОБНОВЛЁННЫЙ QuantumMutable
// ------------------------------------------------------------

public class QuantumMutable : MonoBehaviour
{
    public List<MutableState> states;
    public string currentStateId;
    public StandHold CurrentHold;               // стенд сейчас внутри?

    public bool IsHeld => CurrentHold != null;

    // Стенд входит и удерживает (вызывается мозгом)
    public bool TryHold(string toId, out StandHold hold)
    {
        hold = null;
        if (IsHeld || toId == currentStateId) return false;
        if (ObservationRegistry.Instance.IsObserved(transform.position)) return false;

        hold = new StandHold
        {
            Target = this,
            HeldStateId = toId,
            PreviousStateId = currentStateId,
            StartedAt = Time.time
        };
        CurrentHold = hold;
        currentStateId = toId;
        ApplyStateToWorld(toId);
        SetStandPresenceFX(true);   // см. п.5 — жутковатый tell для игрока
        return true;
    }

    // Предмет ИСПОЛЬЗОВАЛИ, пока стенд держал: удача выстреливает
    public void OnInteracted()
    {
        if (!IsHeld) return;
        SetStandPresenceFX(false);
        CurrentHold.Release(HoldOutcome.Fired);
    }

    // Каждый кадр (или из ObservationRegistry событием): чужой взгляд
    void Update()
    {
        if (!IsHeld) return;
        if (ObservationRegistry.Instance.IsObservedByOther(
                transform.position, except: ObserverId.GeorgeStand))
        {
            SetStandPresenceFX(false);
            CurrentHold.Release(HoldOutcome.Fixed);   // реальность подтверждена
        }
    }

    public void RevertTo(string id) { currentStateId = id; ApplyStateToWorld(id); }
    public void Commit(string id)   { /* состояние теперь обычное, стенда нет */ }

    void ApplyStateToWorld(string id) { }
    void SetStandPresenceFX(bool on) { }
}

// ВАЖНЫЙ ДИЗАЙН-ВОПРОС (переключатель, реши для манги):
// фиксирует ли ВЗГЛЯД САМОГО ДЖОРДЖА?
//   true  → Джордж, посмотрев на подготовленную дверь, "обналичивает"
//           её сам: удобно, управляемо, но менее драматично
//   false → Джордж должен ВСЛЕПУЮ верить, что удача на месте, до
//           момента использования: вера в свою удачу как механика
public static class GeorgeGazeRule
{
    public const bool GeorgeGazeFixes = false;  // драматичный вариант
}

// ------------------------------------------------------------
// 3. ЁМКОСТЬ: стенд распределён по точкам удержания
// ------------------------------------------------------------

public class HoldCapacity
{
    public int Max = 3;                     // растёт с развитием Джорджа
    readonly List<StandHold> _active = new();

    public int Free => Max - _active.Count;
    public IReadOnlyList<StandHold> Active => _active;

    public void Add(StandHold h)
    {
        _active.Add(h);
        h.OnReleased += (hold, _) => _active.Remove(hold);
    }

    // Мозгу нужно место? Бросить самое старое НЕзафиксированное:
    public void AbandonOldest()
    {
        if (_active.Count == 0) return;
        _active[0].Release(HoldOutcome.Abandoned);
    }
}

// Ёмкость ЗАМЕНЯЕТ шкалу удачи из v1 как основной ресурс — она
// осязаемее: игрок видит "3 слота", а не абстрактный процент.
// Невероятность состояния можно оставить как ВРЕМЯ ВХОДА:
// невероятная позиция requires стенду "уговаривать" предмет дольше.

// ------------------------------------------------------------
// 4. МОЗГ v2: держать, перебрасывать, красться
// ------------------------------------------------------------

public class GeorgeStandBrainV2 : MonoBehaviour
{
    StandEntity _e;
    GeorgeMemory _memory;
    UnobservedPropagation _sneak;       // тот же BFS: путь крадущегося стенда
    HoldCapacity _capacity;
    float _thinkTimer;

    public void Bind(StandEntity e)
    {
        _e = e;
        _memory = e.Owner.GetComponent<GeorgeMemory>();
        _sneak = new UnobservedPropagation(rangeMeters: 14f);
        _capacity = new HoldCapacity();
    }

    void Update()
    {
        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer > 0) return;
        _thinkTimer = 0.5f;

        FavorTag need = ReadNeed();

        // Есть ли уже удержание под текущую нужду? Не суетимся.
        foreach (var h in _capacity.Active)
            if ((h.Target.Get(h.HeldStateId).favors & need) != 0) return;

        // Ищем цель: память + не наблюдается + достижима крадучись
        var pick = FindBest(need);
        if (pick.target == null) return;

        // Нет свободного слота → перебросить стенд с самой старой позиции.
        // Лорно: стенд ВЫХОДИТ из предмета (состояние расслабляется,
        // ведь его никто не зафиксировал) и КРАДЁТСЯ к новой цели.
        if (_capacity.Free == 0) _capacity.AbandonOldest();

        if (pick.target.TryHold(pick.stateId, out var hold))
            _capacity.Add(hold);
    }

    (QuantumMutable target, string stateId) FindBest(FavorTag need)
    {
        QuantumMutable best = null; string bestState = null; float bestScore = 0;
        foreach (var q in _memory.Known)
        {
            if (q.IsHeld) continue;
            if (ObservationRegistry.Instance.IsObserved(q.transform.position)) continue;
            if (!_sneak.CanReach(_e.Owner.transform.position, q.transform.position)) continue;

            foreach (var s in q.states)
            {
                if (s.id == q.currentStateId || (s.favors & need) == 0) continue;
                float dist = Vector2.Distance(_e.Owner.transform.position, q.transform.position);
                float score = 10f / (1f + dist * 0.1f + s.improbability * 2f);
                if (score > bestScore) { bestScore = score; best = q; bestState = s.id; }
            }
        }
        return (best, bestState);
    }

    FavorTag ReadNeed()
    {
        var o = _e.Owner;
        if (o.Combat.InDangerFrom != null) return FavorTag.Disarms | FavorTag.Protects;
        if (o.Movement.IsBlockedByGate)    return FavorTag.OpensPath;
        if (o.Health01 < 0.3f)             return FavorTag.Protects | FavorTag.Provides;
        return FavorTag.Provides | FavorTag.Distracts;
    }
}

// ------------------------------------------------------------
// 5. ЧИТАЕМОСТЬ: стенд В предмете — это видно (но только чутьём)
// ------------------------------------------------------------
//
//  Вместо мерцания задним числом (v1) — ПРИСУТСТВИЕ:
//   • предмет с удержанием едва заметно "дышит" мотивом стенда
//     (для игрока — например, лёгкая рябь в стиле его дизайна)
//   • Джордж может ЧУВСТВОВАТЬ свои удержания через стены — HUD
//     показывает 3 слота и куда они вложены. Это честно: это ЕГО стенд.
//   • враги не видят ничего... кроме особых стендюзеров-сенсоров —
//     готовый контр-приём для манги: враг, который видит, ГДЕ
//     разложена удача Джорджа, и нарочно "фиксирует" её взглядом
//     в невыгодные моменты или пасёт предметы, чтобы стенд не вошёл.
//
// ------------------------------------------------------------
// 6. ВРОЖДЁННАЯ УДАЧА ДЖОСТАРОВ: отдельный пассив, БЕЗ стенда
// ------------------------------------------------------------

// Лор: удача рода работает на НЕИЗВЕСТНОЕ — то, чего Джордж не видел
// и о чём не знает. Стенд туда не дотягивается (правило памяти),
// а кровь — да. Механически: редкие фоновые благоприятные броски
// на событиях, которые генерируются вне поля зрения игрока.
public class JoestarBloodline : MonoBehaviour
{
    [Range(0f, 1f)] public float luckWeight = 0.15f;

    // Вызывается системами МИРА в момент скрытой генерации:
    // содержимое невскрытого сундука, патрульный маршрут за экраном,
    // выпадение редкого предмета, "успел ли враг перезарядить"
    public T Bias<T>(IList<(T option, float favor)> options)
    {
        // Смещаем рулетку к благоприятным исходам на luckWeight.
        // НЕ гарантия — склонность. Джостары удачливы, не всесильны.
        float total = 0;
        foreach (var o in options) total += 1f + o.favor * luckWeight * 10f;
        float roll = UnityEngine.Random.value * total, acc = 0;
        foreach (var o in options)
        {
            acc += 1f + o.favor * luckWeight * 10f;
            if (roll <= acc) return o.option;
        }
        return options[options.Count - 1].option;
    }
}

// Итоговая картина силы Джорджа — двухслойная, как ты и описал:
//   КРОВЬ  → неизвестное, фоновая склонность мира, неуправляема
//   СТЕНД  → известное (память), точечное удержание позиций,
//            управляется косвенно: восприятием, взглядами, нуждой
//
// ------------------------------------------------------------
// Заглушки/дополнения
// ------------------------------------------------------------
public enum ObserverId { GeorgeStand, George, PlayerCamera, Enemy }
// ObservationRegistry: + IsObservedByOther(Vector2, ObserverId except)
//   — стенд-внутри-предмета сам наблюдатель, себя не фиксирует
