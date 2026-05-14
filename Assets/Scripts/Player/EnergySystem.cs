using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Универсальная система энергии для Хамона (Джонатан, Джозеф) и Спина (Джонни).
///
/// Хамон  → RechargeCondition = OnBlock  (зарядка пока игрок держит блок)
/// Спин   → RechargeCondition = OnMove   (зарядка при движении)
/// Пассив → RechargeCondition = Passive  (всегда заряжается)
///
/// Настройки задаются прямо в инспекторе или через ApplyConfig() из CharacterData.
/// </summary>
public enum EnergyRechargeCondition { Passive, OnBlock, OnMove }

public class EnergySystem : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Config (переопределяется из CharacterData)")]
    [SerializeField] private float maxEnergy     = 100f;
    [SerializeField] private float rechargeRate  = 10f;    // единиц/сек при выполнении условия
    [SerializeField] private EnergyRechargeCondition rechargeCondition = EnergyRechargeCondition.Passive;

    [Header("References (авто-поиск если пусто)")]
    [SerializeField] private PlayerCombat     playerCombat;
    [SerializeField] private PlayerController playerController;

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    /// <summary>(current, max) — подключить к EnergyBar.OnEnergyChanged</summary>
    public UnityEvent<float, float> OnEnergyChanged;
    public UnityEvent               OnEnergyEmpty;
    public UnityEvent               OnEnergyFull;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private float _current;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _current = maxEnergy;

        if (!playerCombat)
            playerCombat = GetComponentInParent<PlayerCombat>();
        if (!playerController)
            playerController = GetComponentInParent<PlayerController>();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        TickRecharge();
    }

    // ──────────────────────────────────────────
    // Публичное API
    // ──────────────────────────────────────────

    /// <summary>
    /// Потратить amount энергии.
    /// Возвращает true если успешно, false если энергии недостаточно.
    /// </summary>
    public bool Consume(float amount)
    {
        if (_current < amount) return false;
        SetEnergy(_current - amount);
        return true;
    }

    /// <summary>Добавить энергию (восполнение от предметов / способностей).</summary>
    public void Add(float amount) => SetEnergy(_current + amount);

    /// <summary>Проверить наличие нужного количества энергии без траты.</summary>
    public bool HasEnergy(float amount) => _current >= amount;

    /// <summary>
    /// Применить конфиг из CharacterData.
    /// Вызывается автоматически из PlayerStats.Start() через ApplyCharacterStats.
    /// </summary>
    public void ApplyConfig(float max, float rate, EnergyRechargeCondition condition)
    {
        maxEnergy        = max;
        rechargeRate     = rate;
        rechargeCondition = condition;
        SetEnergy(max);
    }

    // ──────────────────────────────────────────
    // Свойства
    // ──────────────────────────────────────────

    public float CurrentEnergy => _current;
    public float MaxEnergy     => maxEnergy;
    public float Percent       => maxEnergy > 0f ? _current / maxEnergy : 0f;

    // ──────────────────────────────────────────
    // Внутренняя логика
    // ──────────────────────────────────────────

    private void TickRecharge()
    {
        if (_current >= maxEnergy) return;

        bool canRecharge = rechargeCondition switch
        {
            EnergyRechargeCondition.OnBlock => playerCombat     != null && playerCombat.IsBlocking,
            EnergyRechargeCondition.OnMove  => playerController != null &&
                                               Mathf.Abs(playerController.Velocity.x) > 0.1f,
            _                               => true  // Passive — всегда
        };

        if (canRecharge)
            SetEnergy(_current + rechargeRate * Time.deltaTime);
    }

    private void SetEnergy(float value)
    {
        bool wasEmpty = _current <= 0f;
        bool wasFull  = _current >= maxEnergy;

        _current = Mathf.Clamp(value, 0f, maxEnergy);

        OnEnergyChanged?.Invoke(_current, maxEnergy);

        if (_current <= 0f && !wasEmpty) OnEnergyEmpty?.Invoke();
        if (_current >= maxEnergy && !wasFull) OnEnergyFull?.Invoke();
    }
}
