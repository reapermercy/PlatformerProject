using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Экран результатов уровня с анимированным счётчиком.
/// Вызывается из LevelExit после добавления всех бонусов/штрафов.
/// </summary>
public class LevelResultUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Stats Text")]
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text timeBonusText;
    [SerializeField] private TMP_Text damagePenaltyText;
    [SerializeField] private TMP_Text levelTotalText;
    [SerializeField] private TMP_Text grandTotalText;

    [Header("Animation")]
    [SerializeField] private float countDuration  = 0.8f;
    [SerializeField] private float delayBetween   = 0.25f;

    [Header("Buttons")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Game Complete (только уровень 5)")]
    [Tooltip("TMP_Text заголовка над таблицей результатов")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private string   gameCompleteHeader = "ИГРА ПРОЙДЕНА!";
    [SerializeField] private string   normalHeader       = "РЕЗУЛЬТАТЫ УРОВНЯ";

    [Header("Settings")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("References")]
    [SerializeField] private SaveSlotUI saveSlotUI;

    // Запоминаем следующую сцену от LevelExit
    private string _nextScene;

    // Защита от двойного CommitLevelScore (ShowGameComplete + OnMainMenu)
    private bool _scoreCommitted;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        panel?.SetActive(false);

        // Дефолтный заголовок
        if (headerText != null) headerText.text = normalHeader;

        nextLevelButton?.onClick.AddListener(OnNextLevel);
        retryButton?.onClick.AddListener(OnRetry);
        saveButton?.onClick.AddListener(OnSave);
        mainMenuButton?.onClick.AddListener(OnMainMenu);
    }

    // ──────────────────────────────────────────
    // Публичное API
    // ──────────────────────────────────────────

    /// <summary>
    /// Вызвать из LevelExit после ScoreManager.AddTimeBonus / AddDamagePenalty.
    /// nextScene — имя следующей сцены (пустая строка = перезагрузить текущую).
    /// </summary>
    public void ShowFromScoreManager(string nextScene = "")
    {
        if (ScoreManager.Instance == null) return;

        _nextScene      = nextScene;
        _scoreCommitted = false;

        if (headerText != null) headerText.text = normalHeader;

        int kills      = ScoreManager.Instance.KillCount;
        int timeBonus  = ScoreManager.Instance.TimeBonusGiven;
        int penalty    = ScoreManager.Instance.DamagePenaltyGiven;
        int levelTotal = ScoreManager.Instance.LevelScore;
        int grandTotal = ScoreManager.Instance.TotalScore + levelTotal;

        ShowResults(kills, timeBonus, penalty, levelTotal, grandTotal);
    }

    /// <summary>
    /// Вызывается LevelExit на уровне 5 вместо ShowFromScoreManager.
    /// Меняет заголовок, скрывает кнопки "Следующий" и "Повторить",
    /// фиксирует итоговый счёт.
    /// </summary>
    public void ShowGameComplete(string menuScene = "MainMenu")
    {
        if (ScoreManager.Instance == null) return;

        _nextScene = menuScene;

        if (headerText != null) headerText.text = gameCompleteHeader;

        // Финальный экран — только кнопки "Сохранить" и "В главное меню"
        nextLevelButton?.gameObject.SetActive(false);
        retryButton?.gameObject.SetActive(false);

        // Фиксируем счёт до ShowResults чтобы grandTotal = TotalScore (включая текущий уровень)
        ScoreManager.Instance.CommitLevelScore();
        _scoreCommitted = true;

        int kills      = ScoreManager.Instance.KillCount;
        int timeBonus  = ScoreManager.Instance.TimeBonusGiven;
        int penalty    = ScoreManager.Instance.DamagePenaltyGiven;
        int levelTotal = ScoreManager.Instance.LevelScore;
        int grandTotal = ScoreManager.Instance.TotalScore;

        ShowResults(kills, timeBonus, penalty, levelTotal, grandTotal);
    }

    /// <summary>Показать результаты напрямую.</summary>
    public void ShowResults(int kills, int timeBonus, int damagePenalty, int levelTotal, int grandTotal)
    {
        panel?.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(AnimateResults(kills, timeBonus, damagePenalty, levelTotal, grandTotal));
    }

    public void Hide()
    {
        panel?.SetActive(false);
    }

    // ──────────────────────────────────────────
    // Кнопки
    // ──────────────────────────────────────────

    private void OnNextLevel()
    {
        ScoreManager.Instance?.CommitLevelScore();
        GameManager.Instance?.ResumeGame();

        string target = string.IsNullOrEmpty(_nextScene)
            ? SceneManager.GetActiveScene().name
            : _nextScene;

        GameManager.Instance?.LoadScene(target);
    }

    private void OnRetry()
    {
        ScoreManager.Instance?.ResetLevelScore();
        GameManager.Instance?.ResumeGame();
        GameManager.Instance?.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnSave()
    {
        saveSlotUI?.Show(SaveSlotUI.PanelMode.Save);
    }

    private void OnMainMenu()
    {
        // ShowGameComplete уже вызвал CommitLevelScore — не дублируем
        if (!_scoreCommitted)
        {
            ScoreManager.Instance?.CommitLevelScore();
            _scoreCommitted = true;
        }

        GameManager.Instance?.ResumeGame();
        GameManager.Instance?.LoadScene(mainMenuScene);  // всегда в главное меню
    }

    // ──────────────────────────────────────────
    // Анимация
    // ──────────────────────────────────────────

    private IEnumerator AnimateResults(int kills, int timeBonus, int penalty, int total, int grand)
    {
        if (killsText         != null) killsText.text         = "0";
        if (timeBonusText     != null) timeBonusText.text     = "0";
        if (damagePenaltyText != null) damagePenaltyText.text = "0";
        if (levelTotalText    != null) levelTotalText.text    = "0";
        if (grandTotalText    != null) grandTotalText.text    = "0";

        yield return AnimateCounter(killsText,         0, kills);
        yield return new WaitForSecondsRealtime(delayBetween);

        yield return AnimateCounter(timeBonusText,     0, timeBonus);
        yield return new WaitForSecondsRealtime(delayBetween);

        yield return AnimateCounter(damagePenaltyText, 0, -penalty);
        yield return new WaitForSecondsRealtime(delayBetween);

        yield return AnimateCounter(levelTotalText,    0, total);
        yield return new WaitForSecondsRealtime(delayBetween);

        yield return AnimateCounter(grandTotalText,    0, grand);
    }

    private IEnumerator AnimateCounter(TMP_Text text, int from, int to)
    {
        if (text == null) yield break;

        float elapsed = 0f;
        while (elapsed < countDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / countDuration);
            text.text = Mathf.RoundToInt(Mathf.Lerp(from, to, t)).ToString();
            yield return null;
        }

        text.text = to.ToString();
    }
}
