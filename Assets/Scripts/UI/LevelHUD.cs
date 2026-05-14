using UnityEngine;
using TMPro;

/// <summary>
/// Отображает в HUD номер уровня и текущее время прохождения.
///
/// Настройка в Canvas:
///   - levelLabel  → TMP_Text, например «УРОВЕНЬ 1»
///   - timerLabel  → TMP_Text, например «02:47»
///
/// Обновляется каждый кадр (таймер) и при смене уровня (levelLabel).
/// </summary>
public class LevelHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private TMP_Text timerLabel;

    [Header("Format")]
    [Tooltip("Формат строки уровня. {0} = номер, {1} = название (если задано)")]
    [SerializeField] private string levelFormat = "УРОВЕНЬ {0}";

    private int _lastLevel = -1;

    private void Update()
    {
        // Таймер — каждый кадр
        if (timerLabel != null && ScoreManager.Instance != null)
            timerLabel.text = ScoreManager.Instance.LevelTimeFormatted;

        // Номер уровня — только при изменении
        if (levelLabel != null && GameManager.Instance != null)
        {
            int level = GameManager.Instance.CurrentLevel;
            if (level != _lastLevel)
            {
                _lastLevel = level;
                levelLabel.text = string.Format(levelFormat, level);
            }
        }
    }
}
