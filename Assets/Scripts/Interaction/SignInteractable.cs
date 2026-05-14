using UnityEngine;

/// <summary>
/// Табличка или подсказка.
/// E — показать текст во всплывающем пузыре (WorldBubble).
/// Повторное E или уход игрока — закрыть.
/// </summary>
public class SignInteractable : MonoBehaviour, IInteractable
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Content")]
    [TextArea(1, 4)]
    [SerializeField] private string signText = "...";

    [Header("Prompt")]
    [SerializeField] private string promptRead  = "E — прочитать";
    [SerializeField] private string promptClose = "E — закрыть";

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private bool _isOpen;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void OnEnable()  => InteractableRegistry.Register(this);
    private void OnDisable() => InteractableRegistry.Unregister(this);

    // ──────────────────────────────────────────
    // IInteractable
    // ──────────────────────────────────────────

    public string InteractionPrompt => _isOpen ? promptClose : promptRead;

    public void Interact(PlayerController player)
    {
        if (_isOpen) { Close(); return; }

        _isOpen = true;
        WorldBubble.Instance?.Show(transform, signText);
    }

    public void OnFocusLost() => Close();

    // ──────────────────────────────────────────
    // Публичное
    // ──────────────────────────────────────────

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        WorldBubble.Instance?.Hide();
    }
}
