// ============================================================
//  STAR PLATINUM / ДЖОТАРО — десятый стресс-тест.
//  Переделка Влада: остановка времени должна ЧУВСТВОВАТЬСЯ
//  в развитии с самого начала.
//
//  Решение: СП умеет ровно ОДНО — останавливать. Растёт только
//  КАТЕГОРИЯ остановленного:
//
//   Ступень 1. ПРЕДМЕТ    — «Хватка»: поймать и заморозить вещь
//   Ступень 2. СЕБЯ       — «Пульс»: остановить СВОЁ время
//                            восприятия (мир замедлился для тебя)
//   Ступень 3. ЧУЖОЕ МГНОВЕНИЕ — «Украденная секунда»: идеальное
//                            парирование замораживает врага
//   Ступень 4. МИР        — «The World»: предел последовательности
//
//  Механически это ОДИН API с расширяющейся областью действия.
//  Игрок, оглянувшись из финала, видит: он всегда играл в
//  остановку времени — просто маленькую.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ЕДИНАЯ ВЛАСТЬ НАД ВРЕМЕНЕМ: один API на все ступени
// ------------------------------------------------------------

public class TimeAuthority : MonoBehaviour
{
    public static TimeAuthority Instance;

    // --- Ступень 1: заморозить ОДИН объект ---
    public void FreezeObject(Rigidbody2D body, float duration)
    {
        StartCoroutine(FreezeRoutine(body, duration));
    }

    System.Collections.IEnumerator FreezeRoutine(Rigidbody2D body, float dur)
    {
        var savedVel = body.linearVelocity;
        var savedAng = body.angularVelocity;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.linearVelocity = Vector2.zero;
        // предмет ВИСИТ: пуля в воздухе, падающая балка над головой
        yield return new WaitForSeconds(dur);
        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = savedVel;      // импульс ВОЗВРАЩАЕТСЯ —
        body.angularVelocity = savedAng;     // канонические ножи ДИО!
    }

    // --- Ступень 2: замедлить мир ОТНОСИТЕЛЬНО владельца ---
    public void PerceptionDilation(float worldScale, float realSeconds, StandUser exempt)
    {
        // Time.timeScale вниз, а exempt получает компенсацию:
        // его аниматор/движение умножаются на 1/worldScale.
        // Для игрока: мир как в смоле, Джотаро — обычный.
        StartCoroutine(DilationRoutine(worldScale, realSeconds, exempt));
    }

    // --- Ступень 3: заморозить ОДНО СУЩЕСТВО ---
    public void FreezeEntity(GameObject target, float duration)
    {
        // аниматор пауза, AI пауза, физика kinematic, хитбоксы живы
        // (замороженного МОЖНО бить — окно наказания)
        StartCoroutine(EntityFreezeRoutine(target, duration));
    }

    // --- Ступень 4: заморозить МИР ---
    public void FreezeWorld(float duration, StandUser exempt)
    {
        // всё, кроме exempt (и других властителей времени — задел
        // на зеркального босса): физика, AI, снаряды, частицы.
        // Предметы, сдвинутые ВО ВРЕМЯ остановки, получают импульс
        // при возобновлении — FreezeObject.savedVel в мировом масштабе.
        StartCoroutine(WorldFreezeRoutine(duration, exempt));
    }

    System.Collections.IEnumerator DilationRoutine(float s, float t, StandUser e) { yield break; }
    System.Collections.IEnumerator EntityFreezeRoutine(GameObject g, float d) { yield break; }
    System.Collections.IEnumerator WorldFreezeRoutine(float d, StandUser e) { yield break; }
}

// Обрати внимание: четыре метода — это одна идея с разным scope:
//   объект → сам → существо → всё.
// Сюжетное «он пришёл к остановке времени» = игрок прошёл
// лестницу вызовов одного и того же API. Форма и есть содержание.

// ------------------------------------------------------------
// 2. СТУПЕНЬ 1: «ХВАТКА» — с первого часа игры
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Absolute Grip")]
public class AbsoluteGripDefinition : StandAbilityDefinition
{
    public float freezeDuration = 3f;
    public override IStandAbility CreateRuntime(StandEntity o) => new AbsoluteGripAbility(this, o);
}

public class AbsoluteGripAbility : IStandAbility
{
    readonly AbsoluteGripDefinition _def;
    readonly StandEntity _e;
    float _cd;

    public AbsoluteGripAbility(AbsoluteGripDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.Halt;    // новый тег гейтинга:
                                                  // остановить лезвия-ловушки,
                                                  // замереть платформу под прыжок
    public bool CanActivate => _cd <= 0;

    public void Activate(in StandCommand cmd)
    {
        // Кадр-визитка канона: СП ловит ПУЛЮ. Перехват снаряда в
        // радиусе стенда → снаряд ВИСИТ. Второе нажатие — вернуть
        // отправителю (savedVel уже хранит его импульс: разворот знака).
        var hit = Physics2D.OverlapCircle(StandPos(), 1.6f,
            LayerMask.GetMask("Projectile", "FallingHazard", "Mechanism"));
        if (hit == null || !hit.attachedRigidbody) return;

        TimeAuthority.Instance.FreezeObject(hit.attachedRigidbody, _def.freezeDuration);
        _cd = _def.cooldown;
        // Игрок с ПЕРВОГО боя учится главному глаголу Джотаро:
        // «это — остановилось, потому что я так сказал».
    }

    public void Tick(float dt) => _cd -= dt;
    Vector2 StandPos() => ((FollowerEmbodiment)_e.Embodiment).BodyPosition;
}

// ------------------------------------------------------------
// 3. СТУПЕНЬ 2: «ПУЛЬС» — остановить себя (восприятие)
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Heartbeat Focus")]
public class HeartbeatFocusDefinition : StandAbilityDefinition
{
    public float worldScale = 0.25f;      // мир в 4 раза медленнее
    public float realSeconds = 2.5f;
    public float meterCost = 40f;
    public override IStandAbility CreateRuntime(StandEntity o) => new HeartbeatFocusAbility(this, o);
}

public class HeartbeatFocusAbility : IStandAbility
{
    readonly HeartbeatFocusDefinition _def;
    readonly StandEntity _e;

    public HeartbeatFocusAbility(HeartbeatFocusDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.None;
    public bool CanActivate => _e.Owner.FocusMeter.Current >= _def.meterCost;

    public void Activate(in StandCommand cmd)
    {
        if (!_e.Owner.FocusMeter.TrySpend(_def.meterCost)) return;
        TimeAuthority.Instance.PerceptionDilation(_def.worldScale, _def.realSeconds, _e.Owner);
        // Лорная подача: СП не ускоряется — он ВЫХОДИТ из общего
        // времени. Знаменитая «точность» СП — это Джотаро, живущий
        // в паузах между чужими секундами. Ступень к The World:
        // сначала научись покидать время В ОДИНОЧКУ.
    }

    public void Tick(float dt) { }
}

// ------------------------------------------------------------
// 4. СТУПЕНЬ 3: «УКРАДЕННАЯ СЕКУНДА» — чужое мгновение
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Stolen Second")]
public class StolenSecondDefinition : StandAbilityDefinition
{
    public float parryWindow = 0.15f;
    public float freezeDuration = 1.2f;
    public override IStandAbility CreateRuntime(StandEntity o) => new StolenSecondAbility(this, o);
}

public class StolenSecondAbility : IStandAbility, IDamageInterceptor
{
    readonly StolenSecondDefinition _def;
    readonly StandEntity _e;
    float _window;

    public StolenSecondAbility(StolenSecondDefinition d, StandEntity e)
    {
        _def = d; _e = e;
        e.Owner.Combat.RegisterInterceptor(this);
    }

    public AbilityTag Tags => AbilityTag.None;
    public bool CanActivate => true;

    public void Activate(in StandCommand cmd) => _window = _def.parryWindow;

    // Идеальное парирование: вражеская атака входит в окно →
    // ВРАГ ЗАМОРОЖЕН на секунду. Не оглушён — именно заморожен:
    // ни анимации, ни гравитации, застыл в замахе. Джотаро
    // впервые останавливает не вещь, а ЖИВОЕ. Механически это
    // тренировка таймингов The World: каждое парирование —
    // репетиция финала.
    public bool TryIntercept(ref DamageInfo dmg)
    {
        if (_window <= 0) return false;
        _window = 0;
        if (dmg.source != null)
            TimeAuthority.Instance.FreezeEntity(dmg.source, _def.freezeDuration);
        return true;   // урон отменён
    }

    public void Tick(float dt) => _window -= dt;
}

// ------------------------------------------------------------
// 5. СТУПЕНЬ 4: «THE WORLD» — предел
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Star Platinum The World")]
public class SPTheWorldDefinition : StandAbilityDefinition
{
    public float baseDuration = 1.0f;       // первая остановка — ОДНА секунда
    public float maxDuration = 5.0f;        // растёт использованием/сюжетом
    public override IStandAbility CreateRuntime(StandEntity o) => new SPTheWorldAbility(this, o);
}

public class SPTheWorldAbility : IStandAbility
{
    readonly SPTheWorldDefinition _def;
    readonly StandEntity _e;
    public float CurrentDuration { get; private set; }
    float _cd;

    public SPTheWorldAbility(SPTheWorldDefinition d, StandEntity e)
    { _def = d; _e = e; CurrentDuration = d.baseDuration; }

    public AbilityTag Tags => AbilityTag.TimeControl;
    public bool CanActivate => _cd <= 0;

    public void Activate(in StandCommand cmd)
    {
        if (!CanActivate) return;
        _cd = _def.cooldown;
        TimeAuthority.Instance.FreezeWorld(CurrentDuration, _e.Owner);
        // Рост: как мышца — от 1 сек к 5 через использование.
        CurrentDuration = Mathf.Min(_def.maxDuration, CurrentDuration + 0.15f);
    }

    public void Tick(float dt) => _cd -= dt;
}

// ------------------------------------------------------------
// 6. ДРАМАТУРГИЯ ОТКРЫТИЯ (предложение)
// ------------------------------------------------------------
//
//  Финальный босс — властитель времени (зеркало ДИО). Он
//  останавливает мир, Джотаро заморожен... но:
//   ступень 1 научила его: остановленное ХРАНИТ импульс
//   ступень 2 научила его: можно ЖИТЬ вне общего времени
//   ступень 3 научила его: чужое время можно КРАСТЬ
//  В остановленном мире босса Джотаро сначала двигает ПАЛЕЦ
//  (одна ступень), потом руку (вторая), потом делает шаг
//  (третья) — и мир останавливается уже ЕГО волей. Игрок
//  проживает открытие The World как экзамен по трём предыдущим
//  главам, а не как кат-сцену. Способность заработана.
//
//  Гейтинг по ступеням:
//   Halt        — замереть лезвие/платформу/поток (ранняя игра)
//   Focus       — «медленные» головоломки на реакцию (середина)
//   TimeControl — двери финала, зоны «пройди за остановку»
//
//  Дополнения к базе: AbilityTag + Halt (1<<14);
//  DamageInfo + source (GameObject); StandUser + FocusMeter;
//  старый TimeStopManager из первого файла упраздняется в пользу
//  TimeAuthority — теперь время едино для всей игры (и готово
//  к боссам-властителям: exempt-список вместо жёсткого игрока).
// ============================================================
