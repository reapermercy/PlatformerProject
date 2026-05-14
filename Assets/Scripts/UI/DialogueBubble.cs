using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Всплывающий пузырь речи в стиле манги.
/// Размещается как дочерний объект NPC / таблички на отдельном World Space Canvas.
///
/// Иерархия объекта:
///   NPC
///   └─ BubbleRoot  (этот компонент)
///      └─ Canvas   (World Space, сортировка выше персонажа)
///         └─ Background  (Image, 9-slice)
///            └─ Label     (TMP_Text)
/// </summary>
public class DialogueBubble : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("References")]
    [SerializeField] private GameObject bubbleRoot;   // корень пузыря (вкл/выкл)
    [SerializeField] private TMP_Text   label;        // текстовый компонент
    [SerializeField] private RectTransform background; // фон — авторесайз

    [Header("Typewriter")]
    [Tooltip("Символов в секунду. 0 — мгновенно.")]
    [SerializeField] private float charsPerSecond = 28f;

    [Header("Layout")]
    [SerializeField] private Vector2 padding = new Vector2(24f, 14f);
    [Tooltip("Максимальная ширина текстового блока в единицах мира")]
    [SerializeField] private float maxWidth = 220f;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private string    _currentText = "";
    private Coroutine _typeRoutine;

    public bool IsVisible { get; private set; }
    public bool IsTyping  { get; private set; }

    // ──────────────────────────────────────────
    // API
    // ──────────────────────────────────────────

    /// <summary>Показать текст (с печатанием если charsPerSecond > 0).</summary>
    public void Show(string text)
    {
        _currentText = text;

        if (bubbleRoot != null) bubbleRoot.SetActive(true);
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
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
        IsTyping  = false;
        IsVisible = false;
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
    }

    // ──────────────────────────────────────────
    // Внутренняя логика
    // ──────────────────────────────────────────

    private void ShowInstant(string text)
    {
        IsTyping = false;
        if (label != null) label.text = text;
        ResizeBackground();
    }

    private IEnumerator TypeRoutine(string text)
    {
        IsTyping   = true;
        label.text = "";

        float         interval = 1f / charsPerSecond;
        var           sb       = new StringBuilder(text.Length);

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

        // Ограничиваем ширину — TMP сам перенесёт строки
        label.rectTransform.sizeDelta = new Vector2(maxWidth, 0f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(label.rectTransform);

        Vector2 textSize = new Vector2(
            label.preferredWidth,
            label.preferredHeight);

        background.sizeDelta = textSize + padding * 2f;
    }

    private void Awake()
    {
        Hide();
    }
}
