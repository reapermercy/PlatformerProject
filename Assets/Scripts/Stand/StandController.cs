using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Основной контроллер стенда игрока.
/// Режимы: Attached (скрыт), Free (управляется мышью), Automatic (AI), SemiAuto.
/// Живёт на отдельном GameObject, physics layer "StandGhost".
/// HP привязаны к PlayerStats через DamageHandler.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(DamageHandler))]
public class StandController : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Data")]
    [SerializeField] private StandData standData;

    [Header("Owner")]
    [SerializeField] private PlayerController ownerPlayerController; // назначить в инспекторе
    private Transform     owner;        // берётся из ownerPlayerController
    private PlayerContext ownerContext; // берётся из ownerPlayerController.Context

    [Header("Command")]
    [Tooltip("Задержка перед возвратом после двойного клика (сек)")]
    [SerializeField] private float returnDelay = 0.4f;


    [Header("Idle Return")]
    [Tooltip("Через сколько секунд без ввода стенд возвращается за спину игрока")]
    [SerializeField] private float idleReturnDelay = 3f;
    [Tooltip("Смещение относительно игрока (x — за спину, y — высота плеча)")]
    [SerializeField] private Vector2 idleOffset = new Vector2(0.8f, 0.3f);

    [Header("Spring Return")]
    [Tooltip("Жёсткость пружины — чем больше, тем резче overshoot")]
    [SerializeField] private float springStrength = 90f;
    [Tooltip("Затухание — чем меньше, тем дольше колебания")]
    [SerializeField] private float springDamping  = 12f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject visualRoot;      // корневой объект для вкл/выкл визуала

    [Header("Item Handling")]
    [SerializeField] private HeldItemCollision heldItemCollision;

    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask enemyLayers;

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 1.5f;

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent<StandMode> OnModeChanged;
    public UnityEvent<int>       OnAbilityUsed;      // индекс способности
    public UnityEvent             OnStandAttack;
    public UnityEvent             OnStandRecalled;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private StandMode  _currentMode;
    private Rigidbody2D _rb;
    private Collider2D  _collider;
    private PlayerStats _ownerStats;

    // Движение в Free режиме
    private Vector2 _targetPosition;
    private bool    _isMovingToTarget;
    private bool    _facingRight = true;

    // Idle return
    private float   _lastInputTime;
    private bool    _isIdleReturning;

    // Spring-возврат (пружинная физика для overshooting)
    private Vector2 _springVelocity;

    // Атака
    private float _attackCooldownTimer;

    // Командный полёт (клик / двойной клик)
    private bool  _isCommandFlying;
    private bool  _commandReturnAfter;  // true = вернуться после действия, false = остаться
    private float _returnDelayTimer;    // ожидание перед возвратом

    // ──────────────────────────────────────────
    // Animator (хэши вместо строк)
    // ──────────────────────────────────────────

    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashReturn   = Animator.StringToHash("Return");
    private static readonly int HashSummon   = Animator.StringToHash("Summon");
    private static readonly int HashAttack   = Animator.StringToHash("Attack");
    private static readonly int HashAbility  = Animator.StringToHash("Ability");

    // ──────────────────────────────────────────
    // Переиспользуемый буфер + фильтр (Unity 6 API — OverlapCircle с ContactFilter2D)
    // ──────────────────────────────────────────

    private static readonly Collider2D[] s_overlapBuffer = new Collider2D[16];
    // Мутируемый struct: перед каждым вызовом задаём SetLayerMask — OK, Physics на main thread
    private static ContactFilter2D s_overlapFilter = new ContactFilter2D { useTriggers = true };

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private Animator _anim;

    private void Awake()
    {
        _rb       = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _anim     = GetComponentInChildren<Animator>();

        _rb.bodyType     = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
    }

    private void Start()
    {
        // Start() гарантированно вызывается после всех Awake() —
        // PlayerController уже создал свой Context к этому моменту
        if (ownerPlayerController != null)
        {
            owner        = ownerPlayerController.transform;
            ownerContext = ownerPlayerController.Context;
            _ownerStats  = ownerPlayerController.GetComponent<PlayerStats>();
        }

        if (standData != null)
            ApplyStandData(standData);

        SetMode(standData != null ? standData.defaultMode : StandMode.Attached);
    }

    private void Update()
    {
        if (_ownerStats != null && !_ownerStats.CanAct)
        {
            if (_currentMode != StandMode.Attached)
                SetMode(StandMode.Attached);
            return;
        }

        TickAttackCooldown();

        if (_currentMode == StandMode.Free)
        {
            TickIdleReturn();
            TickReturnDelay();
            _anim?.SetBool(HashIsMoving, _isMovingToTarget || _isCommandFlying);
        }
    }

    private void FixedUpdate()
    {
        if (_ownerStats != null && !_ownerStats.CanAct) return;

        if (_currentMode == StandMode.Free)
        {
            if (_isMovingToTarget)
            {
                MoveToTarget();
                // Командный полёт: проверяем прибытие сразу после MoveToTarget
                if (_isCommandFlying && !_isMovingToTarget)
                    HandleCommandArrival();
            }
            else if (_isIdleReturning)
                SpringMoveToTarget();
            else
                EnforceRadiusLeash(); // стенд стоит на месте — следим за игроком
        }
    }

    // ──────────────────────────────────────────
    // Инициализация / смена StandData
    // ──────────────────────────────────────────

    /// <summary>Применить данные стенда (при смене через StandSwitchManager)</summary>
    public void ApplyStandData(StandData data)
    {
        standData = data;

        if (spriteRenderer != null && data.standSprite != null)
            spriteRenderer.sprite = data.standSprite;

        // Сбросить кулдауны способностей
        foreach (var ability in data.abilities)
            ability?.ResetCooldown();
    }

    /// <summary>Установить владельца (вызывается при инициализации)</summary>
    public void SetOwner(PlayerController playerController)
    {
        owner        = playerController.transform;
        ownerContext  = playerController.Context;
    }

    // ──────────────────────────────────────────
    // Режимы
    // ──────────────────────────────────────────

    /// <summary>Переключить режим стенда</summary>
    public void SetMode(StandMode newMode)
    {
        StandMode prevMode = _currentMode;
        _currentMode = newMode;

        switch (newMode)
        {
            case StandMode.Attached:
                EnterAttached();
                break;
            case StandMode.Free:
                EnterFree();
                break;
            case StandMode.Automatic:
                EnterAutomatic();
                break;
            case StandMode.SemiAuto:
                EnterSemiAuto();
                break;
        }

        // Синхронизируем контекст игрока
        if (ownerContext != null)
            ownerContext.StandIsFree = (newMode == StandMode.Free);

        OnModeChanged?.Invoke(newMode);
    }

    /// <summary>Q — переключение Attached ↔ Free</summary>
    public void ToggleStand()
    {
        if (_currentMode == StandMode.Attached)
            SetMode(StandMode.Free);
        else
        {
            SetMode(StandMode.Attached);
            OnStandRecalled?.Invoke();
        }
    }

    // ──────────────────────────────────────────
    // Attached — стенд скрыт, слит с персонажем
    // ──────────────────────────────────────────

    private void EnterAttached()
    {
        // При отзыве стенда — бросить предмет где он есть (игрок подберёт сам)
        if (heldItemCollision != null && heldItemCollision.IsHoldingItem)
            heldItemCollision.ReleaseItem();

        _anim?.SetTrigger(HashReturn);

        if (visualRoot != null)
            visualRoot.SetActive(false);

        _isMovingToTarget = false;

        // Стенд следует за владельцем
        if (owner != null)
            transform.position = owner.position;

        // Отключить коллайдер (стенд не получает урон в Attached)
        if (_collider != null)
            _collider.enabled = false;
    }

    // ──────────────────────────────────────────
    // Free — управляется мышью, виден
    // ──────────────────────────────────────────

    private void EnterFree()
    {
        if (visualRoot != null)
            visualRoot.SetActive(true);

        _anim?.SetTrigger(HashSummon);

        // Появляется рядом с владельцем
        if (owner != null)
            transform.position = owner.position;

        // Включить коллайдер
        if (_collider != null)
            _collider.enabled = true;
    }

    // ──────────────────────────────────────────
    // Automatic / SemiAuto — заглушки для будущего AI
    // ──────────────────────────────────────────

    private void EnterAutomatic()
    {
        if (visualRoot != null)
            visualRoot.SetActive(true);
        if (_collider != null)
            _collider.enabled = true;
    }

    private void EnterSemiAuto()
    {
        if (visualRoot != null)
            visualRoot.SetActive(true);
        if (_collider != null)
            _collider.enabled = true;
    }

    // ──────────────────────────────────────────
    // Движение (Free режим)
    // ──────────────────────────────────────────

    /// <summary>Hold ПКМ — ручное управление к позиции курсора</summary>
    public void SetTargetPosition(Vector2 worldPos)
    {
        _isCommandFlying  = false;
        _targetPosition   = ClampToRadius(worldPos);
        _isMovingToTarget = true;
        _isIdleReturning  = false;
        _springVelocity   = Vector2.zero;
        _lastInputTime    = Time.time;
    }

    /// <summary>Hold ПКМ отпущен</summary>
    public void StopMoving()
    {
        _isMovingToTarget = false;
        _lastInputTime    = Time.time;
    }

    /// <summary>1 клик ПКМ — лететь к точке, выполнить действие, остаться там</summary>
    public void CommandFlyToAndReturn(Vector2 worldPos)  => StartCommand(worldPos, returnAfter: false);

    /// <summary>2 клика ПКМ — лететь к точке, выполнить действие, вернуться к игроку</summary>
    public void CommandFlyToAndStay(Vector2 worldPos)    => StartCommand(worldPos, returnAfter: true);

    /// <summary>
    /// ЛКМ при зажатом ПКМ:
    /// — если стенд ничего не держит → подобрать ближайший предмет
    /// — если держит → бросить в сторону курсора
    /// </summary>
    public void GrabOrThrowAtPosition(Vector2 worldPos)
    {
        if (_currentMode != StandMode.Free) return;

        if (heldItemCollision != null && heldItemCollision.IsHoldingItem)
        {
            Vector2 dir = (worldPos - (Vector2)transform.position).normalized;
            ThrowHeldItem(dir);
        }
        else
        {
            heldItemCollision?.TryGrabNearest();
        }
    }

    private void StartCommand(Vector2 worldPos, bool returnAfter)
    {
        if (_currentMode != StandMode.Free) return;

        _isCommandFlying    = true;
        _commandReturnAfter = returnAfter;
        // Командный полёт летит точно в точку клика — без офсета курсора
        _targetPosition     = ClampToRadius(worldPos);
        _isMovingToTarget   = true;
        _isIdleReturning    = false;
        _springVelocity     = Vector2.zero;
        _lastInputTime      = Time.time;
    }

    private void HandleCommandArrival()
    {
        _isCommandFlying = false;

        // Повернуться к ближайшему врагу и атаковать
        FaceNearestEnemy();
        bool attacked = TryAttackNow();

        // Если врагов нет — попробовать захват или взаимодействие
        if (!attacked)
        {
            bool wasHolding = heldItemCollision != null && heldItemCollision.IsHoldingItem;

            // Хватаем только если руки пусты
            if (!wasHolding)
                heldItemCollision?.TryGrabNearest();

            bool justGrabbed = !wasHolding
                             && heldItemCollision != null
                             && heldItemCollision.IsHoldingItem;

            // Взаимодействие — только если ничего не подобрали только что
            // (иначе стенд хватал бы ключ и сразу открывал бы дверь)
            if (!justGrabbed)
                TryInteractWithNearest();
        }

        if (_commandReturnAfter)
        {
            // Возврат с задержкой — стенд успевает выполнить действие
            _returnDelayTimer = returnDelay;
        }
        else
        {
            // Остаться — сбрасываем таймер чтобы idle-return не сработал сразу
            _lastInputTime = Time.time;
        }
    }

    private void TickReturnDelay()
    {
        if (_returnDelayTimer <= 0f) return;

        _returnDelayTimer -= Time.deltaTime;
        if (_returnDelayTimer <= 0f)
        {
            _isIdleReturning = true;
            _springVelocity  = Vector2.zero;
            _targetPosition  = GetBehindPlayerPosition();
        }
    }

    /// <summary>Командная атака при прилёте — увеличенный радиус, без кулдауна. Возвращает true если попал.</summary>
    private bool TryAttackNow()
    {
        if (standData == null) return false;

        float   radius = standData.combatConfig.attackRadius * 1.5f;
        Vector2 origin = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;

        s_overlapFilter.SetLayerMask(enemyLayers);
        int count = Physics2D.OverlapCircle(origin, radius, s_overlapFilter, s_overlapBuffer);
        if (count == 0) return false;

        // Используем общий ExecuteAttack — он сам применит урон и кулдаун
        ExecuteAttack();
        return true;
    }

    /// <summary>Проверить, разрешён ли этому стенду данный тип взаимодействия.</summary>
    private bool IsInteractionAllowed(IInteractable interactable)
    {
        if (standData == null) return true;
        var mask = standData.allowedInteractions;

        return interactable switch
        {
            LeverInteractable    => mask.HasFlag(StandInteractionMask.Lever),
            DoorInteractable     => mask.HasFlag(StandInteractionMask.Door),
            SignInteractable     => mask.HasFlag(StandInteractionMask.Sign),
            DialogueInteractable => mask.HasFlag(StandInteractionMask.NPC),
            NoteInteractable     => mask.HasFlag(StandInteractionMask.Note),
            _                    => true  // неизвестный тип — разрешить по умолчанию
        };
    }

    /// <summary>
    /// Взаимодействие с ближайшим IInteractable из реестра (рычаги, двери).
    /// Коллайдер на объекте НЕ нужен — ищем по расстоянию через InteractableRegistry.
    /// Возвращает true если взаимодействие состоялось.
    /// </summary>
    private bool TryInteractWithNearest()
    {
        // Получаем ItemData держимого предмета (если есть)
        ItemData   heldItemData = null;
        GameObject heldItemGO   = null;
        if (heldItemCollision != null && heldItemCollision.IsHoldingItem)
        {
            var pickup = heldItemCollision.HeldItem.GetComponent<ItemPickupInteractable>();
            if (pickup != null)
            {
                heldItemData = pickup.ItemData;
                heldItemGO   = heldItemCollision.HeldItem.gameObject;
            }
        }

        // Ищем ближайший зарегистрированный интерактивный объект
        IInteractable closest     = null;
        float         closestDist = interactRadius;

        foreach (var mb in InteractableRegistry.All)
        {
            if (mb == null) continue;
            if (mb is not IInteractable interactable) continue;
            if (interactable is ItemPickupInteractable) continue; // предметы — не здесь
            if (!IsInteractionAllowed(interactable)) continue;   // проверка маски стенда

            float dist = Vector2.Distance(transform.position, mb.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest     = interactable;
            }
        }

        if (closest == null) return false;

        // Дверь — всегда через TryOpenWithKey.
        // Стенд НИКОГДА не трогает инвентарь игрока: DoorInteractable.Interact()
        // проверяет Inventory.Instance и списывает ключ — это поведение только для игрока.
        if (closest is DoorInteractable door)
        {
            bool opened = door.TryOpenWithKey(heldItemData);
            // Физический ключ потрачен — убрать из рук стенда
            if (opened && heldItemData != null && heldItemData.itemType == ItemType.Key)
            {
                heldItemCollision?.ReleaseItem();
                if (heldItemGO != null) Destroy(heldItemGO);
            }
            return true;
        }

        // Обычное взаимодействие (рычаг, NPC и т.д.)
        closest.Interact(null);
        return true;
    }

    private void MoveToTarget()
    {
        // Перезажимаем цель в текущий радиус — игрок мог отойти пока стенд летел
        _targetPosition = ClampToRadius(_targetPosition);

        float speed     = standData != null ? standData.combatConfig.moveSpeed : 10f;
        Vector2 current = _rb.position;
        // Зажимаем итоговую позицию в радиус — один MovePosition за кадр, нет конфликта с leash
        Vector2 newPos  = ClampToRadius(Vector2.MoveTowards(current, _targetPosition, speed * Time.fixedDeltaTime));
        _rb.MovePosition(newPos);

        // Флип по горизонтальному направлению движения
        float dx = _targetPosition.x - current.x;
        if (dx > 0.05f && !_facingRight)  Flip();
        if (dx < -0.05f && _facingRight)  Flip();

        // Достигли цели — остановиться
        if (Vector2.Distance(newPos, _targetPosition) < 0.05f)
        {
            _isMovingToTarget = false;
            _isIdleReturning  = false;
        }
    }

    /// <summary>
    /// Пружинное движение к цели — проскакивает чуть дальше и возвращается.
    /// Используется при idle-возврате за спину игрока.
    /// </summary>
    private void SpringMoveToTarget()
    {
        // Цель idle-возврата обновляем каждый кадр — игрок ведь движется
        _targetPosition = GetBehindPlayerPosition();

        Vector2 current    = _rb.position;
        Vector2 toTarget   = _targetPosition - current;

        // Сила пружины к цели
        Vector2 springForce = toTarget * springStrength;

        // Затухание — убираем скорость пропорционально
        _springVelocity += springForce * Time.fixedDeltaTime;
        _springVelocity *= Mathf.Clamp01(1f - springDamping * Time.fixedDeltaTime);

        Vector2 newPos = ClampToRadius(current + _springVelocity * Time.fixedDeltaTime);
        _rb.MovePosition(newPos);

        // Флип по направлению движения
        float dx = _springVelocity.x;
        if (dx > 0.1f && !_facingRight) Flip();
        if (dx < -0.1f && _facingRight) Flip();

        // Остановились — завершить возврат
        if (toTarget.magnitude < 0.05f && _springVelocity.magnitude < 0.05f)
        {
            _springVelocity  = Vector2.zero;
            _isIdleReturning = false;
        }
    }

    private void Flip()
    {
        _facingRight = !_facingRight;
        if (visualRoot != null)
        {
            Vector3 s = visualRoot.transform.localScale;
            s.x *= -1f;
            visualRoot.transform.localScale = s;
        }
    }

    /// <summary>Зажать точку внутри maxRadius от владельца</summary>
    private Vector2 ClampToRadius(Vector2 target)
    {
        if (owner == null || standData == null) return target;
        Vector2 ownerPos = (Vector2)owner.position;
        Vector2 delta    = target - ownerPos;
        if (delta.magnitude <= standData.maxRadius) return target;
        return ownerPos + delta.normalized * standData.maxRadius;
    }

    // ──────────────────────────────────────────
    // Idle Return — возврат за спину игрока
    // ──────────────────────────────────────────

    private void TickIdleReturn()
    {
        if (_isMovingToTarget)  return;
        if (_isIdleReturning)   return;
        if (_isCommandFlying)   return; // во время команды не запускаем авто-возврат

        if (Time.time - _lastInputTime >= idleReturnDelay)
        {
            _targetPosition  = GetBehindPlayerPosition();
            _isIdleReturning = true;
        }
    }

    /// <summary>Позиция за плечом игрока в зависимости от направления взгляда</summary>
    private Vector2 GetBehindPlayerPosition()
    {
        if (owner == null) return _rb.position;

        // Противоположная сторона от взгляда
        float facingSign = (ownerPlayerController != null && ownerPlayerController.FacingRight)
            ? -1f   // смотрит вправо → стенд слева
            : 1f;   // смотрит влево  → стенд справа

        Vector2 offset = new Vector2(facingSign * idleOffset.x, idleOffset.y);
        return ClampToRadius((Vector2)owner.position + offset);
    }

    // ──────────────────────────────────────────

    // ──────────────────────────────────────────
    // RadiusLeash — плавный возврат если игрок ушёл
    // ──────────────────────────────────────────

    /// <summary>
    /// Вызывается только когда стенд стоит на месте (не движется к цели).
    /// Если игрок ушёл дальше maxRadius — запускаем пружинный возврат.
    /// MovePosition НЕ вызывается — нет конфликта с MoveToTarget.
    /// </summary>
    private void EnforceRadiusLeash()
    {
        if (owner == null || standData == null) return;

        float dist = Vector2.Distance(_rb.position, (Vector2)owner.position);
        if (dist <= standData.maxRadius) return;

        // Запустить плавный возврат к позиции за спиной игрока
        _isIdleReturning = true;
        _springVelocity  = Vector2.zero;
        _targetPosition  = GetBehindPlayerPosition();
    }

    /// <summary>Бросить удерживаемый предмет в направлении курсора.</summary>
    public void ThrowHeldItem(Vector2 direction, float force = 8f)
    {
        if (heldItemCollision == null || !heldItemCollision.IsHoldingItem) return;
        heldItemCollision.ThrowItem(direction, force);
    }

    // ──────────────────────────────────────────
    // Атака стенда (ПКМ клик в Free)
    // ──────────────────────────────────────────

    /// <summary>
    /// ЛКМ при зажатом ПКМ:
    /// — держит предмет → бросить в сторону курсора
    /// — предмет рядом  → схватить
    /// — рычаг/дверь рядом → взаимодействие
    /// — ничего нет     → ударить
    /// </summary>
    public void GrabOrThrowOrAttack(Vector2 worldPos)
    {
        if (_currentMode != StandMode.Free) return;

        if (heldItemCollision != null && heldItemCollision.IsHoldingItem)
        {
            // Держим предмет — сначала попробовать использовать на цели (ключ → дверь и т.д.)
            // TryInteractWithNearest сам проверяет что стенд несёт ключ
            if (TryInteractWithNearest()) return;

            // Рядом нет подходящей цели → бросить в сторону курсора
            Vector2 dir = (worldPos - (Vector2)transform.position).normalized;
            ThrowHeldItem(dir);
            return;
        }

        // Руки пусты — попробовать схватить ближайший предмет
        if (heldItemCollision != null)
        {
            heldItemCollision.TryGrabNearest();
            if (heldItemCollision.IsHoldingItem) return;
        }

        // Предметов нет — попробовать взаимодействие (рычаг и т.д.)
        if (TryInteractWithNearest()) return;

        // Совсем ничего нет → ударить
        ExecuteAttack();
    }

    /// <summary>ПКМ клик — захватить предмет / отпустить / удар (устаревший путь)</summary>
    public void PerformAction(Vector2 worldPos)
    {
        if (heldItemCollision != null && heldItemCollision.IsHoldingItem)
        {
            heldItemCollision.ReleaseItem();
            return;
        }

        if (heldItemCollision != null)
        {
            heldItemCollision.TryGrabNearest();
            if (heldItemCollision.IsHoldingItem) return;
        }

        ExecuteAttack();
    }

    /// <summary>Нанести удар в текущей позиции (с кулдауном).</summary>
    private void ExecuteAttack()
    {
        if (!CanAttack()) return;

        FaceNearestEnemy();

        float   radius    = standData != null ? standData.combatConfig.attackRadius    : 0.8f;
        int     damage    = standData != null ? standData.combatConfig.baseDamage      : 12;
        float   knockback = standData != null ? standData.combatConfig.knockbackForce  : 3f;
        Vector2 origin    = attackPoint != null
            ? (Vector2)attackPoint.position
            : (Vector2)transform.position;

        s_overlapFilter.SetLayerMask(enemyLayers);
        int count = Physics2D.OverlapCircle(origin, radius, s_overlapFilter, s_overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            var hit = s_overlapBuffer[i];
            if (hit == null) continue;

            hit.GetComponent<DamageHandler>()?.ApplyDamage(damage, DamageSource.Stand);

            var enemyRb = hit.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                enemyRb.AddForce(dir * knockback, ForceMode2D.Impulse);
            }
        }

        _attackCooldownTimer = standData != null ? standData.combatConfig.attackCooldown : 0.3f;
        _anim?.SetTrigger(HashAttack);
        OnStandAttack?.Invoke();
    }

    private bool CanAttack()
    {
        if (standData != null && !standData.canAttack) return false;
        return _attackCooldownTimer <= 0f && _currentMode != StandMode.Attached;
    }

    private void TickAttackCooldown()
    {
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;
    }

    // ──────────────────────────────────────────
    // Способности (1, 2, 3)
    // ──────────────────────────────────────────

    /// <summary>Активировать способность по индексу (0, 1, 2)</summary>
    public void UseAbility(int index)
    {
        if (standData == null) return;
        if (index < 0 || index >= standData.abilities.Count) return;

        var ability = standData.abilities[index];
        if (ability == null) return;

        FaceNearestEnemy();
        _anim?.SetTrigger(HashAbility);

        if (ability.TryExecute(this))
            OnAbilityUsed?.Invoke(index);
    }

    // ──────────────────────────────────────────
    // Attached mode — следовать за владельцем
    // ──────────────────────────────────────────

    private void LateUpdate()
    {
        if (_currentMode == StandMode.Attached && owner != null)
            transform.position = owner.position;
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Радиус leash
        if (owner != null && standData != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(owner.position, standData.maxRadius);
        }

        // Радиус атаки
        float atkRadius = standData != null ? standData.combatConfig.attackRadius : 0.8f;
        Gizmos.color = Color.magenta;
        if (attackPoint != null)
            Gizmos.DrawWireSphere(attackPoint.position, atkRadius);
        else
            Gizmos.DrawWireSphere(transform.position, atkRadius);
    }

    // ──────────────────────────────────────────
    // Публичные свойства
    // ──────────────────────────────────────────

    public StandMode  CurrentMode        => _currentMode;
    public StandData  Data               => standData;
    public Transform  Owner              => owner;
    public Vector2    Position           => (Vector2)transform.position;
    public bool       IsMovingToTarget   => _isMovingToTarget;
    public LayerMask  EnemyLayers        => enemyLayers;
    public Vector2    MouseWorldPosition => _mouseWorldPosition;

    private Vector2 _mouseWorldPosition;

    /// <summary>
    /// Обновить позицию курсора мыши в мировых координатах.
    /// Вызывается из StandInputHandler через OnMouseWorldUpdated.
    /// Используется ProjectileAbility для направления снаряда.
    /// </summary>
    public void UpdateMousePosition(Vector2 worldPos) => _mouseWorldPosition = worldPos;

    /// <summary>
    /// Мгновенно повернуть стенд лицом к ближайшему врагу.
    /// Вызывается перед атакой и активацией способностей.
    /// </summary>
    public void FaceNearestEnemy()
    {
        Collider2D nearest = Physics2D.OverlapCircle(transform.position, 15f, enemyLayers);
        if (nearest == null) return;

        float dx = nearest.transform.position.x - transform.position.x;
        if (dx > 0.01f && !_facingRight) Flip();
        else if (dx < -0.01f && _facingRight) Flip();
    }
}
