using UnityEngine;

/// <summary>
/// Интерфейс для любого объекта, с которым можно взаимодействовать по E.
/// </summary>
public interface IInteractable
{
    /// <summary>Текст подсказки (для UI)</summary>
    string InteractionPrompt { get; }

    /// <summary>Выполнить взаимодействие</summary>
    void Interact(PlayerController player);

    /// <summary>
    /// Вызывается PlayerInteraction когда игрок выходит из зоны или переключается на другой объект.
    /// По умолчанию — ничего не делает. Переопределяется табличками и диалогами для авто-закрытия.
    /// </summary>
    void OnFocusLost() { }
}
