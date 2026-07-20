using System;
using UnityEngine;

/// <summary>
/// «Дух за плечом» — текущее (и пока единственное) воплощение стенда игрока.
///
/// Поведение перенесено из StandController один в один: ручное ведение мышью,
/// командный полёт, пружинный возврат за спину, поводок maxRadius, флип, визуал.
/// Ничего не добавлено и не изменено — шаг 1 миграции только переселяет код.
/// </summary>
public class FollowerEmbodiment : IStandEmbodiment
{
    private readonly FollowerTuning _tuning;
    private StandBody _body;

    // Движение к цели
    private Vector2 _targetPosition;
    private bool    _isMovingToTarget;
    private bool    _facingRight = true;

    // Idle return
    private float _lastInputTime;
    private bool  _isIdleReturning;

    // Пружина (overshoot при возврате за спину)
    private Vector2 _springVelocity;

    // Командный полёт
    private bool  _isCommandFlying;
    private bool  _commandReturnAfter;
    private float _returnDelayTimer;

    private bool _isSummoned;

    public event Action OnArrived;

    public bool    SupportsDirectControl => true;
    public Vector2 Position              => _body != null ? (Vector2)_body.Transform.position : Vector2.zero;
    public bool    IsMoving              => _isMovingToTarget || _isCommandFlying;

    public FollowerEmbodiment(FollowerTuning tuning) => _tuning = tuning;

    // ──────────────────────────────────────────
    // Жизненный цикл
    // ──────────────────────────────────────────

    public void Setup(StandBody body) => _body = body;

    public void Teardown()
    {
        OnArrived = null;
        _body     = null;
    }

    public void OnSummon()
    {
        if (_body == null) return;
        _isSummoned = true;

        if (_body.VisualRoot != null)
            _body.VisualRoot.SetActive(true);

        _body.Animator?.SetTrigger(StandAnimHashes.Summon);

        // Появляется рядом с владельцем
        if (_body.Owner != null)
            _body.Transform.position = _body.Owner.position;

        if (_body.Collider != null)
            _body.Collider.enabled = true;
    }

    public void OnDismiss()
    {
        if (_body == null) return;
        _isSummoned = false;

        _body.Animator?.SetTrigger(StandAnimHashes.Return);

        if (_body.VisualRoot != null)
            _body.VisualRoot.SetActive(false);

        ResetMotion();

        // Стенд слит с владельцем
        if (_body.Owner != null)
            _body.Transform.position = _body.Owner.position;

        // В Attached стенд не получает урон
        if (_body.Collider != null)
            _body.Collider.enabled = false;
    }

    private void ResetMotion()
    {
        _isMovingToTarget = false;
        _isIdleReturning  = false;
        _isCommandFlying  = false;
        _returnDelayTimer = 0f;
        _springVelocity   = Vector2.zero;
    }

    // ──────────────────────────────────────────
    // Тики
    // ──────────────────────────────────────────

    public void Tick(float dt)
    {
        if (_body == null || !_isSummoned) return;

        TickIdleReturn();
        TickReturnDelay(dt);

        _body.Animator?.SetBool(StandAnimHashes.IsMoving, _isMovingToTarget || _isCommandFlying);
    }

    public void FixedTick(float dt)
    {
        if (_body == null || !_isSummoned) return;

        if (_isMovingToTarget)
        {
            MoveToTarget(dt);
            // Командный полёт: проверяем прибытие сразу после MoveToTarget
            if (_isCommandFlying && !_isMovingToTarget)
                HandleCommandArrival();
        }
        else if (_isIdleReturning)
        {
            SpringMoveToTarget(dt);
        }
        else
        {
            EnforceRadiusLeash(); // стенд стоит на месте — следим за игроком
        }
    }

    public void LateTick(float dt)
    {
        if (_body == null || _isSummoned) return;

        // Attached — тело едет на владельце
        if (_body.Owner != null)
            _body.Transform.position = _body.Owner.position;
    }

    // ──────────────────────────────────────────
    // Намерения
    // ──────────────────────────────────────────

    public void MoveTo(Vector2 worldPos)
    {
        _isCommandFlying  = false;
        _targetPosition   = ClampToRadius(worldPos);
        _isMovingToTarget = true;
        _isIdleReturning  = false;
        _springVelocity   = Vector2.zero;
        _lastInputTime    = Time.time;
    }

    public void StopMoving()
    {
        _isMovingToTarget = false;
        _lastInputTime    = Time.time;
    }

    public void CommandFlyTo(Vector2 worldPos, bool returnAfter)
    {
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

        // Что делать на месте — решает контроллер (атака / захват / взаимодействие)
        OnArrived?.Invoke();

        if (_commandReturnAfter)
        {
            // Возврат с задержкой — стенд успевает выполнить действие
            _returnDelayTimer = _tuning.returnDelay;
        }
        else
        {
            // Остаться — сбрасываем таймер чтобы idle-return не сработал сразу
            _lastInputTime = Time.time;
        }
    }

    private void TickReturnDelay(float dt)
    {
        if (_returnDelayTimer <= 0f) return;

        _returnDelayTimer -= dt;
        if (_returnDelayTimer <= 0f)
        {
            _isIdleReturning = true;
            _springVelocity  = Vector2.zero;
            _targetPosition  = GetBehindPlayerPosition();
        }
    }

    // ──────────────────────────────────────────
    // Движение
    // ──────────────────────────────────────────

    private void MoveToTarget(float dt)
    {
        // Перезажимаем цель в текущий радиус — игрок мог отойти пока стенд летел
        _targetPosition = ClampToRadius(_targetPosition);

        var     data    = _body.Data;
        float   speed   = data != null ? data.combatConfig.moveSpeed : 10f;
        Vector2 current = _body.Rigidbody.position;
        // Зажимаем итоговую позицию в радиус — один MovePosition за кадр, нет конфликта с leash
        Vector2 newPos  = ClampToRadius(Vector2.MoveTowards(current, _targetPosition, speed * dt));
        _body.Rigidbody.MovePosition(newPos);

        // Флип по горизонтальному направлению движения
        float dx = _targetPosition.x - current.x;
        if (dx >  0.05f && !_facingRight) Flip();
        if (dx < -0.05f &&  _facingRight) Flip();

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
    private void SpringMoveToTarget(float dt)
    {
        // Цель idle-возврата обновляем каждый кадр — игрок ведь движется
        _targetPosition = GetBehindPlayerPosition();

        Vector2 current  = _body.Rigidbody.position;
        Vector2 toTarget = _targetPosition - current;

        // Сила пружины к цели
        Vector2 springForce = toTarget * _tuning.springStrength;

        // Затухание — убираем скорость пропорционально
        _springVelocity += springForce * dt;
        _springVelocity *= Mathf.Clamp01(1f - _tuning.springDamping * dt);

        Vector2 newPos = ClampToRadius(current + _springVelocity * dt);
        _body.Rigidbody.MovePosition(newPos);

        // Флип по направлению движения
        float dx = _springVelocity.x;
        if (dx >  0.1f && !_facingRight) Flip();
        if (dx < -0.1f &&  _facingRight) Flip();

        // Остановились — завершить возврат
        if (toTarget.magnitude < 0.05f && _springVelocity.magnitude < 0.05f)
        {
            _springVelocity  = Vector2.zero;
            _isIdleReturning = false;
        }
    }

    public void FaceTowards(float worldX)
    {
        float dx = worldX - _body.Transform.position.x;
        if (dx >  0.01f && !_facingRight) Flip();
        else if (dx < -0.01f && _facingRight) Flip();
    }

    private void Flip()
    {
        _facingRight = !_facingRight;
        if (_body.VisualRoot != null)
        {
            Vector3 s = _body.VisualRoot.transform.localScale;
            s.x *= -1f;
            _body.VisualRoot.transform.localScale = s;
        }
    }

    /// <summary>Зажать точку внутри maxRadius от владельца</summary>
    private Vector2 ClampToRadius(Vector2 target)
    {
        var data = _body.Data;
        if (_body.Owner == null || data == null) return target;

        Vector2 ownerPos = (Vector2)_body.Owner.position;
        Vector2 delta    = target - ownerPos;
        if (delta.magnitude <= data.maxRadius) return target;
        return ownerPos + delta.normalized * data.maxRadius;
    }

    // ──────────────────────────────────────────
    // Idle Return / поводок
    // ──────────────────────────────────────────

    private void TickIdleReturn()
    {
        if (_isMovingToTarget) return;
        if (_isIdleReturning)  return;
        if (_isCommandFlying)  return; // во время команды не запускаем авто-возврат

        if (Time.time - _lastInputTime >= _tuning.idleReturnDelay)
        {
            _targetPosition  = GetBehindPlayerPosition();
            _isIdleReturning = true;
        }
    }

    /// <summary>Позиция за плечом игрока в зависимости от направления взгляда</summary>
    private Vector2 GetBehindPlayerPosition()
    {
        if (_body.Owner == null) return _body.Rigidbody.position;

        // Противоположная сторона от взгляда
        var   ownerController = _body.Controller.OwnerController;
        float facingSign = (ownerController != null && ownerController.FacingRight)
            ? -1f   // смотрит вправо → стенд слева
            :  1f;  // смотрит влево  → стенд справа

        Vector2 offset = new Vector2(facingSign * _tuning.idleOffset.x, _tuning.idleOffset.y);
        return ClampToRadius((Vector2)_body.Owner.position + offset);
    }

    /// <summary>
    /// Вызывается только когда стенд стоит на месте (не движется к цели).
    /// Если игрок ушёл дальше maxRadius — запускаем пружинный возврат.
    /// MovePosition НЕ вызывается — нет конфликта с MoveToTarget.
    /// </summary>
    private void EnforceRadiusLeash()
    {
        var data = _body.Data;
        if (_body.Owner == null || data == null) return;

        float dist = Vector2.Distance(_body.Rigidbody.position, (Vector2)_body.Owner.position);
        if (dist <= data.maxRadius) return;

        // Запустить плавный возврат к позиции за спиной игрока
        _isIdleReturning = true;
        _springVelocity  = Vector2.zero;
        _targetPosition  = GetBehindPlayerPosition();
    }
}
