using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Компонент на персонаже. Ищет ближайший IInteractable в радиусе,
/// подсвечивает его, по E — взаимодействует.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerInteraction : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Настройки
    // ──────────────────────────────────────────

    [Header("Detection")]
    [SerializeField] private float interactionRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("Highlight")]
    [Tooltip("Цвет подсветки ближайшего объекта")]
    [SerializeField] private Color highlightColor = Color.yellow;

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent<string> OnPromptChanged;  // текст подсказки (пустой = скрыть)

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private PlayerController   _controller;
    private IInteractable      _current;
    private SpriteRenderer     _currentHighlight;
    private InteractableOutline _activeOutline;

    // Переиспользуемый буфер + фильтр (Unity 6 — OverlapCircle с ContactFilter2D)
    private static readonly Collider2D[] s_hitsBuffer = new Collider2D[16];
    private static ContactFilter2D s_overlapFilter = new ContactFilter2D { useTriggers = true };

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // Записка только что закрылась — пропускаем кадр чтобы не переоткрыть её
        if (NoteUI.InputBlockedUntilFrame >= Time.frameCount) return;

        // interactableLayer == 0 означает «все слои» — отключаем фильтр по слою
        if (interactableLayer.value == 0)
            s_overlapFilter.useLayerMask = false;
        else
            s_overlapFilter.SetLayerMask(interactableLayer);

        int hitCount = Physics2D.OverlapCircle(transform.position, interactionRadius, s_overlapFilter, s_hitsBuffer);

        // Авто-подбор — не требует нажатия E
        CheckAutoPickup(s_hitsBuffer, hitCount);

        // Обычное взаимодействие по E
        FindNearest(s_hitsBuffer, hitCount);

        if (_current != null
            && Keyboard.current != null
            && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    // ──────────────────────────────────────────
    // Поиск ближайшего
    // ──────────────────────────────────────────

    private void CheckAutoPickup(Collider2D[] hits, int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            var hit = hits[i];
            if (hit == null) continue;

            var pickup = hit.GetComponent<ItemPickupInteractable>()
                      ?? hit.GetComponentInParent<ItemPickupInteractable>();
            // CanAutoPickup учитывает задержку после выброса из инвентаря
            if (pickup == null || !pickup.CanAutoPickup) continue;
            pickup.Interact(_controller);
            return;
        }
    }

    private void FindNearest(Collider2D[] hits, int hitCount)
    {
        IInteractable  best     = null;
        float          bestDist = float.MaxValue;
        SpriteRenderer bestSR   = null;

        // ── 1. Физический поиск (объекты с коллайдерами) ──
        for (int i = 0; i < hitCount; i++)
        {
            var hit = hits[i];
            if (hit == null) continue;

            IInteractable interactable =
                hit.GetComponent<IInteractable>()
             ?? hit.GetComponentInParent<IInteractable>()
             ?? hit.GetComponentInChildren<IInteractable>();

            if (interactable == null) continue;

            // Авто-подбираемые не показываем как требующие E
            if (interactable is ItemPickupInteractable p && p.CanAutoPickup) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist >= bestDist) continue;

            bestDist = dist;
            best     = interactable;
            bestSR   = hit.GetComponentInChildren<SpriteRenderer>()
                    ?? hit.GetComponentInParent<SpriteRenderer>();
        }

        // ── 2. Реестр (рычаги, двери — коллайдер не нужен) ──
        foreach (var mb in InteractableRegistry.All)
        {
            if (mb == null) continue;
            if (mb is not IInteractable interactable) continue;
            if (interactable is ItemPickupInteractable) continue;

            float dist = Vector2.Distance(transform.position, mb.transform.position);
            if (dist >= interactionRadius || dist >= bestDist) continue;

            bestDist = dist;
            best     = interactable;
            bestSR   = mb.GetComponentInChildren<SpriteRenderer>()
                    ?? mb.GetComponentInParent<SpriteRenderer>();
        }

        // Подсветка — только если цель сменилась
        if (best != _current)
        {
            // Сообщаем старой цели что игрок ушёл (табличка/диалог сами закроются)
            _current?.OnFocusLost();
            ClearHighlight();
            _current          = best;
            _currentHighlight = bestSR;
            ApplyHighlight();
        }

        // Подсказку обновляем ВСЕГДА — состояние объекта могло измениться (дверь открылась)
        OnPromptChanged?.Invoke(_current != null ? _current.InteractionPrompt : "");
    }

    // ──────────────────────────────────────────
    // Взаимодействие (вызывается из InputRouter)
    // ──────────────────────────────────────────

    /// <summary>E — взаимодействие с ближайшим объектом</summary>
    public void TryInteract()
    {
        if (_current == null) return;
        _current.Interact(_controller);
        // Обновить подсказку сразу — объект мог сменить состояние (дверь открылась)
        OnPromptChanged?.Invoke(_current.InteractionPrompt);
    }

    // ──────────────────────────────────────────
    // Highlight
    // ──────────────────────────────────────────

    private void ApplyHighlight()
    {
        if (_currentHighlight == null) return;

        // Берём существующий или добавляем новый компонент каймы
        _activeOutline = _currentHighlight.GetComponentInParent<InteractableOutline>()
                      ?? _currentHighlight.gameObject.AddComponent<InteractableOutline>();
        _activeOutline.Show(_currentHighlight, highlightColor);
    }

    private void ClearHighlight()
    {
        _activeOutline?.Hide();
        _activeOutline = null;
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
