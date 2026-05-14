using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Синглтон-оверлей для чтения записок.
/// Открывается через NoteInteractable.Interact().
/// E / Escape — закрыть.
///
/// Иерархия в Canvas (Screen Space — Overlay):
///   NotePanel
///   ├─ Background  (Image, затемнение + рамка)
///   ├─ TitleText   (TMP_Text)
///   ├─ BodyText    (TMP_Text, ScrollRect для длинных текстов)
///   └─ CloseHint   (TMP_Text — "E / Esc — закрыть")
/// </summary>
public class NoteUI : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Синглтон
    // ──────────────────────────────────────────

    public static NoteUI Instance { get; private set; }

    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text   titleText;
    [SerializeField] private TMP_Text   bodyText;

    [Header("Pause while reading")]
    [Tooltip("Заморозить игру пока записка открыта")]
    [SerializeField] private bool pauseWhileReading = true;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    public bool IsOpen { get; private set; }

    /// <summary>
    /// Если текущий кадр ≤ этого значения — записка только что закрылась.
    /// PlayerInteraction и PauseMenuUI проверяют это чтобы не обработать тот же ввод.
    /// </summary>
    public static int InputBlockedUntilFrame { get; private set; } = -1;

    // Флаг чтобы E открытия не закрыл записку в тот же кадр
    private bool _ignoreInputThisFrame;

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
        panel?.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Пропускаем кадр открытия — тот же E не должен сразу закрыть записку
        if (_ignoreInputThisFrame)
        {
            _ignoreInputThisFrame = false;
            return;
        }

        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame
         || Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────────────────────────────────────
    // API
    // ──────────────────────────────────────────

    /// <summary>Открыть оверлей с содержимым записки.</summary>
    public void Open(string title, string body)
    {
        if (titleText != null) titleText.text = title;
        if (bodyText  != null) bodyText.text  = body;

        panel?.SetActive(true);
        IsOpen                = true;
        _ignoreInputThisFrame = true;

        // Замораживаем время напрямую — не через GameManager.PauseGame(),
        // чтобы PauseMenuUI не считал это паузой и не открывался поверх
        if (pauseWhileReading)
            Time.timeScale = 0f;
    }

    /// <summary>Закрыть оверлей.</summary>
    public void Close()
    {
        panel?.SetActive(false);
        IsOpen               = false;
        InputBlockedUntilFrame = Time.frameCount; // блокируем ввод до конца этого кадра

        if (pauseWhileReading)
            Time.timeScale = 1f;
    }

    // ──────────────────────────────────────────
    // Внутреннее
    // ──────────────────────────────────────────

}
