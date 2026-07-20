// ============================================================
//  UNDER PRESSURE / ДЖОРДЖ ДЖОСТАР II — ВЕРСИЯ 4 (финальная)
//
//  Уточнения Влада:
//   1. Стенд ВИДИМ и НЕ исчезает под взглядом — он ЗАМИРАЕТ,
//      когда движется к цели (эффект Зенона: наблюдение
//      останавливает эволюцию системы). Рядом с Джорджем летает
//      как обычный follower.
//   2. РЕЖИМ ДАЛЬНЕЙ ОТПРАВКИ: стенд целиком уносится до 20 м
//      за ~1 сек, на месте РАСЩЕПЛЯЕТСЯ на части и работает со
//      средой (ломает оружие, открывает двери). Цена: Джордж
//      остаётся БЕЗ защиты стенда.
//   3. HEY YOU (финальная битва): весь стенд прячется в одной
//      пуле; попадание вгоняет ЦЕЛЬ в суперпозицию — она
//      временно перестаёт существовать.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ДВА РЕЖИМА ВОПЛОЩЕНИЯ (та же пара, что у Джолин, но своя)
// ------------------------------------------------------------

// Режим A: ОХРАННИК — follower при Джордже.
// Обычные кулаки (скромные: стенд не боец), блок, и главное —
// части в резерве дают фоновую удачу ближнего боя (v3 §2).
public class UnderPressureFollower : FollowerEmbodiment
{
    public UnderPressureFollower(StandEntity e) : base(e) { }
    // Наследует всё; поводок короткий (2м). Заморозка Зенона тут
    // НЕ действует: при владельце стенд "дома", а не "в пути".
}

// Режим B: ДАЛЬНЯЯ ОПЕРАЦИЯ — якорь на расстоянии + рой частей.
public class UnderPressureRemote : IStandEmbodiment
{
    readonly StandEntity _e;
    public Vector2 Anchor { get; private set; }        // куда отправлен
    public const float MaxRange = 20f;
    public const float DashTime = 1f;

    GameObject _body;               // видимое тело стенда у якоря
    StandPartPool _parts;
    PartNavigator _nav;
    bool _dashing;
    float _dashT;
    Vector2 _dashFrom;

    public bool SupportsDirectControl => true;  // фокус двигает якорь!

    public UnderPressureRemote(StandEntity e, StandPartPool parts, PartNavigator nav)
    { _e = e; _parts = parts; _nav = nav; }

    public void Setup()
    {
        _body = UnityEngine.Object.Instantiate(_e.Definition.embodimentPrefab);
    }
    public void Teardown() { if (_body) UnityEngine.Object.Destroy(_body); }

    public void OnSummon() { }
    public void OnDismiss() { }

    // Отправка: вызывается при переключении режима
    public void DispatchTo(Vector2 point)
    {
        Anchor = Vector2.ClampMagnitude(point - (Vector2)_e.Owner.transform.position, MaxRange)
                 + (Vector2)_e.Owner.transform.position;
        _dashFrom = _e.Owner.transform.position;
        _dashing = true; _dashT = 0;
    }

    public void Tick(float dt)
    {
        if (_dashing)
        {
            // РЫВОК — это тоже "движение к цели": Зенон действует!
            // Взгляд на летящий стенд замораживает его В ВОЗДУХЕ.
            if (ObservationRegistry.Instance.IsObserved(_body.transform.position))
            {
                // застыл — жуткая картина: силуэт висит посреди зала,
                // пока на него смотрят. Отвели взгляд — долетел.
                return;
            }
            _dashT += dt / DashTime;
            _body.transform.position = Vector2.Lerp(_dashFrom, Anchor, _dashT);
            if (_dashT >= 1f) { _dashing = false; SplitIntoParts(); }
            return;
        }

        // На якоре: части работают вокруг него (радиус операции ~6м).
        foreach (var p in _parts.All) _nav.Tick(p, dt);

        // ГЛАВНАЯ ЦЕНА РЕЖИМА: Джордж без охраны.
        _e.Owner.Combat.SetStandGuard(false);
        _e.Owner.RemoveMeleeLuckPassive();      // резервных частей нет — все в деле
    }

    void SplitIntoParts()
    {
        foreach (var p in _parts.All)
        {
            p.Position = Anchor;
            p.Phase = PartPhase.Sneaking;       // мозг раздаст цели вокруг якоря
        }
        _body.GetComponent<StandBodyView>()?.PlaySplitFX(); // тело "рассыпается"
    }

    public void Execute(in StandCommand cmd)
    {
        switch (cmd.type)
        {
            case StandCommandType.Move:
                // Фокус стенда: вести якорь по карте (пере-отправка короткими
                // рывками; каждый рывок снова уязвим для Зенона)
                if (!_dashing) DispatchTo(Anchor + cmd.direction * 4f);
                break;
            case StandCommandType.HeavyAttack:
                // Прямой приказ среде у якоря: сломать оружие/механизм.
                // Механически: ближайшему QuantumMutable у якоря
                // назначается приоритетная цель с FavorTag.Disarms
                RemoteInteract(FavorTag.Disarms);
                break;
            case StandCommandType.UseAbility:
                RemoteInteract(FavorTag.OpensPath);   // открыть двери и т.п.
                break;
            case StandCommandType.Recall:
                RecallAll();
                break;
        }
    }

    void RemoteInteract(FavorTag intent)
        => ((GeorgeStandBrainV3)_e.Owner.Brain).ForceNeedAt(Anchor, intent);

    void RecallAll()
    {
        foreach (var p in _parts.All)
            if (p.Phase == PartPhase.Holding && p.Target != null)
                p.Target.CurrentHold.Release(HoldOutcome.Abandoned);
        _e.SetEmbodiment(EmbodimentKind.Follower);   // домой, в охранники
        _e.Owner.Combat.SetStandGuard(true);
        _e.Owner.RestoreMeleeLuckPassive();
    }
}

// Переключение режимов — одна кнопка с прицелом:
//  удержал → появился указатель до 20м → отпустил → Dispatch.
//  Recall — та же кнопка коротко.

// ------------------------------------------------------------
// 2. ЗЕНОН КАК ОБЩЕЕ ПРАВИЛО ДВИЖЕНИЯ
// ------------------------------------------------------------
//
//  Единая формулировка (для кода и для манги):
//  «Under Pressure не может ИЗМЕНЯТЬСЯ, пока наблюдаем.
//   Движение к цели — изменение. Удержание позиции — нет.»
//
//   • крадущаяся часть под взглядом  → Frozen (уже было в v3)
//   • рывок целого стенда под взглядом → висит в воздухе
//   • стенд при Джордже               → не "в пути", свободен
//   • часть внутри предмета/пули      → не наблюдаема по определению
//
//  Заметь: это то же правило, что фиксация состояний, только с
//  другой стороны. Наблюдение ОСТАНАВЛИВАЕТ возможности: у предмета
//  коллапсирует состояние, у стенда — движение. Одна физика на всё.
//
// ------------------------------------------------------------
// 3. HEY YOU — ультимативная способность (финальная битва)
// ------------------------------------------------------------

[CreateAssetMenu(menuName = "JoJo/Abilities/Hey You")]
public class HeyYouDefinition : StandAbilityDefinition
{
    public float superpositionDuration = 6f;
    public bool unlocked;                        // сюжетный флаг финала
    public override IStandAbility CreateRuntime(StandEntity o) => new HeyYouAbility(this, o);
}

public class HeyYouAbility : IStandAbility
{
    readonly HeyYouDefinition _def;
    readonly StandEntity _e;
    float _cooldownLeft;

    public HeyYouAbility(HeyYouDefinition d, StandEntity e) { _def = d; _e = e; }

    public AbilityTag Tags => AbilityTag.None;

    // Требования максимальны — это козырь, не приём:
    //  все части ДОМА (стенд должен уместиться в пулю ЦЕЛИКОМ),
    //  патрон есть, режим Follower, способность открыта сюжетом
    public bool CanActivate =>
        _def.unlocked
        && _cooldownLeft <= 0
        && _e.Embodiment is UnderPressureFollower
        && _e.Owner.Ammo.Current > 0
        && AllPartsHome();

    bool AllPartsHome()
    {
        foreach (var p in ((GeorgeStandBrainV3)_e.Owner.Brain).Pool.All)
            if (p.Phase != PartPhase.InReserve) return false;
        return true;
    }

    public void Activate(in StandCommand cmd)
    {
        if (!CanActivate || !_e.Owner.Ammo.TrySpend(1)) return;
        _cooldownLeft = _def.cooldown;

        // Весь стенд прячется в пуле. Пока она летит, Джордж
        // АБСОЛЮТНО без стенда — даже фоновой удачи нет.
        _e.Owner.Combat.SetStandGuard(false);
        _e.Owner.RemoveMeleeLuckPassive();

        var bullet = ProjectilePool.Spawn("HeyYouBullet",
            _e.Owner.transform.position, cmd.direction);

        bullet.OnHit = hitObj =>
        {
            RestoreStand();
            var target = hitObj.GetComponent<SuperposableEntity>();
            if (target == null) return;   // промах по несуперпозируемому

            // ЦЕЛЬ ВХОДИТ В СУПЕРПОЗИЦИЮ: временно не существует.
            target.EnterSuperposition(_def.superpositionDuration);
        };
        bullet.OnMiss = RestoreStand;
    }

    void RestoreStand()
    {
        _e.Owner.Combat.SetStandGuard(true);
        _e.Owner.RestoreMeleeLuckPassive();
    }

    public void Tick(float dt) => _cooldownLeft -= dt;
}

// Состояние "не существует": ни видеть, ни бить, ни быть побитым
public class SuperposableEntity : MonoBehaviour
{
    public bool InSuperposition { get; private set; }
    Collider2D[] _colliders;
    Renderer[] _renderers;
    IObserver2D _asObserver;         // если цель сама наблюдатель...

    public void EnterSuperposition(float duration)
    {
        InSuperposition = true;
        _colliders = GetComponentsInChildren<Collider2D>();
        _renderers = GetComponentsInChildren<Renderer>();
        foreach (var c in _colliders) c.enabled = false;
        foreach (var r in _renderers) r.enabled = false;

        // Критично: суперпозированный НЕ наблюдатель. Его конус
        // зрения выключается → карта взглядов дышит свободнее,
        // все замершие части стенда отмирают. Убрал наблюдателя
        // из мира — освободил свою же скрытность. Финальный ход
        // работает на ВСЮ систему сразу.
        if (TryGetComponent<VisionCone>(out var eyes)) eyes.blinded = true;

        Invoke(nameof(Collapse), duration);
    }

    void Collapse()
    {
        InSuperposition = false;
        foreach (var c in _colliders) c.enabled = true;
        foreach (var r in _renderers) r.enabled = true;
        if (TryGetComponent<VisionCone>(out var eyes)) eyes.blinded = false;
        // Возвращается ТАМ ЖЕ: мир "вспоминает" его. За 6 секунд
        // Джордж перестроил доску: удержания разложены, позиция
        // занята, ствол наведён на точку возврата. Шах.
    }
}

// ------------------------------------------------------------
// 4. ИТОГОВАЯ КАРТА СИЛЫ ДЖОРДЖА
// ------------------------------------------------------------
//
//  UnderPressure.asset (HybridStandDefinition)
//   ├─ defaultEmbodiment: Follower (охранник, 2м)
//   ├─ remote: UnderPressureRemote (якорь ≤20м, рывок 1с, Зенон)
//   ├─ части: 3 → 5 по развитию; резервные = фоновая удача боя
//   └─ abilities: ChargedShot (доставка пулей), HeyYou (финал)
//
//  Слои силы:
//   КРОВЬ   — пассив на неизвестное (JoestarBloodline, вне стенда)
//   ЧАСТИ   — точечные удержания известного (память! вход без глаз!)
//   ЦЕЛИКОМ — дальняя операция: сила там, беззащитность здесь
//   HEY YOU — суперпозиция цели: козырь, требующий собрать ВСЁ
//             в одну пулю и на 6 секунд поверить в удачу до конца
//
//  Один принцип на всём пути: наблюдение останавливает возможность.
//  Джордж побеждает не силой — управлением тем, кто куда смотрит.
// ============================================================
