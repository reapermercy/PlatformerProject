// ============================================================
//  СТЕНД ДЖОРДЖА — ВЕРСИЯ 3 (финальная архитектура)
//
//  Решения Влада:
//   1. ФИКСАЦИЯ АСИММЕТРИЧНА: любой взгляд (включая Джорджа)
//      подтверждает удержанное состояние в реальность. Джордж
//      фиксирует УВЕРЕННО и добровольно; враг, посмотрев,
//      СОБСТВЕННОРУЧНО подтверждает удачу Джорджа.
//      → Контр-игра врага: не дать стенду ВОЙТИ (профилактика),
//        а не смотреть после (это проигрыш).
//   2. СТЕНД СОСТОИТ ИЗ ЧАСТЕЙ. Джордж управляет только тем,
//      СКОЛЬКО частей отправить. Цели и маршруты — сам стенд.
//   3. ДОСТАВКА ДВУМЯ ПУТЯМИ:
//      • крадучись — медленно, бесплатно, нужны тьма/слепые зоны
//      • ПУЛЕЙ ИЗ РЕВОЛЬВЕРА — мгновенно: часть прячется ВНУТРИ
//        пули (движущееся укрытие) и пролетает сквозь
//        просматриваемое пространство. Цена: патрон + ГРОХОТ,
//        который сам поворачивает взгляды.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ЧАСТЬ СТЕНДА: маленький агент со своим жизненным циклом
// ------------------------------------------------------------

public enum PartPhase
{
    InReserve,   // при Джордже (внутри него — он тоже укрытие!)
    Sneaking,    // ползёт к цели через непросматриваемые ячейки
    Frozen,      // путь перекрыло взглядом — часть замерла в укрытии
    Holding,     // сидит в предмете, удерживает состояние
    Returning    // отозвана / после Fired-Fixed возвращается
}

public class StandPart
{
    public int Id;
    public PartPhase Phase = PartPhase.InReserve;
    public Vector2 Position;              // где часть физически "есть"
    public QuantumMutable Target;
    public string DesiredStateId;
    public Queue<Vector2> Path = new();   // маршрут по ячейкам BFS
    public float SneakSpeed = 3.5f;       // медленнее бегущего человека!
}

// ------------------------------------------------------------
// 2. ПУЛ ЧАСТЕЙ: единственный рычаг прямого управления Джорджа
// ------------------------------------------------------------

public class StandPartPool
{
    public int TotalParts = 3;            // растёт с развитием
    public int DeployLimit { get; private set; } = 3;

    readonly List<StandPart> _parts = new();

    public StandPartPool()
    {
        for (int i = 0; i < TotalParts; i++)
            _parts.Add(new StandPart { Id = i });
    }

    public IEnumerable<StandPart> All => _parts;
    public IEnumerable<StandPart> Deployed
    {
        get { foreach (var p in _parts) if (p.Phase != PartPhase.InReserve) yield return p; }
    }

    public StandPart NextFree()
    {
        int deployed = 0;
        foreach (var p in _parts) if (p.Phase != PartPhase.InReserve) deployed++;
        if (deployed >= DeployLimit) return null;
        foreach (var p in _parts) if (p.Phase == PartPhase.InReserve) return p;
        return null;
    }

    // ДЖОРДЖ РЕШАЕТ ТОЛЬКО ЭТО (колесо/D-pad): сколько частей
    // стенду разрешено держать снаружи. 0 = "все ко мне" (отзыв).
    public void SetDeployLimit(int n)
    {
        DeployLimit = Mathf.Clamp(n, 0, TotalParts);
        // превышение → мозг отзывает самые бесполезные (см. Recall в мозге)
    }
}

// Дизайн-смысл лимита: чем больше частей снаружи, тем "жиже" сам
// стенд при Джордже. Можно привязать пассивку: части InReserve дают
// Джорджу мелкую фоновую удачу в ближнем бою (уклоны, спотыкания
// врагов рядом). Разложил всё по карте — сам ходишь "голый".

// ------------------------------------------------------------
// 3. ПЕРЕДВИЖЕНИЕ ЧАСТИ: скрытность как физика
// ------------------------------------------------------------

public class PartNavigator
{
    readonly UnobservedPropagation _grid;   // тот же BFS: даёт ПУТЬ, не только да/нет

    public PartNavigator(UnobservedPropagation grid) { _grid = grid; }

    public void Tick(StandPart part, float dt)
    {
        switch (part.Phase)
        {
            case PartPhase.Sneaking:
            case PartPhase.Returning:
                if (part.Path.Count == 0) { Arrive(part); return; }

                Vector2 next = part.Path.Peek();
                // Взгляд упал на следующую ячейку? Часть ЗАМИРАЕТ в текущем
                // укрытии и ждёт (или мозг перестроит маршрут):
                if (ObservationRegistry.Instance.IsObserved(next))
                {
                    part.Phase = PartPhase.Frozen;
                    return;
                }
                part.Position = Vector2.MoveTowards(part.Position, next, part.SneakSpeed * dt);
                if (Vector2.Distance(part.Position, next) < 0.05f) part.Path.Dequeue();
                break;

            case PartPhase.Frozen:
                // Путь снова темен? Продолжаем красться
                if (part.Path.Count == 0 || !ObservationRegistry.Instance.IsObserved(part.Path.Peek()))
                    part.Phase = part.Target != null ? PartPhase.Sneaking : PartPhase.Returning;
                break;
        }
    }

    void Arrive(StandPart part)
    {
        if (part.Phase == PartPhase.Returning) { part.Phase = PartPhase.InReserve; return; }

        // Дошли до цели: вход требует НЕнаблюдения (это правило входа,
        // оно не менялось) — если предмет под взглядом, ждём у порога
        if (part.Target != null && part.Target.TryHold(part.DesiredStateId, out var hold))
        {
            part.Phase = PartPhase.Holding;
            hold.OnReleased += (h, outcome) =>
            {
                part.Target = null;
                part.Phase = PartPhase.Returning;
                // мозг проложит обратный маршрут; после Fixed/Fired часть
                // может красться домой уже неторопливо
            };
        }
        else part.Phase = PartPhase.Frozen;   // караулит у предмета момент
    }
}

// ------------------------------------------------------------
// 4. РЕВОЛЬВЕР: баллистическая доставка
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Stand-Charged Shot")]
public class ChargedShotDefinition : StandAbilityDefinition
{
    public float noiseRadius = 12f;      // грохот: враги в радиусе ПОВОРАЧИВАЮТСЯ
    public override IStandAbility CreateRuntime(StandEntity o) => new ChargedShotAbility(this, o);
}

public class ChargedShotAbility : IStandAbility
{
    readonly ChargedShotDefinition _def;
    readonly StandEntity _e;

    public ChargedShotAbility(ChargedShotDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.None;

    public bool CanActivate =>
        _e.Owner.Ammo.Current > 0
        && ((GeorgeStandBrainV3)_e.Owner.Brain).Pool.NextFree() != null;

    public void Activate(in StandCommand cmd)
    {
        var brain = (GeorgeStandBrainV3)_e.Owner.Brain;
        var part = brain.Pool.NextFree();
        if (part == null || !_e.Owner.Ammo.TrySpend(1)) return;

        // Часть прячется В ПУЛЕ: пока летит, стенд невидим —
        // движущееся укрытие пересекает любые взгляды.
        var bullet = ProjectilePool.Spawn("ChargedBullet",
            _e.Owner.transform.position, cmd.direction);
        part.Phase = PartPhase.Sneaking;              // формально "в пути"
        part.Path.Clear();                            // маршрут не нужен

        bullet.OnHit = hitObj =>
        {
            part.Position = bullet.Position;
            var q = hitObj.GetComponent<QuantumMutable>();
            if (q != null && !q.IsHeld)
            {
                // Мгновенная попытка входа. Тонкость: предмет, в который
                // ЦЕЛИЛИСЬ, обычно под взглядом Джорджа! Пуля прилетает
                // раньше, чем "фиксация входа" — лорно: часть успевает
                // юркнуть внутрь В МОМЕНТ удара, пока пыль/искры на долю
                // секунды прячут предмет от всех глаз. Механически:
                // TryHold с однокадровым иммунитетом к наблюдению.
                part.DesiredStateId = brain.PickStateFor(q);
                if (q.TryHoldIgnoringObservationOnce(part.DesiredStateId, out var hold))
                {
                    part.Target = q; part.Phase = PartPhase.Holding;
                    hold.OnReleased += (h, _) => { part.Target = null; part.Phase = PartPhase.Returning; };
                }
                else part.Phase = PartPhase.Returning;
            }
            else part.Phase = PartPhase.Returning;    // промах — часть ползёт домой
        };

        // ЦЕНА: грохот. Все конусы зрения в радиусе разворачиваются
        // на звук → карта наблюдения ПЕРЕКРАИВАЕТСЯ. Выстрел может
        // разом заморозить всех крадущихся частей — или наоборот,
        // увести взгляды из нужного коридора. Оружие и отмычка сразу.
        NoiseSystem.Emit(_e.Owner.transform.position, _def.noiseRadius);
    }

    public void Tick(float dt) { }
}

// ------------------------------------------------------------
// 5. ФИКСАЦИЯ v3: асимметрия
// ------------------------------------------------------------
//
//  Правила (все взгляды равны, последствия — нет):
//   • ВХОД: часть входит в предмет только БЕЗ наблюдателей
//     (однокадровое исключение пули — искры как микро-укрытие)
//   • УДЕРЖАНИЕ: любой взгляд на удерживаемый предмет → Fixed:
//     состояние подтверждено в реальность НАВСЕГДА, часть свободна
//   • Джордж фиксирует ДОБРОВОЛЬНО: посмотрел на подготовленную
//     дверь — "обналичил" её, уверенность как жест
//   • Враг, посмотрев, подтверждает УДАЧУ ДЖОРДЖА — и знает это.
//     Отсюда паранойя противника: НЕ смотреть туда, где мог
//     побывать стенд, и не спускать глаз с того, где он НЕ был.
//     Взгляд врага из оружия превратился в обязательство.
//
//  Контр-приёмы врага (материал для арок манги):
//   • тотальный надзор ДО: свет, зеркала, посты — стенд не войдёт
//   • сенсор, видящий ЧАСТИ в пути → охота на крадущихся
//   • навязанный выбор: враг создаёт ситуацию, где Джорджу ВЫГОДНО
//     не смотреть (фиксация раньше времени портит план), и бьёт
//     по слепоте его же уверенности
//
// ------------------------------------------------------------
// 6. HUD ДЖОРДЖА
// ------------------------------------------------------------
//
//   [●][●][○]  ← части: две снаружи (одна Holding, одна Sneaking),
//                одна в резерве. Лимит выставлен колесом на 2.
//   Кажд. часть: иконка фазы + силуэт цели. Frozen мигает —
//   "мой стенд замер, кто-то смотрит на его дорогу".
//   Патроны револьвера отдельно: заряженный выстрел подсвечен,
//   если есть свободная часть.
//
// ------------------------------------------------------------
// Заглушки/дополнения
// ------------------------------------------------------------
public class AmmoPool { public int Current = 6; public bool TrySpend(int n) { if (Current < n) return false; Current -= n; return true; } }
public static class NoiseSystem { public static void Emit(Vector2 at, float radius) { } }
// Projectile: Position, OnHit(GameObject)
// QuantumMutable: + TryHoldIgnoringObservationOnce(...)
// StandUser: + Ammo (AmmoPool), + Brain
// UnobservedPropagation: CanReach → FindPath(from, to, out Queue<Vector2>)
// GeorgeStandBrainV3: Pool (StandPartPool), PickStateFor(QuantumMutable),
//   Update() = ReadNeed → назначить цели свободным частям в пределах
//   DeployLimit → PartNavigator.Tick по всем частям
