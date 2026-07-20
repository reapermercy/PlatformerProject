// ============================================================
//  STAND SYSTEM — скелет архитектуры
//  Принцип: Стенд = Сущность (данные/логика) + Воплощение (тело в мире)
//  Ни один класс не знает про конкретного персонажа.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------
// 1. ДАННЫЕ: описание стенда как ассет (ScriptableObject)
// ------------------------------------------------------------

public enum StandStatGrade { E, D, C, B, A, Infinite, None }

[CreateAssetMenu(menuName = "JoJo/Stand Definition")]
public class StandDefinition : ScriptableObject
{
    [Header("Идентичность")]
    public string standName;          // "Crimson Tide", "Sh-Boom"...
    public Sprite portrait;

    [Header("Статы (как в манге)")]
    public StandStatGrade power;
    public StandStatGrade speed;
    public StandStatGrade range;
    public StandStatGrade durability;
    public StandStatGrade precision;
    public StandStatGrade potential;

    [Header("Воплощение")]
    public EmbodimentKind defaultEmbodiment;
    public GameObject embodimentPrefab;   // тело призрака / юнит роя / турель; null для integrated

    [Header("Способности")]
    public List<StandAbilityDefinition> abilities; // модули, см. ниже

    [Header("Параметры связи")]
    public float leashRange = 4f;     // дистанция от владельца (0 = не ограничен, автономный)
    public bool sharesDamage = true;  // урон стенду = урон владельцу
}

public enum EmbodimentKind { Follower, Integrated, Autonomous, Deployable, Swarm }

// ------------------------------------------------------------
// 2. СУЩНОСТЬ: живой стенд в рантайме. НЕ MonoBehaviour у тела,
//    а компонент на владельце. Позиции в мире не имеет.
// ------------------------------------------------------------

public class StandEntity : MonoBehaviour
{
    public StandDefinition Definition { get; private set; }
    public StandUser Owner { get; private set; }
    public IStandEmbodiment Embodiment { get; private set; }

    public bool IsSummoned { get; private set; }
    public event Action<bool> OnSummonChanged;

    readonly List<IStandAbility> _abilities = new();

    public void Initialize(StandDefinition def, StandUser owner)
    {
        Definition = def;
        Owner = owner;

        foreach (var abilityDef in def.abilities)
            _abilities.Add(abilityDef.CreateRuntime(this));

        SetEmbodiment(def.defaultEmbodiment);
    }

    // Смена воплощения В РАНТАЙМЕ — акты Echoes, режимы боя/головоломки
    public void SetEmbodiment(EmbodimentKind kind)
    {
        Embodiment?.Teardown();
        Embodiment = EmbodimentFactory.Create(kind, this);
        Embodiment.Setup();
    }

    public void Summon()
    {
        if (IsSummoned) return;
        IsSummoned = true;
        Embodiment.OnSummon();
        OnSummonChanged?.Invoke(true);
    }

    public void Dismiss()
    {
        if (!IsSummoned) return;
        IsSummoned = false;
        Embodiment.OnDismiss();
        OnSummonChanged?.Invoke(false);
    }

    // Единая точка входа команд — от инпута ИЛИ от AI-мозга.
    // Сущность не знает, кто прислал команду и как её исполнит тело.
    public void IssueCommand(in StandCommand cmd)
    {
        if (!IsSummoned && cmd.type != StandCommandType.Summon) return;
        Embodiment.Execute(cmd);
    }

    public bool HasAbilityTag(AbilityTag tag)
    {
        foreach (var a in _abilities)
            if (a.Tags.HasFlag(tag)) return true;
        return false;
    }

    public T GetAbility<T>() where T : class, IStandAbility
    {
        foreach (var a in _abilities)
            if (a is T typed) return typed;
        return null;
    }

    void Update()
    {
        Embodiment?.Tick(Time.deltaTime);
        foreach (var a in _abilities) a.Tick(Time.deltaTime);
    }
}

// Команды — общий язык между инпутом, AI и телом
public enum StandCommandType { Summon, Dismiss, Move, LightAttack, HeavyAttack, Barrage, UseAbility, Deploy, Recall }

public readonly struct StandCommand
{
    public readonly StandCommandType type;
    public readonly Vector2 direction;   // куда бить/лететь
    public readonly Vector2 worldPoint;  // куда ставить/кидать рой
    public readonly int abilityIndex;    // какую способность

    public StandCommand(StandCommandType t, Vector2 dir = default, Vector2 point = default, int ability = -1)
    { type = t; direction = dir; worldPoint = point; abilityIndex = ability; }
}

// ------------------------------------------------------------
// 3. ВОПЛОЩЕНИЯ: как стенд существует в пространстве
// ------------------------------------------------------------

public interface IStandEmbodiment
{
    void Setup();
    void Teardown();
    void OnSummon();
    void OnDismiss();
    void Tick(float dt);
    void Execute(in StandCommand cmd);

    // Может ли игрок взять прямой контроль над этим воплощением?
    // Integrated → false (это и есть мувсет владельца)
    // Autonomous → false (у него свой мозг)
    bool SupportsDirectControl { get; }
}

public static class EmbodimentFactory
{
    public static IStandEmbodiment Create(EmbodimentKind kind, StandEntity entity) => kind switch
    {
        EmbodimentKind.Follower   => new FollowerEmbodiment(entity),
        EmbodimentKind.Integrated => new IntegratedEmbodiment(entity),
        EmbodimentKind.Autonomous => new AutonomousEmbodiment(entity),
        EmbodimentKind.Deployable => new DeployableEmbodiment(entity),
        EmbodimentKind.Swarm      => new SwarmEmbodiment(entity),
        _ => throw new ArgumentOutOfRangeException()
    };
}

// --- 3a. Классический призрак за плечом (Star Platinum) ---
public class FollowerEmbodiment : IStandEmbodiment
{
    readonly StandEntity _e;
    GameObject _body;
    StandBodyView _view;      // аниматор, хитбоксы, спрайт
    Vector2 _anchorOffset = new(-0.7f, 0.9f);

    public bool SupportsDirectControl => true;

    public FollowerEmbodiment(StandEntity e) { _e = e; }

    public void Setup()
    {
        _body = UnityEngine.Object.Instantiate(_e.Definition.embodimentPrefab);
        _view = _body.GetComponent<StandBodyView>();
        _body.SetActive(false);
    }

    public void Teardown() { if (_body) UnityEngine.Object.Destroy(_body); }
    public void OnSummon()  { _body.SetActive(true); _view.PlaySummonFX(); }
    public void OnDismiss() { _view.PlayDismissFX(() => _body.SetActive(false)); }

    public void Tick(float dt)
    {
        if (!_e.IsSummoned) return;
        // Мягкое следование за владельцем + ограничение leashRange
        Vector2 target = (Vector2)_e.Owner.transform.position + _anchorOffset;
        _body.transform.position = Vector2.Lerp(_body.transform.position, target, 12f * dt);
    }

    public void Execute(in StandCommand cmd)
    {
        switch (cmd.type)
        {
            case StandCommandType.LightAttack: _view.PlayAttack(0, cmd.direction); break;
            case StandCommandType.HeavyAttack: _view.PlayAttack(1, cmd.direction); break;
            case StandCommandType.Barrage:     _view.EnterBarrage(cmd.direction);  break;
            case StandCommandType.Move:
                _anchorOffset = cmd.direction * _e.Definition.leashRange; // "вынести" стенд вперёд
                break;
        }
    }
}

// --- 3b. Стенд-кожа: тела нет, есть модификатор владельца ---
public class IntegratedEmbodiment : IStandEmbodiment
{
    readonly StandEntity _e;
    public bool SupportsDirectControl => false;

    public IntegratedEmbodiment(StandEntity e) { _e = e; }

    public void Setup() { }
    public void Teardown() { OnDismiss(); }

    public void OnSummon()
    {
        // Не спавним тело — переключаем состояние ВЛАДЕЛЬЦА:
        // другой мувсет, оверлей на спрайте, изменённые статы
        _e.Owner.Movement.ApplyStatModifier(_e.Definition);
        _e.Owner.View.SetStandSkinOverlay(true);
        _e.Owner.Combat.SetMoveset(MovesetId.StandEmpowered);
    }

    public void OnDismiss()
    {
        _e.Owner.Movement.RemoveStatModifier(_e.Definition);
        _e.Owner.View.SetStandSkinOverlay(false);
        _e.Owner.Combat.SetMoveset(MovesetId.Base);
    }

    public void Tick(float dt) { }

    public void Execute(in StandCommand cmd)
    {
        // Атаки integrated-стенда — это атаки владельца в усиленном мувсете.
        // Команды сюда почти не доходят: инпут-мост видит SupportsDirectControl=false
        // и просто гоняет владельца.
    }
}

// --- 3c. Автономный: тело + свой мозг (Highway Star) ---
public class AutonomousEmbodiment : IStandEmbodiment
{
    readonly StandEntity _e;
    GameObject _body;
    StandAIBrain _brain;      // условия активации, цели, поведение

    public bool SupportsDirectControl => false;

    public AutonomousEmbodiment(StandEntity e) { _e = e; }

    public void Setup()
    {
        _body = UnityEngine.Object.Instantiate(_e.Definition.embodimentPrefab);
        _brain = _body.GetComponent<StandAIBrain>();
        _brain.Bind(_e);              // мозг шлёт команды через ту же IssueCommand
        _body.SetActive(false);
    }

    public void Teardown() { if (_body) UnityEngine.Object.Destroy(_body); }
    public void OnSummon()  { _body.transform.position = _e.Owner.transform.position; _body.SetActive(true); }
    public void OnDismiss() { _body.SetActive(false); }
    public void Tick(float dt) { /* мозг тикает сам в своём Update */ }

    public void Execute(in StandCommand cmd)
    {
        // Команды исполняются, но приходят они от _brain, не от игрока
        if (cmd.type == StandCommandType.Move)
            _body.GetComponent<Rigidbody2D>().linearVelocity = cmd.direction * _brain.MoveSpeed;
    }
}

// --- 3d. Ставится в точку: турель/ловушка/зона (Echoes Act 2) ---
public class DeployableEmbodiment : IStandEmbodiment
{
    readonly StandEntity _e;
    GameObject _deployed;

    public bool SupportsDirectControl => false;

    public DeployableEmbodiment(StandEntity e) { _e = e; }

    public void Setup() { }
    public void Teardown() { Recall(); }
    public void OnSummon() { }
    public void OnDismiss() { Recall(); }
    public void Tick(float dt) { }

    public void Execute(in StandCommand cmd)
    {
        switch (cmd.type)
        {
            case StandCommandType.Deploy:
                Recall();
                _deployed = UnityEngine.Object.Instantiate(
                    _e.Definition.embodimentPrefab, cmd.worldPoint, Quaternion.identity);
                _deployed.GetComponent<DeployedStandZone>().Bind(_e);
                break;
            case StandCommandType.Recall:
                Recall();
                break;
        }
    }

    void Recall() { if (_deployed) UnityEngine.Object.Destroy(_deployed); }
}

// --- 3e. Колония (Harvest, Bad Company) ---
public class SwarmEmbodiment : IStandEmbodiment
{
    readonly StandEntity _e;
    readonly List<SwarmUnit> _units = new();
    Vector2 _swarmFocus;          // "курсор роя" — куда игрок их направляет
    SwarmMode _mode = SwarmMode.Orbit;

    public bool SupportsDirectControl => true;   // управление = управление фокусом

    public SwarmEmbodiment(StandEntity e) { _e = e; }

    public void Setup()
    {
        int count = 60; // из definition можно вынести
        for (int i = 0; i < count; i++)
        {
            var u = UnityEngine.Object.Instantiate(_e.Definition.embodimentPrefab)
                                       .GetComponent<SwarmUnit>();
            u.gameObject.SetActive(false);
            _units.Add(u);
        }
    }

    public void Teardown() { foreach (var u in _units) if (u) UnityEngine.Object.Destroy(u.gameObject); }
    public void OnSummon()  { foreach (var u in _units) { u.transform.position = _e.Owner.transform.position; u.gameObject.SetActive(true); } }
    public void OnDismiss() { foreach (var u in _units) u.gameObject.SetActive(false); }

    public void Tick(float dt)
    {
        if (!_e.IsSummoned) return;
        // Простейший флокинг: каждый юнит тянется к цели своего режима
        foreach (var u in _units)
        {
            Vector2 target = _mode switch
            {
                SwarmMode.Orbit    => (Vector2)_e.Owner.transform.position + u.OrbitOffset(Time.time),
                SwarmMode.Converge => _swarmFocus,
                SwarmMode.Carry    => _swarmFocus, // несут предмет/владельца к точке
                _ => u.transform.position
            };
            u.Steer(target, dt);
        }
    }

    public void Execute(in StandCommand cmd)
    {
        switch (cmd.type)
        {
            case StandCommandType.Move:        _swarmFocus += cmd.direction * 8f * Time.deltaTime; break;
            case StandCommandType.LightAttack: _mode = SwarmMode.Converge; break;   // накинуться на точку
            case StandCommandType.Recall:      _mode = SwarmMode.Orbit;    break;
            case StandCommandType.UseAbility:  _mode = SwarmMode.Carry;    break;   // собрать/принести
        }
    }
}

public enum SwarmMode { Orbit, Converge, Carry }

// ------------------------------------------------------------
// 4. СПОСОБНОСТИ: модули, не знающие про воплощение
// ------------------------------------------------------------

[Flags]
public enum AbilityTag
{
    None        = 0,
    HeavyImpact = 1 << 0,   // разбить стену
    Grapple     = 1 << 1,   // дотянуться (Hermit Purple, Stone Free)
    Phase       = 1 << 2,   // пролезть/распутаться
    TimeControl = 1 << 3,
    Heal        = 1 << 4,   // Crazy Diamond
    Trap        = 1 << 5,
    Gather      = 1 << 6,   // Harvest собирает предметы
}

public abstract class StandAbilityDefinition : ScriptableObject
{
    public string abilityName;
    public AbilityTag tags;
    public float cooldown;
    public abstract IStandAbility CreateRuntime(StandEntity owner);
}

public interface IStandAbility
{
    AbilityTag Tags { get; }
    bool CanActivate { get; }
    void Activate(in StandCommand cmd);
    void Tick(float dt);
}

// Пример конкретного модуля — остановка времени
[CreateAssetMenu(menuName = "JoJo/Abilities/Time Stop")]
public class TimeStopDefinition : StandAbilityDefinition
{
    public float duration = 3f;
    public override IStandAbility CreateRuntime(StandEntity owner) => new TimeStopAbility(this, owner);
}

public class TimeStopAbility : IStandAbility
{
    readonly TimeStopDefinition _def;
    readonly StandEntity _owner;
    float _cooldownLeft;

    public TimeStopAbility(TimeStopDefinition def, StandEntity owner) { _def = def; _owner = owner; }

    public AbilityTag Tags => _def.tags;
    public bool CanActivate => _cooldownLeft <= 0f;

    public void Activate(in StandCommand cmd)
    {
        if (!CanActivate) return;
        _cooldownLeft = _def.cooldown;
        TimeStopManager.Instance.Freeze(_def.duration, except: _owner.Owner);
    }

    public void Tick(float dt) => _cooldownLeft -= dt;
}

// ------------------------------------------------------------
// 5. МОСТ ИНПУТА: кто получает команды прямо сейчас
// ------------------------------------------------------------

public class DualControlBridge : MonoBehaviour
{
    public StandUser user;
    StandEntity Stand => user.Stand;

    // Зажатый триггер = фокус на стенде (если воплощение это поддерживает)
    public bool StandFocusHeld { get; private set; }

    void Update()
    {
        StandFocusHeld = Input.GetButton("StandFocus")
                         && Stand.IsSummoned
                         && Stand.Embodiment.SupportsDirectControl;

        Vector2 move = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (StandFocusHeld)
        {
            // Управляем стендом; владелец стоит (или идёт медленно — решается тут)
            Stand.IssueCommand(new StandCommand(StandCommandType.Move, move));
            if (Input.GetButtonDown("Attack"))
                Stand.IssueCommand(new StandCommand(StandCommandType.LightAttack, user.FacingDir));
            if (Input.GetButton("Special"))
                Stand.IssueCommand(new StandCommand(StandCommandType.Barrage, user.FacingDir));
        }
        else
        {
            // Управляем владельцем; стенд ассистирует по кнопке
            user.Movement.SetInput(move);
            if (Input.GetButtonDown("Attack")) user.Combat.LightAttack();
            if (Input.GetButtonDown("StandAssist") && Stand.IsSummoned)
                Stand.IssueCommand(new StandCommand(StandCommandType.LightAttack, user.FacingDir));
        }

        if (Input.GetButtonDown("SummonToggle"))
        {
            if (Stand.IsSummoned) Stand.Dismiss(); else Stand.Summon();
        }
    }
}

// ------------------------------------------------------------
// 6. ВЛАДЕЛЕЦ: сборная точка. Персонаж = префаб + StandDefinition
// ------------------------------------------------------------

public class StandUser : MonoBehaviour
{
    public StandDefinition standDefinition;

    public StandEntity Stand { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public PlayerView View { get; private set; }
    public Vector2 FacingDir => Movement.FacingDir;

    void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        Combat   = GetComponent<PlayerCombat>();
        View     = GetComponent<PlayerView>();

        Stand = gameObject.AddComponent<StandEntity>();
        Stand.Initialize(standDefinition, this);
    }
}

// ------------------------------------------------------------
// 7. ГЕЙТИНГ МЕТРОИДВАНИИ: двери спрашивают теги, не имена
// ------------------------------------------------------------

public class AbilityGate : MonoBehaviour
{
    public AbilityTag requiredTag;

    void OnTriggerEnter2D(Collider2D other)
    {
        var user = other.GetComponent<StandUser>();
        if (user != null && user.Stand.HasAbilityTag(requiredTag))
            Open();
    }

    void Open() { /* анимация, коллайдер off */ }
}

// ------------------------------------------------------------
// Заглушки внешних классов, чтобы был виден контракт
// ------------------------------------------------------------
public enum MovesetId { Base, StandEmpowered }
public class StandBodyView : MonoBehaviour {
    public void PlaySummonFX() { }
    public void PlayDismissFX(Action onDone) { onDone?.Invoke(); }
    public void PlayAttack(int index, Vector2 dir) { }
    public void EnterBarrage(Vector2 dir) { }
}
public class StandAIBrain : MonoBehaviour {
    public float MoveSpeed = 5f;
    public void Bind(StandEntity e) { }
}
public class DeployedStandZone : MonoBehaviour { public void Bind(StandEntity e) { } }
public class SwarmUnit : MonoBehaviour {
    public Vector2 OrbitOffset(float t) => new(Mathf.Cos(t + GetInstanceID()), Mathf.Sin(t + GetInstanceID())) * 1.5f;
    public void Steer(Vector2 target, float dt) => transform.position = Vector2.Lerp(transform.position, target, 4f * dt);
}
public class PlayerMovement : MonoBehaviour {
    public Vector2 FacingDir => Vector2.right;
    public void SetInput(Vector2 v) { }
    public void ApplyStatModifier(StandDefinition d) { }
    public void RemoveStatModifier(StandDefinition d) { }
}
public class PlayerCombat : MonoBehaviour {
    public void LightAttack() { }
    public void SetMoveset(MovesetId id) { }
}
public class PlayerView : MonoBehaviour {
    public void SetStandSkinOverlay(bool on) { }
}
public class TimeStopManager : MonoBehaviour {
    public static TimeStopManager Instance;
    public void Freeze(float duration, StandUser except) { }
}
