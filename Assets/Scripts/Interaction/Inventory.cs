using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Синглтон инвентаря игрока.
/// Хранит до maxItems предметов. Если переполнен — предмет падает рядом с игроком.
/// </summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Максимальное количество предметов")]
    [SerializeField] private int maxItems = 5;

    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [Tooltip("Transform игрока — точка выброса предметов")]
    [SerializeField] private Transform playerTransform;

    [Header("Events")]
    public UnityEvent<ItemData> OnItemAdded;
    public UnityEvent<ItemData> OnItemRemoved;
    public UnityEvent<int>      OnActiveSlotChanged; // (slotIndex)

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private readonly List<ItemData> _items = new();
    private int _activeSlot = 0;

    public IReadOnlyList<ItemData> Items      => _items;
    public int                     Count      => _items.Count;
    public int                     MaxItems   => maxItems;
    public int                     ActiveSlot => _activeSlot;

    public ItemData ActiveItem =>
        _activeSlot >= 0 && _activeSlot < _items.Count ? _items[_activeSlot] : null;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ──────────────────────────────────────────
    // Добавление
    // ──────────────────────────────────────────

    /// <summary>
    /// Добавить предмет. Score — применяется немедленно.
    /// Если инвентарь полон — предмет падает рядом с игроком.
    /// Возвращает true если предмет попал в инвентарь.
    /// </summary>
    public bool Add(ItemData item)
    {
        if (item == null) return false;

        if (item.itemType == ItemType.Score)
        {
            ScoreManager.Instance?.AddKillPoints(default, item.value);
            OnItemAdded?.Invoke(item);
            return true;
        }

        if (_items.Count >= maxItems)
        {
            // Инвентарь полон — выбросить предмет в мир рядом с игроком
            SpawnInWorld(item, GetDropPosition());
            Debug.Log($"[Inventory] Переполнен — {item.itemName} упал рядом");
            return false;
        }

        _items.Add(item);
        OnItemAdded?.Invoke(item);
        Debug.Log($"[Inventory] Добавлено: {item.itemName} ({_items.Count}/{maxItems})");
        return true;
    }

    // ──────────────────────────────────────────
    // Использование
    // ──────────────────────────────────────────

    /// <summary>Использовать предмет в слоте. Возвращает false если нельзя.</summary>
    public bool UseItem(ItemData item)
    {
        if (!_items.Contains(item)) return false;

        switch (item.itemType)
        {
            case ItemType.Heal:
                if (playerStats == null) return false;
                playerStats.Heal(item.value);
                break;
            case ItemType.Key:
                return false; // ключ нельзя "использовать" вручную — только через дверь
            default:
                return false;
        }

        _items.Remove(item);
        // Скорректировать активный слот если он вышел за пределы
        _activeSlot = Mathf.Clamp(_activeSlot, 0, Mathf.Max(0, _items.Count - 1));
        OnItemRemoved?.Invoke(item);
        return true;
    }

    /// <summary>Использовать активный предмет.</summary>
    public bool UseActiveItem() => ActiveItem != null && UseItem(ActiveItem);

    // ──────────────────────────────────────────
    // Выбор слота
    // ──────────────────────────────────────────

    public void SetActiveSlot(int index)
    {
        if (_items.Count == 0) return;
        _activeSlot = Mathf.Clamp(index, 0, _items.Count - 1);
        OnActiveSlotChanged?.Invoke(_activeSlot);
    }

    public void SelectNext()
    {
        if (_items.Count == 0) return;
        SetActiveSlot((_activeSlot + 1) % _items.Count);
    }

    public void SelectPrev()
    {
        if (_items.Count == 0) return;
        SetActiveSlot((_activeSlot - 1 + _items.Count) % _items.Count);
    }

    // ──────────────────────────────────────────
    // Выброс из инвентаря
    // ──────────────────────────────────────────

    /// <summary>Выбросить активный предмет из инвентаря в мир.</summary>
    public void DropActiveItem()
    {
        if (ActiveItem == null) return;
        DropItem(ActiveItem);
    }

    /// <summary>Выбросить конкретный предмет из инвентаря в мир.</summary>
    public void DropItem(ItemData item)
    {
        if (!_items.Remove(item)) return;

        Vector2 pos = GetDropPosition();
        var go = SpawnInWorld(item, pos);

        // Небольшой импульс вперёд
        if (go != null && playerTransform != null)
        {
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 forward = playerTransform.localScale.x >= 0 ? Vector2.right : Vector2.left;
                rb.AddForce((forward + Vector2.up * 0.5f).normalized * 4f, ForceMode2D.Impulse);
            }
        }

        _activeSlot = Mathf.Clamp(_activeSlot, 0, Mathf.Max(0, _items.Count - 1));
        OnItemRemoved?.Invoke(item);
    }

    // ──────────────────────────────────────────
    // Прочее
    // ──────────────────────────────────────────

    public bool Remove(ItemData item)
    {
        if (!_items.Remove(item)) return false;
        _activeSlot = Mathf.Clamp(_activeSlot, 0, Mathf.Max(0, _items.Count - 1));
        OnItemRemoved?.Invoke(item);
        return true;
    }

    public bool Has(ItemData item) => _items.Contains(item);

    // ──────────────────────────────────────────
    // Вспомогательные
    // ──────────────────────────────────────────

    private Vector2 GetDropPosition()
    {
        if (playerTransform != null)
            return (Vector2)playerTransform.position + Vector2.up * 0.5f;
        return Vector2.zero;
    }

    private GameObject SpawnInWorld(ItemData item, Vector2 position)
    {
        if (item.worldPrefab == null)
        {
            Debug.LogWarning($"[Inventory] {item.itemName} не имеет worldPrefab — не могу выбросить");
            return null;
        }

        var go = Instantiate(item.worldPrefab, position, Quaternion.identity);

        // Гарантируем гравитацию >= 1f: нулевая + AddForce при дропе = вечный полёт
        var spawnRb = go.GetComponent<Rigidbody2D>();
        if (spawnRb != null && spawnRb.gravityScale < 1f)
            spawnRb.gravityScale = 1f;

        // Ищем компонент на объекте и в детях
        var pickup = go.GetComponent<ItemPickupInteractable>()
                  ?? go.GetComponentInChildren<ItemPickupInteractable>();

        if (pickup != null)
        {
            pickup.SetItemData(item);
            // Задержка 0.5 сек — предмет не подбирается сразу после выброса
            pickup.SetPickupDelay(0.5f);
        }

        return go;
    }
}
