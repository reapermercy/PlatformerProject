using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Подбираемый предмет в мире.
/// autoPickup = true  → подбирается при касании игрока (нужен Collider2D Is Trigger).
/// autoPickup = false → требует нажатия E (обнаруживается PlayerInteraction).
/// </summary>
public class ItemPickupInteractable : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;

    [Header("Behaviour")]
    [Tooltip("Подобрать автоматически при соприкосновении")]
    [SerializeField] private bool autoPickup = false;
    [Tooltip("Стенд может схватить этот предмет физически")]
    [SerializeField] private bool canBeGrabbedByStand = true;
    [Tooltip("Подсказка при autoPickup = false")]
    [SerializeField] private string customPrompt = "";

    [Header("Events")]
    public UnityEvent<ItemData> OnPickedUp;

    // Время до которого авто-подбор заблокирован (защита от мгновенного подбора после выброса)
    private float _pickupAllowedAfter = 0f;

    // ──────────────────────────────────────────
    // IInteractable
    // ──────────────────────────────────────────

    public string InteractionPrompt =>
        !string.IsNullOrEmpty(customPrompt)
            ? customPrompt
            : itemData != null
                ? $"E — Подобрать {itemData.itemName}"
                : "E — Подобрать";

    public void Interact(PlayerController player)
    {
        PickUp();
    }

    // ──────────────────────────────────────────
    // Логика подбора
    // ──────────────────────────────────────────

    private void PickUp()
    {
        Inventory.Instance?.Add(itemData);
        OnPickedUp?.Invoke(itemData);
        Destroy(gameObject);
    }

    // ──────────────────────────────────────────
    // Публичные свойства и методы
    // ──────────────────────────────────────────

    public ItemData ItemData            => itemData;
    public bool     CanBeGrabbedByStand => canBeGrabbedByStand;

    /// <summary>
    /// true если авто-подбор включён И задержка после выброса истекла.
    /// Используется PlayerInteraction.CheckAutoPickup.
    /// </summary>
    public bool CanAutoPickup => autoPickup && Time.time >= _pickupAllowedAfter;

    /// <summary>
    /// Задержать авто-подбор на delay секунд.
    /// Вызывается из Inventory при выбросе — предмет не подбирается сразу же.
    /// </summary>
    public void SetPickupDelay(float delay)
    {
        _pickupAllowedAfter = Time.time + delay;
    }

    /// <summary>Устанавливает данные предмета при спавне из инвентаря.</summary>
    public void SetItemData(ItemData data)
    {
        itemData            = data;
        autoPickup          = data != null && data.autoPickup;
        canBeGrabbedByStand = data == null || data.canBeGrabbedByStand;

        // Подставляем спрайт из ItemData
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && data?.icon != null)
            sr.sprite = data.icon;
    }
}
