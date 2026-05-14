using UnityEngine;

/// <summary>
/// Компонент на корневом GameObject каждой игровой сцены (уровня 1-5).
/// Сообщает GameManager и ScoreManager о том, на каком уровне мы находимся,
/// и запускает таймер прохождения.
///
/// Настройка: добавить на пустой GameObject "LevelManager" в каждой сцене,
/// выставить levelNumber (1-5) в инспекторе.
/// </summary>
public class LevelStartup : MonoBehaviour
{
    [Header("Level Info")]
    [Tooltip("Номер уровня (1-5)")]
    [SerializeField] private int levelNumber = 1;

    [Tooltip("Отображаемое название уровня, например «Особняк Джеста»")]
    [SerializeField] private string levelDisplayName = "";

    private void Start()
    {
        // Сообщаем менеджерам о текущем уровне
        GameManager.Instance?.SetCurrentLevel(levelNumber);

        // Сброс очков уровня и запуск таймера
        ScoreManager.Instance?.ResetLevelScore();
        ScoreManager.Instance?.StartTimer();

        Debug.Log($"[LevelStartup] Уровень {levelNumber}" +
                  (string.IsNullOrEmpty(levelDisplayName) ? "" : $" — {levelDisplayName}"));
    }

    // ──────────────────────────────────────────
    // Свойства (читаются HUD)
    // ──────────────────────────────────────────

    public int    LevelNumber      => levelNumber;
    public string LevelDisplayName => levelDisplayName;
}
