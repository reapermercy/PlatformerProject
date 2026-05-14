using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC с диалогом в стиле манги.
/// E — следующая реплика; если текст ещё печатается — пропустить анимацию.
/// После последней реплики — закрыть пузырь и сбросить к началу.
///
/// Пузырь берётся из WorldBubble.Instance — отдельный Canvas не нужен.
/// </summary>
public class DialogueInteractable : MonoBehaviour, IInteractable
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("NPC")]
    [SerializeField] private string npcName = "???";

    [Header("Dialogue lines")]
    [TextArea(2, 5)]
    [SerializeField] private List<string> lines = new();

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private int  _index  = -1;
    private bool _isOpen;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void OnEnable()  => InteractableRegistry.Register(this);
    private void OnDisable() => InteractableRegistry.Unregister(this);

    // ──────────────────────────────────────────
    // IInteractable
    // ──────────────────────────────────────────

    public string InteractionPrompt =>
        _isOpen ? "E — продолжить" : $"E — поговорить с {npcName}";

    public void Interact(PlayerController player)
    {
        if (lines.Count == 0) return;

        var bubble = WorldBubble.Instance;

        // Текст ещё печатается — пропустить анимацию
        if (bubble != null && bubble.IsTyping && _isOpen)
        {
            bubble.SkipTyping();
            return;
        }

        _index++;

        if (_index >= lines.Count)
        {
            Close();
            return;
        }

        _isOpen = true;
        bubble?.Show(transform, lines[_index]);
    }

    public void OnFocusLost() => Close();

    // ──────────────────────────────────────────
    // Публичное
    // ──────────────────────────────────────────

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _index  = -1;
        WorldBubble.Instance?.Hide();
    }
}
