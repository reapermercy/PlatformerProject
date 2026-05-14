using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Источник правды по здоровью для персонажа И его стенда.
/// HP общее — урон стенду = урон персонажу.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки в инспекторе
    // ──────────────────────────────────────────

    // Если задан — характеристики берутся из CharacterData при вызове ApplyCharacterStats().
    // Сейчас не вызывается автоматически; подключи когда будет нужно.
    [Header("Character (optional)")]
    [SerializeField] private CharacterData characterData;

    [Header("Health")]
    [SerializeField] private int maxHP = 100;

    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 0.5f; // сек после получения урона

    [Header("Death")]
    [SerializeField] private float deathDelay = 1.2f; // задержка перед сценой смерти

    // ──────────────────────────────────────────
    // Events — подписываются HUD, ScoreManager, аниматор
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent<int, int>   OnHealthChanged;  // (currentHP, maxHP)
    public UnityEvent<int>        OnDamageTaken;    // (amount)
    public UnityEvent             OnDeath;
    public UnityEvent             OnRevive;

    // ──────────────────────────────────────────
    // Приватные поля
    // ──────────────────────────────────────────

    private int   _currentHP;
    private bool  _isDead;
    private bool  _isInvincible;
    private float _invincibilityTimer;

    // Счётчик для ScoreManager — сколько урона получил за уровень
    private int _damageTakenThisLevel;

    private Coroutine _deathCoroutine;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _currentHP = maxHP;
    }

    private void Start()
    {
        // GameManager перекрывает инспектор — если персонаж выбран в меню, берём оттуда
        if (GameManager.Instance?.SelectedCharacter != null)
            characterData = GameManager.Instance.SelectedCharacter;

        if (characterData == null) return;

        ApplyCharacterStats(characterData);
        GetComponent<PlayerController>()?.ApplyCharacterStats(characterData);
        GetComponent<PlayerCombat>()?.ApplyCharacterStats(characterData);

        // Применяем конфиг энергии
        var energySystems = GetComponents<EnergySystem>();
        foreach (var es in energySystems)
        {
            // Первый EnergySystem на объекте — Хамон, второй — Спин (если есть)
            // Конкретная привязка делается через порядок компонентов или отдельный тег
        }

        // Прямое применение через CharacterData.hamonConfig / spinConfig
        ApplyEnergyConfig(characterData);
    }

    private void ApplyEnergyConfig(CharacterData data)
    {
        var energySystems = GetComponents<EnergySystem>();
        int idx = 0;

        if (data.hamonConfig != null && data.hamonConfig.enabled && idx < energySystems.Length)
        {
            energySystems[idx].ApplyConfig(
                data.hamonConfig.maxEnergy,
                data.hamonConfig.rechargeRate,
                data.hamonConfig.rechargeCondition);
            idx++;
        }

        if (data.spinConfig != null && data.spinConfig.enabled && idx < energySystems.Length)
        {
            energySystems[idx].ApplyConfig(
                data.spinConfig.maxEnergy,
                data.spinConfig.rechargeRate,
                data.spinConfig.rechargeCondition);
        }
    }

    private void Update()
    {
        if (_isInvincible)
        {
            _invincibilityTimer -= Time.deltaTime;
            if (_invincibilityTimer <= 0f)
                _isInvincible = false;
        }
    }

    // ──────────────────────────────────────────
    // Урон
    // ──────────────────────────────────────────

    /// <summary>
    /// Принять урон. Вызывается через DamageHandler —
    /// тот уже проверил, может ли источник вообще нанести урон.
    /// </summary>
    public void TakeDamage(int amount, DamageSource source = DamageSource.Script)
    {
        if (_isDead || _isInvincible || amount <= 0) return;

        // Блок / парирование перехватывает урон
        var combat = GetComponent<PlayerCombat>();
        if (combat != null && combat.TryInterceptDamage(ref amount, source)) return;

        _currentHP = Mathf.Max(0, _currentHP - amount);
        _damageTakenThisLevel += amount;

        OnDamageTaken?.Invoke(amount);
        OnHealthChanged?.Invoke(_currentHP, maxHP);

        StartInvincibility();

        if (_currentHP <= 0)
            Die();
    }

    /// <summary>
    /// Урон без неуязвимости и без триггера смерти.
    /// Для DoT эффектов, яда, хамон-отдачи.
    /// </summary>
    public void TakeDamageRaw(int amount)
    {
        if (_isDead || amount <= 0) return;

        _currentHP = Mathf.Max(0, _currentHP - amount);
        _damageTakenThisLevel += amount;

        OnDamageTaken?.Invoke(amount);
        OnHealthChanged?.Invoke(_currentHP, maxHP);

        if (_currentHP <= 0)
            Die();
    }

    // ──────────────────────────────────────────
    // Лечение
    // ──────────────────────────────────────────

    public void Heal(int amount)
    {
        if (_isDead || amount <= 0) return;

        _currentHP = Mathf.Min(maxHP, _currentHP + amount);
        OnHealthChanged?.Invoke(_currentHP, maxHP);
    }

    public void HealToFull()
    {
        Heal(maxHP);
    }

    // ──────────────────────────────────────────
    // Смерть / воскрешение
    // ──────────────────────────────────────────

    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        Debug.Log("[PlayerStats] Player died — CanAct = false, input blocked.");
        OnDeath?.Invoke();

        // Даём аниматору и эффектам отыграть, потом — GameManager
        _deathCoroutine = StartCoroutine(DeathDelayRoutine());
    }

    private IEnumerator DeathDelayRoutine()
    {
        yield return new WaitForSeconds(deathDelay);
        NotifyGameManagerDeath();
    }

    private void NotifyGameManagerDeath()
    {
        GameManager.Instance?.OnPlayerDeath();
    }

    private void OnDestroy()
    {
        if (_deathCoroutine != null)
            StopCoroutine(_deathCoroutine);
    }

    /// <summary>Воскресить — например у чекпоинта или через способность стенда.</summary>
    public void Revive(int hpAmount)
    {
        if (!_isDead) return;

        _isDead    = false;
        _currentHP = Mathf.Clamp(hpAmount, 1, maxHP);

        OnHealthChanged?.Invoke(_currentHP, maxHP);
        OnRevive?.Invoke();
    }

    // ──────────────────────────────────────────
    // Неуязвимость
    // ──────────────────────────────────────────

    private void StartInvincibility()
    {
        _isInvincible       = true;
        _invincibilityTimer = invincibilityDuration;
    }

    /// <summary>Внешний вызов — например, при активации способности.</summary>
    public void SetInvincible(float duration)
    {
        _isInvincible       = true;
        _invincibilityTimer = Mathf.Max(_invincibilityTimer, duration);
    }

    // ──────────────────────────────────────────
    // Уровневый счётчик урона → ScoreManager
    // ──────────────────────────────────────────

    /// <summary>Вызывается ScoreManager в конце уровня.</summary>
    public int GetAndResetLevelDamage()
    {
        int val = _damageTakenThisLevel;
        _damageTakenThisLevel = 0;
        return val;
    }

    // ──────────────────────────────────────────
    // Публичные свойства
    // ──────────────────────────────────────────

    public int  CurrentHP      => _currentHP;
    public int  MaxHP          => maxHP;
    public bool IsDead         => _isDead;
    public bool IsInvincible   => _isInvincible;
    public bool CanAct         => !_isDead;

    /// <summary>Процент HP — удобно для HUD и AI.</summary>
    public float HPPercent => (float)_currentHP / maxHP;

    /// <summary>
    /// Применить характеристики из CharacterData (HP, скорость и т.д.).
    /// Вызови из Awake() или из менеджера уровня, когда персонаж выбран.
    /// PlayerController.ApplyCharacterStats() применяет скорость и комбо отдельно.
    /// </summary>
    public void ApplyCharacterStats(CharacterData data = null)
    {
        var src = data ?? characterData;
        if (src == null) return;

        maxHP      = src.maxHP;
        _currentHP = maxHP;
        OnHealthChanged?.Invoke(_currentHP, maxHP);

        Debug.Log($"[PlayerStats] Применены характеристики: {src.characterName} | HP {maxHP}");
    }

    public CharacterData CharacterData => characterData;
}
