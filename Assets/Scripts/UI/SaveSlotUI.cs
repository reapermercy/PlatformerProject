using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Панель выбора слота сохранения.
/// Режим Save — сохраняет текущую игру в слот.
/// Режим Load — загружает выбранный слот.
/// Используется в паузе и в главном меню.
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    public enum PanelMode { Save, Load }

    // ──────────────────────────────────────────
    // Ссылки
    // ──────────────────────────────────────────

    [Header("Mode")]
    [SerializeField] private PanelMode mode = PanelMode.Save;

    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Slots (3 штуки)")]
    [SerializeField] private SlotView[] slots = new SlotView[3];

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Confirm Dialog")]
    [SerializeField] private GameObject confirmDialog;
    [SerializeField] private TMP_Text   confirmText;
    [SerializeField] private Button     confirmYesButton;
    [SerializeField] private Button     confirmNoButton;

    // ──────────────────────────────────────────
    // Вложенный тип
    // ──────────────────────────────────────────

    [System.Serializable]
    public class SlotView
    {
        public Button   loadButton;    // кнопка загрузки / сохранения
        public Button   deleteButton;  // кнопка удаления
        public TMP_Text titleText;     // "Слот 1" или "Пусто"
        public TMP_Text dateText;      // дата сохранения
        public TMP_Text scoreText;     // очки
        public TMP_Text levelText;     // "Уровень 3"
        public TMP_Text timeText;      // "Время: 01:24:07"
    }

    // ──────────────────────────────────────────
    // Приватные
    // ──────────────────────────────────────────

    private int     _pendingSlot = -1;
    private bool    _pendingDelete;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        panel?.SetActive(false);
        confirmDialog?.SetActive(false);

        closeButton?.onClick.AddListener(Hide);
        confirmYesButton?.onClick.AddListener(OnConfirmYes);
        confirmNoButton?.onClick.AddListener(OnConfirmNo);

        for (int i = 0; i < slots.Length; i++)
        {
            int idx = i; // замыкание
            slots[i].loadButton?.onClick.AddListener(  () => OnSlotAction(idx));
            slots[i].deleteButton?.onClick.AddListener(() => OnDeleteRequest(idx));
        }
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    // ──────────────────────────────────────────
    // Публичное API
    // ──────────────────────────────────────────

    public void Show(PanelMode panelMode)
    {
        mode = panelMode;
        if (panel != null) panel.SetActive(true);
        RefreshAll();
    }

    public bool IsVisible => panel != null && panel.activeSelf;

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        if (confirmDialog != null) confirmDialog.SetActive(false);
    }

    // ──────────────────────────────────────────
    // Обновление отображения слотов
    // ──────────────────────────────────────────

    private void RefreshAll()
    {
        for (int i = 0; i < slots.Length; i++)
            RefreshSlot(i);
    }

    private void RefreshSlot(int i)
    {
        if (slots[i] == null || SaveLoadManager.Instance == null) return;

        bool exists = SaveLoadManager.Instance.SlotExists(i);
        var  data   = exists ? SaveLoadManager.Instance.ReadSlot(i) : null;

        // Текст слота
        if (slots[i].titleText != null)
            slots[i].titleText.text = exists ? data.slotName : $"Слот {i + 1}  —  Пусто";

        if (slots[i].dateText != null)
            slots[i].dateText.text = exists ? data.saveDate : string.Empty;

        if (slots[i].scoreText != null)
            slots[i].scoreText.text = exists ? $"Очки: {data.totalScore}" : string.Empty;

        if (slots[i].levelText != null)
            slots[i].levelText.text = exists ? $"Уровень {data.currentLevel}" : string.Empty;

        if (slots[i].timeText != null)
            slots[i].timeText.text = exists
                ? $"Время: {ScoreManager.FormatTime(data.totalPlayTime)}"
                : string.Empty;

        // Кнопка действия
        if (slots[i].loadButton != null)
        {
            var label = slots[i].loadButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = mode == PanelMode.Save
                    ? (exists ? "Перезаписать" : "Сохранить")
                    : "Загрузить";

            // Загрузка недоступна если слот пуст
            slots[i].loadButton.interactable = mode == PanelMode.Save || exists;
        }

        // Кнопка удаления — только если слот есть
        if (slots[i].deleteButton != null)
            slots[i].deleteButton.gameObject.SetActive(exists);
    }

    // ──────────────────────────────────────────
    // Действия
    // ──────────────────────────────────────────

    private void OnSlotAction(int slot)
    {
        if (mode == PanelMode.Save)
        {
            bool exists = SaveLoadManager.Instance != null && SaveLoadManager.Instance.SlotExists(slot);
            if (exists)
                ShowConfirm(slot, delete: false, $"Перезаписать Слот {slot + 1}?");
            else
                ExecuteSave(slot);
        }
        else
        {
            ExecuteLoad(slot);
        }
    }

    private void OnDeleteRequest(int slot)
    {
        ShowConfirm(slot, delete: true, $"Удалить Слот {slot + 1}?");
    }

    private void ShowConfirm(int slot, bool delete, string message)
    {
        _pendingSlot   = slot;
        _pendingDelete = delete;

        if (confirmText    != null) confirmText.text = message;
        if (confirmDialog  != null) confirmDialog.SetActive(true);
    }

    private void OnConfirmYes()
    {
        if (confirmDialog != null) confirmDialog.SetActive(false);

        if (_pendingSlot < 0) return;

        if (_pendingDelete) ExecuteDelete(_pendingSlot);
        else                ExecuteSave(_pendingSlot);

        _pendingSlot = -1;
    }

    private void OnConfirmNo()
    {
        _pendingSlot = -1;
        if (confirmDialog != null) confirmDialog.SetActive(false);
    }

    private void ExecuteSave(int slot)
    {
        SaveLoadManager.Instance?.Save(slot);
        RefreshSlot(slot);
    }

    private void ExecuteLoad(int slot)
    {
        Hide();
        SaveLoadManager.Instance?.LoadGame(slot);
    }

    private void ExecuteDelete(int slot)
    {
        SaveLoadManager.Instance?.DeleteSlot(slot);
        RefreshSlot(slot);
    }
}
