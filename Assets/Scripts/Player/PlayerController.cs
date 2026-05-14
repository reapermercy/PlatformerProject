using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Основной контроллер персонажа игрока.
/// Движение, прыжок, приседание, карабканье, блок.
/// Использует новый Unity Input System.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки в инспекторе
    // ──────────────────────────────────────────

    [Header("Movement")]
    [SerializeField] private float moveSpeed       = 7f;
    [SerializeField] private float crouchSpeedMult = 0.5f; // скорость присев
    [SerializeField] private float climbSpeed      = 4f;

    [Header("Jump")]
    [SerializeField] private float jumpForce          = 14f;
    [SerializeField] private float fallMultiplier     = 2.5f; // тяжёлое падение
    [SerializeField] private float lowJumpMultiplier  = 2f;   // короткий прыжок
    [SerializeField] private int   maxExtraJumps      = 0;    // 0 = только земля

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float     groundCheckRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Ladder Check")]
    [SerializeField] private Transform ladderCheck;
    [SerializeField] private float     ladderCheckRadius = 0.3f;
    [SerializeField] private LayerMask ladderLayer;

    [Header("Crouch")]
    [SerializeField] private CapsuleCollider2D capsuleCollider;
    [SerializeField] private Vector2 standingColliderSize  = new Vector2(0.5f, 1.0f);
    [SerializeField] private Vector2 crouchingColliderSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 standingColliderOffset  = new Vector2(0f, 0f);
    [SerializeField] private Vector2 crouchingColliderOffset = new Vector2(0f, -0.35f);
    [Tooltip("Дочерний объект со SpriteRenderer — двигается вместе с коллайдером при приседе")]
    [SerializeField] private Transform spriteRoot;
    [SerializeField] private Transform ceilingCheck;
    [SerializeField] private float     ceilingCheckRadius = 0.1f;

    // ──────────────────────────────────────────
    // Приватные поля
    // ──────────────────────────────────────────

    private Rigidbody2D   _rb;
    private Animator      _anim;
    private PlayerContext _ctx;
    private PlayerStats   _stats;
    private PlayerCombat  _combat;
    private StatusEffectController _status;

    private Vector2 _moveInput;
    private bool    _jumpBuffered;      // буфер прыжка (нажали чуть раньше земли)
    private float   _jumpBufferTimer;
    private const float JumpBufferTime = 0.12f;

    private bool  _coyoteAvailable;    // coyote time (прыжок после края)
    private float _coyoteTimer;
    private const float CoyoteTime = 0.12f;

    private int _extraJumpsLeft;

    private bool    _facingRight = true;
    private Vector3 _standingSpriteLocalPos;   // кешируем в Awake, восстанавливаем при вставании

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _anim   = GetComponentInChildren<Animator>();
        _stats  = GetComponent<PlayerStats>();
        _combat = GetComponent<PlayerCombat>();
        _status = GetComponent<StatusEffectController>();
        _ctx    = new PlayerContext();

        // Автоматически найти коллайдер если не назначен
        if (capsuleCollider == null)
            capsuleCollider = GetComponent<CapsuleCollider2D>();

        // Кешируем стартовую позицию спрайта (до любого приседания)
        if (spriteRoot != null)
            _standingSpriteLocalPos = spriteRoot.localPosition;
    }

    private void Start()
    {
        if (_stats != null)
            _stats.OnDeath.AddListener(() => _anim?.SetTrigger("Die"));
    }

    private void Update()
    {
        UpdateContext();
        if (_stats != null && !_stats.CanAct) return; // мёртв — всё остальное не обновляем
        HandleJumpBuffer();
        HandleCoyote();
        HandleFallGravity();
        UpdateAnimator();
    }

    private void LateUpdate()
    {
        // Пивот спрайта внизу — держим его на нижней грани коллайдера.
        // Это компенсирует любое смещение offset/size без хардкода дельты.
        if (spriteRoot == null || capsuleCollider == null) return;
        spriteRoot.localPosition = new Vector3(
            _standingSpriteLocalPos.x,
            capsuleCollider.offset.y - capsuleCollider.size.y * 0.5f,
            _standingSpriteLocalPos.z);
    }

    private void FixedUpdate()
    {
        if (_stats != null && !_stats.CanAct)
        {
            // Мёртв — останавливаем горизонталь, гравитация продолжает работать
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        if (_ctx.IsClimbing)
            ApplyClimbMovement();
        else
            ApplyHorizontalMovement();
    }

    // ──────────────────────────────────────────
    // Input System callbacks
    // Привязываются через PlayerInput компонент
    // ──────────────────────────────────────────

    /// <summary>WASD / стик — горизонталь + вертикаль для лестниц</summary>
    public void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    /// <summary>Пробел — прыжок</summary>
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        if (_stats != null && !_stats.CanAct) return;
        if (_status != null && _status.IsIncapacitated) return;

        _jumpBuffered    = true;
        _jumpBufferTimer = JumpBufferTime;
        TryJump();
    }

    /// <summary>
    /// W — контекстная кнопка.
    /// У лестницы → начать карабкаться.
    /// Иначе → прыжок (дублирует Space).
    /// </summary>
    public void OnWAction(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        if (_stats != null && !_stats.CanAct) return;

        if (_ctx.IsNearLadder)
        {
            StartClimbing();
            return;
        }

        // Прыжок — то же самое что Space
        _jumpBuffered    = true;
        _jumpBufferTimer = JumpBufferTime;
        TryJump();
    }

    /// <summary>ЛКМ — атака / комбо</summary>
    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        if (_stats != null && !_stats.CanAct) return;
        if (_status != null && _status.IsIncapacitated) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        _combat?.OnAttack();
    }

    /// <summary>Ctrl — присесть. На лестнице — слезть.</summary>
    public void OnCrouch(InputAction.CallbackContext ctx)
    {
        if (!ctx.started && !ctx.canceled) return;

        if (_ctx.IsClimbing && ctx.started)
        {
            StopClimbing();
            return;
        }

        SetCrouch(ctx.started);
    }

    // ──────────────────────────────────────────
    // Движение
    // ──────────────────────────────────────────

    private void ApplyHorizontalMovement()
    {
        // Stun / Freeze — полная остановка
        if (_status != null && _status.IsIncapacitated)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        if (_ctx.IsBlocking)
        {
            // Во время блока — стоим, но можно чуть двигаться (опционально)
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        float speed = _ctx.IsCrouching ? moveSpeed * crouchSpeedMult : moveSpeed;

        // Slow
        if (_status != null) speed *= _status.SlowMultiplier;

        float targetX = _moveInput.x * speed;

        _rb.linearVelocity = new Vector2(targetX, _rb.linearVelocity.y);

        // Разворот спрайта
        if (targetX > 0.01f && !_facingRight) Flip();
        if (targetX < -0.01f && _facingRight) Flip();
    }

    private void ApplyClimbMovement()
    {
        // На лестнице — двигаемся по обеим осям, отключаем гравитацию
        _rb.gravityScale = 0f;
        _rb.linearVelocity = new Vector2(_moveInput.x * climbSpeed, _moveInput.y * climbSpeed);

        // Сошли с лестницы — прыжок или уход в сторону
        if (!_ctx.IsNearLadder && Mathf.Abs(_moveInput.x) > 0.1f)
            StopClimbing();
    }

    // ──────────────────────────────────────────
    // Прыжок
    // ──────────────────────────────────────────

    private void TryJump()
    {
        bool canJump = (_ctx.IsGrounded || _coyoteAvailable) && !_ctx.IsClimbing;
        bool canExtraJump = _extraJumpsLeft > 0 && !_ctx.IsGrounded;

        if (canJump)
        {
            ExecuteJump();
            _coyoteAvailable = false;
        }
        else if (canExtraJump)
        {
            ExecuteJump();
            _extraJumpsLeft--;
        }
    }

    private void ExecuteJump()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        _jumpBuffered    = false;
        _jumpBufferTimer = 0f;
    }

    /// <summary>
    /// Разная гравитация при падении и коротком прыжке.
    /// Делает прыжок более отзывчивым.
    /// </summary>
    private void HandleFallGravity()
    {
        if (_ctx.IsClimbing) return;

        if (_rb.linearVelocity.y < 0)
        {
            _rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (_rb.linearVelocity.y > 0)
        {
            // Кнопка прыжка не зажата → короткий прыжок (Space или W)
            bool jumpHeld = Keyboard.current != null &&
                            (Keyboard.current.spaceKey.isPressed ||
                             Keyboard.current.wKey.isPressed);
            if (!jumpHeld)
                _rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }

    private void HandleJumpBuffer()
    {
        if (!_jumpBuffered) return;

        _jumpBufferTimer -= Time.deltaTime;
        if (_jumpBufferTimer <= 0f)
        {
            _jumpBuffered = false;
            return;
        }

        // Приземлились в буферном окне → прыгаем
        if (_ctx.IsGrounded)
            TryJump();
    }

    private void HandleCoyote()
    {
        if (_ctx.IsGrounded)
        {
            _coyoteAvailable  = true;
            _coyoteTimer      = CoyoteTime;
            _extraJumpsLeft   = maxExtraJumps;
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
            if (_coyoteTimer <= 0f)
                _coyoteAvailable = false;
        }
    }

    // ──────────────────────────────────────────
    // Карабканье
    // ──────────────────────────────────────────

    private void StartClimbing()
    {
        _ctx.IsClimbing  = true;
        _rb.gravityScale = 0f;
    }

    private void StopClimbing()
    {
        _ctx.IsClimbing  = false;
        _rb.gravityScale = 1f;
    }

    // ──────────────────────────────────────────
    // Приседание
    // ──────────────────────────────────────────

    private void SetCrouch(bool crouch)
    {
        // Нельзя встать если над головой потолок
        if (!crouch && IsCeilingAbove()) return;

        _ctx.IsCrouching = crouch;

        if (capsuleCollider != null)
        {
            capsuleCollider.size   = crouch ? crouchingColliderSize   : standingColliderSize;
            capsuleCollider.offset = crouch ? crouchingColliderOffset : standingColliderOffset;
        }

    }

    private bool IsCeilingAbove()
    {
        if (ceilingCheck == null) return false;
        return Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, groundLayer);
    }

    // ──────────────────────────────────────────
    // Блок
    // ──────────────────────────────────────────

    private void SetBlock(bool blocking)
    {
        _ctx.IsBlocking = blocking;
        if (blocking) _combat?.OnBlockStart();
        else          _combat?.OnBlockEnd();
    }

    // ──────────────────────────────────────────
    // Контекст (обновляется каждый кадр)
    // ──────────────────────────────────────────

    private void UpdateContext()
    {
        _ctx.IsGrounded  = CheckGrounded();
        _ctx.IsNearLadder = CheckNearLadder();

        // Если вышли из зоны лестницы во время карабканья
        if (_ctx.IsClimbing && !_ctx.IsNearLadder)
            StopClimbing();
    }

    private bool CheckGrounded()
    {
        if (groundCheck == null) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private bool CheckNearLadder()
    {
        if (ladderCheck == null) return false;
        return Physics2D.OverlapCircle(ladderCheck.position, ladderCheckRadius, ladderLayer);
    }

    // ──────────────────────────────────────────
    // Разворот
    // ──────────────────────────────────────────

    private void Flip()
    {
        _facingRight = !_facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // ──────────────────────────────────────────
    // Аниматор
    // ──────────────────────────────────────────

    private void UpdateAnimator()
    {
        if (_anim == null) return;

        _anim.SetFloat("Speed",    Mathf.Abs(_moveInput.x));
        _anim.SetBool("IsGrounded", _ctx.IsGrounded);
        _anim.SetBool("IsCrouching", _ctx.IsCrouching);
        _anim.SetBool("IsClimbing",  _ctx.IsClimbing);
        _anim.SetBool("IsBlocking",  _ctx.IsBlocking);
        _anim.SetFloat("VelocityY", _rb.linearVelocity.y);
    }

    // ──────────────────────────────────────────
    // Публичный доступ для других скриптов
    // ──────────────────────────────────────────

    public PlayerContext Context => _ctx;
    public bool FacingRight      => _facingRight;
    public Vector2 Velocity      => _rb.linearVelocity;

    /// <summary>
    /// Применить скорость и силу прыжка из CharacterData.
    /// Вызови из менеджера уровня вместе с PlayerStats.ApplyCharacterStats().
    /// Комбо-длину отдельно применяй через PlayerCombat.ApplyCharacterStats().
    /// </summary>
    public void ApplyCharacterStats(CharacterData data)
    {
        if (data == null) return;
        moveSpeed = data.moveSpeed;
        jumpForce = data.jumpForce;
        Debug.Log($"[PlayerController] {data.characterName} | speed {moveSpeed}, jump {jumpForce}");
    }
}
