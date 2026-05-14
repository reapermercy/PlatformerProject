using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Единственный всплывающий пузырь речи на сцене.
/// Синглтон — перемещается к нужному объекту по запросу.
/// Не нужно добавлять Canvas к каждому NPC / двери / табличке.
///
/// ── Настройка в редакторе ──────────────────────────────────────────
/// Иерархия:
///   WorldBubble          ← этот компонент, перемещается в мире
///   └─ Canvas            ← World Space, Scale = (0.01, 0.01, 1)
///      └─ BG             ← Image (9-slice), растянут на весь Canvas
///         └─ Label       ← TMP_Text
///
/// Canvas → RectTransform: Width = 240, Height = 80
/// Canvas → Canvas Scaler: Dynamic Pixels Per Unit = 1
/// Canvas → Sort Order: 10 (выше всего)
/// ───────────────────────────────────────────────────────────────────
/// </summary>
public class WorldBubble : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Синглтон
    // ──────────────────────────────────────────

    public static WorldBubble Instance { get; private set; }

    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("References")]
    [SerializeField] private GameObject    bubbleRoot;   // Canvas или корневой GO пузыря
    [SerializeField] private TMP_Text      label;
    [SerializeField] private RectTransform background;

    [Header("Layout")]
    [SerializeField] private Vector2 worldOffset    = new Vector2(0f, 1.2f); // над объектом
    [SerializeField] private Vector2 padding        = new Vector2(24f, 14f);
    [SerializeField] private float   maxWidthPixels = 200f;

    [Header("Typewriter")]
    [Tooltip("Символов в секунду. 0 — мгновенно.")]
    [SerializeField] private float charsPerSecond = 28f;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private Transform _target;
    private string    _currentText = "";
    private float     _hideAt;          // Time.time для авто-скрытия (0 = не скрывать)
    private Coroutine _typeRoutine;

    public bool IsVisible { get; private set; }
    public bool IsTyping  { get; private set; }

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Hide();
    }

    private void LateUpdate()
    {
        // Следуем за целью
        if (_target != null && IsVisible)
            transform.position = (Vector2)_target.position + worldOffset;

        // Авто-скрытие по таймеру
        if (_hideAt > 0f && Time.time >= _hideAt)
        {
            _hideAt = 0f;
            Hide();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────────────────────────────────────
    // API
    // ──────────────────────────────────────────

    /// <summary>
    /// Показать пузырь над target.
    /// duration > 0 — авто-скрыть через N секунд.
    /// duration = 0 — скрыть вручную через Hide().
    /// </summary>
    public void Show(Transform target, string text, float duration = 0f)
    {
        _target = target;

        // Сразу переместиться чтобы не мелькнуло в старом месте
        if (target != null)
            transform.position = (Vector2)target.position + worldOffset;

        _hideAt = duration > 0f ? Time.time + duration : 0f;

        _currentText = text;
        bubbleRoot?.SetActive(true);
        IsVisible = true;

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);

        if (charsPerSecond > 0f)
            _typeRoutine = StartCoroutine(TypeRoutine(text));
        else
            ShowInstant(text);
    }

    /// <summary>Пропустить анимацию — показать весь текст сразу.</summary>
    public void SkipTyping()
    {
        if (!IsTyping) return;
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = null;
        ShowInstant(_currentText);
    }

    /// <summary>Скрыть пузырь.</summary>
    public void Hide()
    {
        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        IsTyping  = false;
        IsVisible = false;
        _target   = null;
        _hideAt   = 0f;
        bubbleRoot?.SetActive(false);
    }

    // ──────────────────────────────────────────
    // Внутреннее
    // ──────────────────────────────────────────

    private void ShowInstant(string text)
    {
        IsTyping   = false;
        label.text = text;
        ResizeBackground();
    }

    private IEnumerator TypeRoutine(string text)
    {
        IsTyping   = true;
        label.text = "";

        float interval = 1f / charsPerSecond;
        var   sb       = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            sb.Append(c);
            label.text = sb.ToString();
            ResizeBackground();
            yield return new WaitForSeconds(interval);
        }

        IsTyping     = false;
        _typeRoutine = null;
    }

    private void ResizeBackground()
    {
        if (background == null || label == null) return;

        label.rectTransform.sizeDelta = new Vector2(maxWidthPixels, 0f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(label.rectTransform);

        Vector2 size = new Vector2(label.preferredWidth, label.preferredHeight);
        background.sizeDelta = size + padding * 2f;
    }
}
