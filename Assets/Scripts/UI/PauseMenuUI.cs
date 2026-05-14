using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Меню паузы. Проверяет GameManager.IsPaused каждый кадр — не зависит от подписок.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject pausePanel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button menuButton;

    [Header("Settings")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("References")]
    [SerializeField] private SaveSlotUI saveSlotUI;

    private bool _shown;

    private void Awake()
    {
        resumeButton?.onClick.AddListener(OnResume);
        saveButton?.onClick.AddListener(OnSave);
        loadButton?.onClick.AddListener(OnLoad);
        menuButton?.onClick.AddListener(OnQuitToMenu);
        pausePanel?.SetActive(false);
    }

    private void Update()
    {
        // Синхронизируем видимость панели с состоянием паузы
        bool shouldShow = GameManager.Instance != null && GameManager.Instance.IsPaused;
        if (shouldShow != _shown)
        {
            _shown = shouldShow;
            pausePanel?.SetActive(_shown);
            if (!_shown) saveSlotUI?.Hide(); // закрыть Save/Load при снятии паузы извне
        }

        if (Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        // Записка открыта или только что закрылась — не трогаем паузу в этом кадре
        if (NoteUI.Instance != null && NoteUI.Instance.IsOpen) return;
        if (NoteUI.InputBlockedUntilFrame >= Time.frameCount) return;

        if (!_shown)
        {
            // Игра идёт — открыть паузу
            GameManager.Instance?.PauseGame();
        }
        else if (saveSlotUI != null && saveSlotUI.IsVisible)
        {
            // Пауза открыта + Save/Load панель видна — закрыть только её
            saveSlotUI.Hide();
        }
        else
        {
            // Пауза открыта — закрыть
            GameManager.Instance?.ResumeGame();
        }
    }

    public void OnResume()
    {
        GameManager.Instance?.ResumeGame();
    }

    public void OnSave()
    {
        saveSlotUI?.Show(SaveSlotUI.PanelMode.Save);
    }

    public void OnLoad()
    {
        saveSlotUI?.Show(SaveSlotUI.PanelMode.Load);
    }

    public void OnQuitToMenu()
    {
        GameManager.Instance?.ResumeGame();
        GameManager.Instance?.LoadScene(mainMenuScene);
    }
}
