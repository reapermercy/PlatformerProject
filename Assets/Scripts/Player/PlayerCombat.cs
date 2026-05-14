using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Боевая система персонажа.
/// Комбо-цепочка до 3 ударов, зажатый блок, парирование по таймингу.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки ударов
    // ──────────────────────────────────────────

    [Header("Combo")]
    [SerializeField] private int   comboLength       = 3;
    [SerializeField] private float comboResetTime    = 0.8f;
    [SerializeField] private float comboBufferTime   = 0.2f;

    [Header("Attack Timing (без анимаций)")]
    [Tooltip("Выключи если используешь Animation Events")]
    [SerializeField] private bool  useTimerFallback  = true;
    [SerializeField] private float attackHitDelay    = 0.1f;  // задержка до хитбокса
    [SerializeField] private float attackEndDelay    = 0.35f; // длительность удара

    [Header("Hit Detection")]
    [SerializeField] private Transform  attackPoint;
    [SerializeField] private float      attackRadius  = 0.6f;
    [SerializeField] private LayerMask  enemyLayers;

    [Header("Damage Per Hit")]
    [SerializeField] private int[] comboDamage = { 8, 8, 15 }; // урон 1, 2, 3 удара

    [Header("Knockback Per Hit")]
    [SerializeField] private float[] comboKnockback = { 2f, 2f, 5f };

    // ──────────────────────────────────────────
    // Настройки блока и парри
    // ──────────────────────────────────────────

    [Header("Block")]
    [SerializeField] private float blockDamageMultiplier = 0.3f; // 30% урона проходит при блоке

    [Header("Parry")]
    [SerializeField] private float parryWindow       = 0.15f;  // сек после нажатия W = парри
    [SerializeField] private float parryCooldown     = 0.6f;
    [SerializeField] private float parryInvincibility = 0.4f;  // сек неуязвимости после парри
    [SerializeField] private int   parryCounterDamage = 20;    // контратака при успешном парри

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent<int>  OnComboHit;      // (comboStep)
    public UnityEvent       OnComboFinished;
    public UnityEvent       OnBlock;
    public UnityEvent       OnParrySuccess;
    public UnityEvent       OnParryFail;

    // ──────────────────────────────────────────
    // Приватные поля
    // ──────────────────────────────────────────

    private PlayerStats     _stats;
    private PlayerController _controller;
    private Animator        _anim;

    // Комбо
    private int   _comboStep        = 0;  // текущий удар в цепочке (0 = не начато)
    private float _comboResetTimer  = 0f;
    private float _comboBufferTimer = 0f;
    private bool  _attackBuffered   = false;
    private bool  _attackActive     = false; // анимация удара играет

    // Блок / парри
    private bool  _isBlocking       = false;
    private bool  _parryActive      = false;
    private float _parryTimer       = 0f;
    private float _parryCooldownTimer = 0f;
    private bool  _parryOnCooldown  = false;

    // Защита от двойного урона (useTimerFallback + Animation Event в одном кадре)
    private bool  _hitAppliedThisSwing = false;

    // Переиспользуемый буфер + фильтр (Unity 6 — OverlapCircle с ContactFilter2D)
    private static readonly Collider2D[] s_overlapBuffer = new Collider2D[16];
    private static ContactFilter2D s_overlapFilter = new ContactFilter2D { useTriggers = true };

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _stats      = GetComponent<PlayerStats>();
        _controller = GetComponent<PlayerController>();
        _anim       = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        TickComboReset();
        TickParry();
        TickParryCooldown();
        TryExecuteBufferedAttack();
    }

    // ──────────────────────────────────────────
    // Input callbacks (вызываются из PlayerInputHandler)
    // ──────────────────────────────────────────

    /// <summary>LMB — атака / буфер комбо</summary>
    public void OnAttack()
    {
        if (!_stats.CanAct) return;
        if (_isBlocking)    return;
        // Стенд в ручном управлении — ЛКМ уходит к нему, игрок не атакует
        if (_controller.Context.StandManualControl) return;

        if (_attackActive)
        {
            // Анимация ещё играет — буферизуем следующий удар
            _attackBuffered    = true;
            _comboBufferTimer  = comboBufferTime;
        }
        else
        {
            ExecuteAttack();
        }
    }

    /// <summary>
    /// W нажат — начало блока И попытка парри.
    /// Парри активно только в первые parryWindow секунд.
    /// </summary>
    public void OnBlockStart()
    {
        if (!_stats.CanAct)      return;
        if (_parryOnCooldown)
        {
            // Кулдаун парри — просто блок без парри-окна
            _isBlocking  = true;
            _parryActive = false;
        }
        else
        {
            _isBlocking  = true;
            _parryActive = true;
            _parryTimer  = parryWindow;
        }

        _anim?.SetBool("IsBlocking", true);
        OnBlock?.Invoke();
    }

    /// <summary>W отпущен — конец блока</summary>
    public void OnBlockEnd()
    {
        _isBlocking  = false;
        _parryActive = false;
        _anim?.SetBool("IsBlocking", false);
    }

    // ──────────────────────────────────────────
    // Удар
    // ──────────────────────────────────────────

    private void ExecuteAttack()
    {
        _comboStep++;
        if (_comboStep > comboLength)
            _comboStep = 1;

        _attackActive          = true;
        _comboResetTimer       = comboResetTime;
        _attackBuffered        = false;
        _hitAppliedThisSwing   = false;

        // Триггер анимации: Attack1 / Attack2 / Attack3
        _anim?.SetInteger("ComboStep", _comboStep);
        _anim?.SetTrigger("Attack");

        OnComboHit?.Invoke(_comboStep);

        if (_comboStep == comboLength)
            OnComboFinished?.Invoke();

        if (useTimerFallback)
            StartCoroutine(AttackTimingRoutine());
    }

    private IEnumerator AttackTimingRoutine()
    {
        yield return new WaitForSeconds(attackHitDelay);
        AnimEvent_ApplyHit();
        yield return new WaitForSeconds(attackEndDelay - attackHitDelay);
        AnimEvent_AttackEnd();
    }

    /// <summary>
    /// Вызывается Animation Event в середине анимации удара.
    /// Именно здесь проверяем коллизии — не раньше.
    /// </summary>
    public void AnimEvent_ApplyHit()
    {
        if (_hitAppliedThisSwing) return;
        _hitAppliedThisSwing = true;

        if (attackPoint == null) return;

        s_overlapFilter.SetLayerMask(enemyLayers);
        int count = Physics2D.OverlapCircle(
            attackPoint.position, attackRadius, s_overlapFilter, s_overlapBuffer);

        int damage   = GetComboDamage();
        float knockback = GetComboKnockback();

        for (int i = 0; i < count; i++)
        {
            var hit = s_overlapBuffer[i];
            if (hit == null) continue;

            var handler = hit.GetComponent<DamageHandler>();
            if (handler == null) continue;

            handler.ApplyDamage(damage, DamageSource.Human);

            // Нокбэк — толкаем от игрока
            var enemyRb = hit.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                enemyRb.AddForce(dir * knockback, ForceMode2D.Impulse);
            }
        }
    }

    /// <summary>
    /// Вызывается Animation Event в конце анимации удара.
    /// Разрешает следующий удар из буфера.
    /// </summary>
    public void AnimEvent_AttackEnd()
    {
        _attackActive        = false;
        _hitAppliedThisSwing = false;
    }

    // ──────────────────────────────────────────
    // Буфер атаки
    // ──────────────────────────────────────────

    private void TryExecuteBufferedAttack()
    {
        if (!_attackBuffered || _attackActive) return;

        _comboBufferTimer -= Time.deltaTime;
        if (_comboBufferTimer <= 0f)
        {
            _attackBuffered = false;
            return;
        }

        // Анимация закончилась — выполняем буферизованный удар
        if (!_attackActive)
        {
            _attackBuffered = false;
            ExecuteAttack();
        }
    }

    // ──────────────────────────────────────────
    // Сброс комбо
    // ──────────────────────────────────────────

    private void TickComboReset()
    {
        if (_comboStep == 0) return;

        _comboResetTimer -= Time.deltaTime;
        if (_comboResetTimer <= 0f)
        {
            _comboStep    = 0;
            _attackActive = false;
            _anim?.SetInteger("ComboStep", 0);
        }
    }

    // ──────────────────────────────────────────
    // Парирование
    // ──────────────────────────────────────────

    private void TickParry()
    {
        if (!_parryActive) return;

        _parryTimer -= Time.deltaTime;
        if (_parryTimer <= 0f)
            _parryActive = false; // окно закрылось → теперь просто блок
    }

    private void TickParryCooldown()
    {
        if (!_parryOnCooldown) return;

        _parryCooldownTimer -= Time.deltaTime;
        if (_parryCooldownTimer <= 0f)
            _parryOnCooldown = false;
    }

    /// <summary>
    /// Вызывается DamageHandler ПЕРЕД применением урона.
    /// Возвращает true если урон заблокирован / отпарирован.
    /// </summary>
    public bool TryInterceptDamage(ref int damage, DamageSource source)
    {
        if (!_isBlocking) return false;

        // Парри — урон пришёл пока окно открыто
        if (_parryActive)
        {
            TriggerParrySuccess();
            damage = 0;
            return true;
        }

        // Обычный блок — снижаем урон
        damage = Mathf.RoundToInt(damage * blockDamageMultiplier);
        return false; // урон частично проходит — возвращаем false
    }

    private void TriggerParrySuccess()
    {
        _parryActive      = false;
        _parryOnCooldown  = true;
        _parryCooldownTimer = parryCooldown;

        // Неуязвимость после парри
        _stats.SetInvincible(parryInvincibility);

        // Контратака — наносим урон всем врагам в радиусе
        if (attackPoint != null)
        {
            s_overlapFilter.SetLayerMask(enemyLayers);
            int count = Physics2D.OverlapCircle(
                attackPoint.position, attackRadius * 1.5f, s_overlapFilter, s_overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                var hit = s_overlapBuffer[i];
                if (hit == null) continue;
                hit.GetComponent<DamageHandler>()
                   ?.ApplyDamage(parryCounterDamage, DamageSource.Human);
            }
        }

        _anim?.SetTrigger("Parry");
        OnParrySuccess?.Invoke();
    }

    // ──────────────────────────────────────────
    // Вспомогательные
    // ──────────────────────────────────────────

    private int GetComboDamage()
    {
        int idx = Mathf.Clamp(_comboStep - 1, 0, comboDamage.Length - 1);
        return comboDamage[idx];
    }

    private float GetComboKnockback()
    {
        int idx = Mathf.Clamp(_comboStep - 1, 0, comboKnockback.Length - 1);
        return comboKnockback[idx];
    }

    // ──────────────────────────────────────────
    // Gizmos — видно в редакторе
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }

    // ──────────────────────────────────────────
    // Публичные свойства
    // ──────────────────────────────────────────

    public bool IsBlocking   => _isBlocking;
    public bool IsParrying   => _parryActive;
    public int  ComboStep    => _comboStep;

    /// <summary>
    /// Применить длину комбо из CharacterData.
    /// Вызови вместе с PlayerController.ApplyCharacterStats() и PlayerStats.ApplyCharacterStats().
    /// </summary>
    public void ApplyCharacterStats(CharacterData data)
    {
        if (data == null) return;
        comboLength = data.comboLength;
        Debug.Log($"[PlayerCombat] {data.characterName} | combo {comboLength}");
    }
}
