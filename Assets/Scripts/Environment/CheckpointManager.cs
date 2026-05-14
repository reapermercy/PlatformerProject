using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Синглтон, хранящий позицию последнего активированного чекпоинта.
/// Респавн-точка сбрасывается при смене сцены (обычная Metroidvania-логика).
///
/// Если CheckpointManager-а нет на сцене — Checkpoint просто не сохранит точку,
/// никаких ошибок не будет.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    [Header("Default")]
    [Tooltip("Точка респавна по умолчанию (если ни один чекпоинт ещё не активирован)")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Header("Respawn Behavior")]
    [Tooltip("При смерти игрока — автоматически респавнить (false = перезагружать сцену)")]
    [SerializeField] private bool autoRespawnOnDeath = false;

    [Tooltip("HP при респавне (0..1 от maxHP)")]
    [SerializeField, Range(0.1f, 1f)] private float respawnHPPercent = 1f;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private bool    _hasRespawnPoint;
    private Vector2 _respawnPosition;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Чекпоинты привязаны к сцене — не DontDestroyOnLoad
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Новая сцена — чекпоинт сброшен, используем default
        _hasRespawnPoint = false;
    }

    // ──────────────────────────────────────────
    // Публичное API
    // ──────────────────────────────────────────

    /// <summary>Запомнить позицию респавна. Вызывается из Checkpoint.Activate.</summary>
    public void SetRespawnPoint(Vector2 position)
    {
        _respawnPosition = position;
        _hasRespawnPoint = true;
    }

    /// <summary>Текущая позиция респавна (или default, если не задано).</summary>
    public Vector2 RespawnPosition
    {
        get
        {
            if (_hasRespawnPoint) return _respawnPosition;
            if (defaultSpawnPoint != null) return defaultSpawnPoint.position;
            return Vector2.zero;
        }
    }

    public bool HasRespawnPoint => _hasRespawnPoint;

    /// <summary>
    /// Респавнить игрока на последнем чекпоинте.
    /// Вызывается из GameManager при смерти (если autoRespawnOnDeath = true).
    /// Возвращает true если респавн успешен.
    /// </summary>
    public bool RespawnPlayer(PlayerStats stats)
    {
        if (stats == null) return false;

        // Переместить
        stats.transform.position = RespawnPosition;

        // Сбросить скорость
        var rb = stats.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Оживить с частью HP
        int hp = Mathf.Max(1, Mathf.RoundToInt(stats.MaxHP * respawnHPPercent));
        stats.Revive(hp);

        return true;
    }

    public bool AutoRespawnOnDeath => autoRespawnOnDeath;
}
