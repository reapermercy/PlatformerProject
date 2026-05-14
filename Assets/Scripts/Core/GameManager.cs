using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Синглтон игрового менеджера.
/// Ссылки на менеджеры, пауза, переход сцен, обработка смерти.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Singleton
    // ──────────────────────────────────────────

    public static GameManager Instance { get; private set; }

    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Scene Transition")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private CanvasGroup fadeOverlay; // чёрный UI overlay для fade

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent OnGamePaused;
    public UnityEvent OnGameResumed;
    public UnityEvent OnPlayerDied;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private bool          _isPaused;
    private bool          _isTransitioning;
    private int           _currentLevel = 1;  // 1-5
    private CharacterData _selectedCharacter;  // выбранный в меню персонаж

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

    // ──────────────────────────────────────────
    // Update — Escape
    // ──────────────────────────────────────────

    // Escape обрабатывается в PauseMenuUI — там есть логика для SaveSlot и NoteUI

    // ──────────────────────────────────────────
    // Пауза
    // ──────────────────────────────────────────

    /// <summary>Переключить паузу</summary>
    public void TogglePause()
    {
        if (_isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        OnGamePaused?.Invoke();
    }

    public void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        OnGameResumed?.Invoke();
    }

    public bool IsPaused => _isPaused;

    // ──────────────────────────────────────────
    // Переход сцен
    // ──────────────────────────────────────────

    /// <summary>Загрузить сцену с fade transition</summary>
    public void LoadScene(string sceneName)
    {
        if (_isTransitioning) return;
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        _isTransitioning = true;

        // Fade out
        yield return FadeRoutine(1f);

        // Снять паузу если была
        Time.timeScale = 1f;
        _isPaused = false;

        SceneManager.LoadScene(sceneName);

        // Fade in
        yield return FadeRoutine(0f);

        _isTransitioning = false;
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (fadeOverlay == null) yield break;

        float startAlpha = fadeOverlay.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        fadeOverlay.alpha = targetAlpha;
    }

    // ──────────────────────────────────────────
    // Смерть игрока
    // ──────────────────────────────────────────

    /// <summary>Вызывается PlayerStats при смерти</summary>
    public void OnPlayerDeath()
    {
        ScoreManager.Instance?.StopTimer();

        // Если включён автореспавн на чекпоинте — ищем игрока и оживляем
        var cpm = CheckpointManager.Instance;
        if (cpm != null && cpm.AutoRespawnOnDeath)
        {
            var stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null && cpm.RespawnPlayer(stats))
            {
                // Респавн получился — событие смерти не шлём (игрок "вернулся")
                ScoreManager.Instance?.StartTimer();
                return;
            }
        }

        OnPlayerDied?.Invoke();
    }

    // ──────────────────────────────────────────
    // Уровни
    // ──────────────────────────────────────────

    /// <summary>
    /// Загрузить конкретный уровень (1-5) с обновлением таймера и счёта.
    /// Вызывать вместо LoadScene при переходе между уровнями.
    /// </summary>
    public void LoadLevel(int levelNumber, string sceneName)
    {
        // Фиксируем результат завершённого уровня
        ScoreManager.Instance?.StopTimer();
        ScoreManager.Instance?.CommitLevelScore();

        _currentLevel = Mathf.Clamp(levelNumber, 1, 5);
        LoadScene(sceneName);
    }

    /// <summary>Установить уровень без загрузки сцены (например, после загрузки сохранения)</summary>
    public void SetCurrentLevel(int levelNumber)
    {
        _currentLevel = Mathf.Clamp(levelNumber, 1, 5);
    }

    /// <summary>Текущий уровень (1-5)</summary>
    public int CurrentLevel => _currentLevel;

    // ──────────────────────────────────────────
    // Выбор персонажа
    // ──────────────────────────────────────────

    /// <summary>
    /// Вызвать из экрана выбора персонажа перед LoadScene.
    /// Уровень прочитает это значение и применит к Player.
    /// </summary>
    public void SelectCharacter(CharacterData data) => _selectedCharacter = data;

    /// <summary>
    /// CharacterData выбранного персонажа.
    /// null = использовать дефолтное значение из инспектора Player-префаба.
    /// </summary>
    public CharacterData SelectedCharacter => _selectedCharacter;

}
