using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главное меню: Новая игра, Загрузить, Выход.
/// SaveSlotUI открывается в режиме Load.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string firstSceneName = "Level1";

    [Header("References")]
    [SerializeField] private SaveSlotUI saveSlotUI;
    [SerializeField] private Button     continueButton; // активна только если есть хоть одно сохранение

    private void Start()
    {
        RefreshContinueButton();
    }

    // ──────────────────────────────────────────
    // Кнопки
    // ──────────────────────────────────────────

    public void OnNewGame()
    {
        ScoreManager.Instance?.ResetLevelScore();
        GameManager.Instance?.LoadScene(firstSceneName);
    }

    /// <summary>Загрузить последний слот без открытия панели</summary>
    public void OnContinue()
    {
        if (SaveLoadManager.Instance == null) return;

        // Берём первый существующий слот
        for (int i = 0; i < SaveLoadManager.MaxSlots; i++)
        {
            if (SaveLoadManager.Instance.SlotExists(i))
            {
                SaveLoadManager.Instance.LoadGame(i);
                return;
            }
        }
    }

    /// <summary>Открыть панель выбора слота для загрузки</summary>
    public void OnLoad()
    {
        saveSlotUI?.Show(SaveSlotUI.PanelMode.Load);
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ──────────────────────────────────────────
    // Вспомогательные
    // ──────────────────────────────────────────

    private void RefreshContinueButton()
    {
        if (continueButton == null || SaveLoadManager.Instance == null) return;

        bool hasSave = false;
        for (int i = 0; i < SaveLoadManager.MaxSlots; i++)
            if (SaveLoadManager.Instance.SlotExists(i)) { hasSave = true; break; }

        continueButton.interactable = hasSave;
    }
}
