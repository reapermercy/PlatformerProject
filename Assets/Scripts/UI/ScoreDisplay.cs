using UnityEngine;
using TMPro;

/// <summary>
/// Отображает текущий счёт (LevelScore + TotalScore) в TMP_Text.
/// Подписывается на ScoreManager.OnScoreChanged автоматически.
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private string   prefix = "Счёт: ";

    private void Start()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged.AddListener(UpdateScore);

        Refresh();
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged.RemoveListener(UpdateScore);
    }

    private void UpdateScore(int levelScore)
    {
        int total = ScoreManager.Instance != null ? ScoreManager.Instance.TotalScore : 0;
        if (scoreText != null)
            scoreText.text = prefix + (total + levelScore);
    }

    private void Refresh()
    {
        if (ScoreManager.Instance != null)
            UpdateScore(ScoreManager.Instance.LevelScore);
    }
}
