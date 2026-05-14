using UnityEngine;

/// <summary>
/// Триггерная зона выхода с уровня.
/// Когда игрок входит — считает бонус за время, показывает итоговый экран и паузит игру.
/// Повесить на пустой GameObject с Collider2D (Is Trigger = true).
/// </summary>
public class LevelExit : MonoBehaviour
{
    [Header("Next Scene")]
    [Tooltip("Имя следующей сцены. Оставь пустым — останется на текущей (для теста).")]
    [SerializeField] private string nextScene = "";

    [Header("Time")]
    [Tooltip("Целевое время прохождения уровня в секундах для расчёта бонуса")]
    [SerializeField] private float parTime = 120f;

    [Header("Last Level")]
    [Tooltip("Включи на уровне 5 — показывает 'ИГРА ПРОЙДЕНА!' вместо обычного экрана результатов")]
    [SerializeField] private bool isLastLevel = false;

    [Header("References")]
    [SerializeField] private LevelResultUI resultUI;

    private bool _triggered;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;

        // 1. Берём точное время уровня из ScoreManager (учитывает паузы)
        float elapsed = ScoreManager.Instance != null
            ? ScoreManager.Instance.LevelTime
            : 0f;

        ScoreManager.Instance?.AddTimeBonus(elapsed, parTime);
        ScoreManager.Instance?.StopTimer();

        // 2. Заморозить игру
        GameManager.Instance?.PauseGame();

        // 3. Показать итоги
        if (resultUI != null)
        {
            if (isLastLevel) resultUI.ShowGameComplete(nextScene);
            else             resultUI.ShowFromScoreManager(nextScene);
        }
        else
        {
            Debug.LogWarning("[LevelExit] LevelResultUI не назначен в Inspector!");
        }
    }

    // ──────────────────────────────────────────
    // Редактор — нарисовать зону
    // ──────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);
        var col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        else
            Gizmos.DrawCube(transform.position, Vector3.one);
    }
}
