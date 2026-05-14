using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Экран смерти. Появляется после смерти игрока с fade-in.
/// Кнопки: Заново (перезагрузить сцену), В меню.
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Ссылки
    // ──────────────────────────────────────────

    [Header("Panel")]
    [SerializeField] private CanvasGroup deathPanel;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;

    [Header("Fade")]
    [SerializeField] private float fadeInDelay  = 1f;   // ждём после смерти
    [SerializeField] private float fadeInDuration = 0.8f;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        restartButton?.onClick.AddListener(OnRestart);
        menuButton?.onClick.AddListener(OnMenu);

        if (deathPanel != null)
        {
            deathPanel.alpha          = 0f;
            deathPanel.interactable   = false;
            deathPanel.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied.AddListener(OnPlayerDied);
        else
            Debug.LogWarning("[DeathScreenUI] GameManager.Instance is null in Start!");
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied.RemoveListener(OnPlayerDied);
    }

    // ──────────────────────────────────────────
    // Смерть игрока
    // ──────────────────────────────────────────

    private void OnPlayerDied()
    {
        StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return new WaitForSecondsRealtime(fadeInDelay);

        // Fade in панели
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (deathPanel != null)
                deathPanel.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        if (deathPanel != null)
        {
            deathPanel.interactable   = true;
            deathPanel.blocksRaycasts = true;
        }
    }

    // ──────────────────────────────────────────
    // Кнопки
    // ──────────────────────────────────────────

    private void OnRestart()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void OnMenu()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.LoadScene("MainMenu");
    }
}
