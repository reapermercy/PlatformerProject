using UnityEngine;

/// <summary>
/// Записка или письмо.
/// E — открыть полноэкранный оверлей для чтения.
/// Закрывается повторным E или Escape.
/// </summary>
public class NoteInteractable : MonoBehaviour, IInteractable
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Content")]
    [SerializeField] private string noteTitle = "Записка";
    [TextArea(3, 12)]
    [SerializeField] private string noteText  = "";

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void OnEnable()  => InteractableRegistry.Register(this);
    private void OnDisable() => InteractableRegistry.Unregister(this);

    // ──────────────────────────────────────────
    // IInteractable
    // ──────────────────────────────────────────

    public string InteractionPrompt => "E — прочитать";

    public void Interact(PlayerController player)
    {
        NoteUI.Instance?.Open(noteTitle, noteText);
    }

    // OnFocusLost не переопределяем — записка не закрывается при уходе игрока,
    // только вручную через E / Escape внутри NoteUI.
}
