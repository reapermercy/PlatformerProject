using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Синглтон подсчёта очков.
/// Убийства, временной бонус, штраф за урон → итог уровня → общий счёт.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Singleton
    // ──────────────────────────────────────────

    public static ScoreManager Instance { get; private set; }

    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Time Bonus")]
    [Tooltip("Макс. бонус за быстрое прохождение")]
    [SerializeField] private int maxTimeBonus = 5000;

    [Header("Damage Penalty")]
    [Tooltip("Очков вычитается за единицу полученного урона")]
    [SerializeField] private int penaltyPerDamage = 10;

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent<int> OnScoreChanged;      // (levelScore)
    public UnityEvent<int> OnTotalScoreChanged;  // (totalScore)

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private int   _totalScore;
    private int   _levelScore;
    private int   _killCount;
    private int   _damagePenaltyGiven;
    private int   _timeBonusGiven;

    // Таймеры
    private float _levelTime;       // секунд на текущем уровне
    private float _totalPlayTime;   // накопленное время за все уровни (из сохранения)
    private bool  _timerRunning;

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
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!_timerRunning) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        _levelTime += Time.deltaTime;
    }

    // ──────────────────────────────────────────
    // API
    // ──────────────────────────────────────────

    /// <summary>Очки за убийство врага</summary>
    public void AddKillPoints(EnemyType type, int basePoints)
    {
        _killCount++;
        _levelScore += basePoints;
        OnScoreChanged?.Invoke(_levelScore);
    }

    /// <summary>Бонус за время прохождения. parTime — целевое время в секундах.</summary>
    public void AddTimeBonus(float timeElapsed, float parTime)
    {
        if (parTime <= 0f) return;

        float ratio = Mathf.Clamp01(1f - (timeElapsed / parTime));
        int bonus   = Mathf.RoundToInt(ratio * maxTimeBonus);

        _timeBonusGiven += bonus;
        _levelScore     += bonus;
        OnScoreChanged?.Invoke(_levelScore);
    }

    /// <summary>Штраф за полученный урон</summary>
    public void AddDamagePenalty(float damageTaken)
    {
        int penalty = Mathf.RoundToInt(damageTaken * penaltyPerDamage);
        _damagePenaltyGiven += penalty;
        _levelScore = Mathf.Max(0, _levelScore - penalty);
        OnScoreChanged?.Invoke(_levelScore);
    }

    /// <summary>Зафиксировать результат уровня → прибавить к общему</summary>
    public void CommitLevelScore()
    {
        _totalScore     += _levelScore;
        _totalPlayTime  += _levelTime;
        OnTotalScoreChanged?.Invoke(_totalScore);
        _levelScore = 0;
        _levelTime  = 0f;
    }

    /// <summary>Сброс очков уровня (рестарт)</summary>
    public void ResetLevelScore()
    {
        _levelScore         = 0;
        _killCount          = 0;
        _damagePenaltyGiven = 0;
        _timeBonusGiven     = 0;
        _levelTime          = 0f;
        OnScoreChanged?.Invoke(_levelScore);
    }

    // ──────────────────────────────────────────
    // Таймер
    // ──────────────────────────────────────────

    /// <summary>Запустить таймер уровня (вызывать при старте уровня)</summary>
    public void StartTimer() => _timerRunning = true;

    /// <summary>Остановить таймер (при завершении уровня / смерти)</summary>
    public void StopTimer()  => _timerRunning = false;

    /// <summary>Форматированное время уровня: MM:SS</summary>
    public static string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    // ──────────────────────────────────────────
    // Сохранение / загрузка
    // ──────────────────────────────────────────

    public void SetTotalScore(int score)       => _totalScore    = score;
    public void SetTotalPlayTime(float seconds) => _totalPlayTime = seconds;

    // ──────────────────────────────────────────
    // Свойства
    // ──────────────────────────────────────────

    public int   TotalScore         => _totalScore;
    public int   LevelScore         => _levelScore;
    public int   KillCount          => _killCount;
    public int   TimeBonusGiven     => _timeBonusGiven;
    public int   DamagePenaltyGiven => _damagePenaltyGiven;
    public float LevelTime          => _levelTime;
    public float TotalPlayTime      => _totalPlayTime + _levelTime; // включая текущий уровень
    public string LevelTimeFormatted => FormatTime(_levelTime);
    public string TotalTimeFormatted => FormatTime(TotalPlayTime);
}
