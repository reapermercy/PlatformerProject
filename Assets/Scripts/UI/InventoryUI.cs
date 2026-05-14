using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;

/// <summary>
/// Инвентарь-хотбар.
///
/// Активный предмет (activeItemRoot) — всегда виден.
/// Tab — показать / скрыть полный хотбар.
/// Пока хотбар открыт: цифры 1-9 выбирают слот, способности стенда заблокированы.
/// G — выбросить активный предмет.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Active Item — always visible")]
    [SerializeField] private GameObject activeItemRoot;   // скрывается если инвентарь пуст
    [SerializeField] private Image      activeItemIcon;
    [SerializeField] private TMP_Text   activeItemName;

    [Header("Hotbar — Tab to open/close")]
    [SerializeField] private GameObject hotbarPanel;      // родитель всех слотов
    [SerializeField] private Transform  slotsContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("Colours")]
    [SerializeField] private Color normalColor   = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color selectedColor = new Color(0.8f, 0.6f, 0.1f, 0.9f);

    // ──────────────────────────────────────────
    // Состояние (статик — читается StandInputHandler)
    // ──────────────────────────────────────────

    /// <summary>true пока полный хотбар открыт → цифры идут в инвентарь, не в стенд.</summary>
    public static bool IsHotbarOpen { get; private set; }

    private readonly List<SlotUI> _slots = new();

    // CanvasGroup — скрываем без SetActive, чтобы Layout Group не прыгал
    private CanvasGroup _hotbarGroup;
    private CanvasGroup _activeItemGroup;

    // Делегаты хранятся в полях — иначе RemoveListener не найдёт
    // ту же самую лямбду что AddListener (каждая лямбда = новый объект)
    private UnityAction<ItemData> _onItemAddedCb;
    private UnityAction<ItemData> _onItemRemovedCb;
    private UnityAction<int>      _onActiveSlotChangedCb;

    private class SlotUI
    {
        public GameObject Root;
        public Image      Background;
        public Image      Icon;
        public TMP_Text   NameText;
        public TMP_Text   NumberText;
    }

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Start()
    {
        // Получаем / создаём CanvasGroup для плавного скрытия без SetActive
        _hotbarGroup     = GetOrAddCanvasGroup(hotbarPanel);
        _activeItemGroup = GetOrAddCanvasGroup(activeItemRoot);

        // Создаём делегаты один раз — чтобы можно было корректно отписаться
        _onItemAddedCb         = _ => Refresh();
        _onItemRemovedCb       = _ => Refresh();
        _onActiveSlotChangedCb = _ => Refresh();

        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnItemAdded.AddListener(_onItemAddedCb);
            Inventory.Instance.OnItemRemoved.AddListener(_onItemRemovedCb);
            Inventory.Instance.OnActiveSlotChanged.AddListener(_onActiveSlotChangedCb);
        }

        SetHotbarOpen(false);
        BuildSlots();
        Refresh();
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
        {
            if (_onItemAddedCb         != null) Inventory.Instance.OnItemAdded.RemoveListener(_onItemAddedCb);
            if (_onItemRemovedCb       != null) Inventory.Instance.OnItemRemoved.RemoveListener(_onItemRemovedCb);
            if (_onActiveSlotChangedCb != null) Inventory.Instance.OnActiveSlotChanged.RemoveListener(_onActiveSlotChangedCb);
        }
        IsHotbarOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Tab — открыть / закрыть хотбар
        if (Keyboard.current.tabKey.wasPressedThisFrame)
            SetHotbarOpen(!IsHotbarOpen);

        // G — выбросить активный предмет (всегда)
        if (Keyboard.current.gKey.wasPressedThisFrame)
            Inventory.Instance?.DropActiveItem();

        // F — использовать активный предмет (лечение и т.д.)
        if (Keyboard.current.fKey.wasPressedThisFrame)
            Inventory.Instance?.UseActiveItem();

        // Цифры 1-9 — выбор слота (только когда хотбар открыт)
        if (IsHotbarOpen)
        {
            int count = Mathf.Min(_slots.Count, 9);
            for (int i = 0; i < count; i++)
            {
                var key = NumberKey(i);
                if (key != null && key.wasPressedThisFrame)
                {
                    Inventory.Instance?.SetActiveSlot(i);
                    // Закрываем хотбар после выбора — удобно
                    SetHotbarOpen(false);
                    break;
                }
            }
        }
    }

    // ──────────────────────────────────────────
    // Хотбар
    // ──────────────────────────────────────────

    private void SetHotbarOpen(bool open)
    {
        IsHotbarOpen = open;
        SetGroupVisible(_hotbarGroup, open);
    }

    // ──────────────────────────────────────────
    // Построение слотов
    // ──────────────────────────────────────────

    private void BuildSlots()
    {
        foreach (var s in _slots)
            if (s.Root != null) Destroy(s.Root);
        _slots.Clear();

        if (slotPrefab == null || slotsContainer == null) return;

        int max = Inventory.Instance != null ? Inventory.Instance.MaxItems : 5;

        for (int i = 0; i < max; i++)
        {
            var go   = Instantiate(slotPrefab, slotsContainer);
            var slot = new SlotUI
            {
                Root       = go,
                Background = go.GetComponent<Image>()
                          ?? go.GetComponentInChildren<Image>(),
                Icon       = go.transform.Find("Icon")?.GetComponent<Image>(),
                NameText   = go.transform.Find("NameText")?.GetComponent<TMP_Text>(),
                NumberText = go.transform.Find("NumberText")?.GetComponent<TMP_Text>(),
            };

            if (slot.NumberText != null)
                slot.NumberText.text = (i + 1).ToString();

            _slots.Add(slot);
        }
    }

    // ──────────────────────────────────────────
    // Обновление
    // ──────────────────────────────────────────

    private void Refresh()
    {
        if (Inventory.Instance == null) return;

        var items  = Inventory.Instance.Items;
        int active = Inventory.Instance.ActiveSlot;

        // ── Полный хотбар ──
        for (int i = 0; i < _slots.Count; i++)
        {
            bool    hasItem = i < items.Count;
            ItemData item   = hasItem ? items[i] : null;
            SlotUI   slot   = _slots[i];

            if (slot.NameText != null)
                slot.NameText.text = hasItem ? item.itemName : string.Empty;

            if (slot.Icon != null)
            {
                slot.Icon.enabled = hasItem && item.icon != null;
                if (hasItem && item.icon != null)
                    slot.Icon.sprite = item.icon;
            }

            if (slot.Background != null)
                slot.Background.color = (i == active) ? selectedColor : normalColor;
        }

        // ── Компактное отображение активного предмета ──
        ItemData activeItem = Inventory.Instance.ActiveItem;

        if (_activeItemGroup != null)
        {
            bool has = activeItem != null;
            SetGroupVisible(_activeItemGroup, has);

            if (has)
            {
                if (activeItemIcon != null)
                {
                    activeItemIcon.enabled = activeItem.icon != null;
                    if (activeItem.icon != null)
                        activeItemIcon.sprite = activeItem.icon;
                }

                if (activeItemName != null)
                    activeItemName.text = activeItem.itemName;
            }
        }
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        return go.TryGetComponent<CanvasGroup>(out var cg) ? cg : go.AddComponent<CanvasGroup>();
    }

    private static void SetGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha          = visible ? 1f : 0f;
        group.interactable   = visible;
        group.blocksRaycasts = visible;
    }

    private static KeyControl NumberKey(int zeroBasedIndex) => zeroBasedIndex switch
    {
        0 => Keyboard.current.digit1Key,
        1 => Keyboard.current.digit2Key,
        2 => Keyboard.current.digit3Key,
        3 => Keyboard.current.digit4Key,
        4 => Keyboard.current.digit5Key,
        5 => Keyboard.current.digit6Key,
        6 => Keyboard.current.digit7Key,
        7 => Keyboard.current.digit8Key,
        8 => Keyboard.current.digit9Key,
        _ => null
    };
}
