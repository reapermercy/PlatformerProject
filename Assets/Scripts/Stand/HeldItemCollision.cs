using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Компонент на стенде для захвата и переноса предметов.
/// Предмет остаётся Dynamic — физика работает, стены блокируют перенос.
/// Притяжение реализовано пружинной силой, а не MovePosition.
/// </summary>
public class HeldItemCollision : MonoBehaviour
{
    [Header("Grab")]
    [Tooltip("Смещение предмета относительно стенда")]
    [SerializeField] private Vector2 holdOffset = new Vector2(0f, -0.3f);

    [Header("Spring Follow")]
    [Tooltip("Сила притяжения к стенду")]
    [SerializeField] private float followStrength  = 150f;
    [Tooltip("Затухание — убирает осцилляции")]
    [SerializeField] private float followDamping   = 12f;
    [Tooltip("Максимальная дистанция притяжения — срезает пиковую силу")]
    [SerializeField] private float maxPullDistance = 3f;
    [Tooltip("Максимальная скорость предмета — предотвращает tunneling")]
    [SerializeField] private float maxItemSpeed    = 8f;

    [Header("Detection")]
    [Tooltip("Радиус поиска предметов для захвата")]
    [SerializeField] private float grabRadius = 1f;
    [Tooltip("Слой предметов, которые можно хватать")]
    [SerializeField] private LayerMask grabbableLayer;

    [Header("Stuck Drop")]
    [Tooltip("Если предмет дальше этого расстояния от стенда — считается застрявшим")]
    [SerializeField] private float stuckDropDistance = 1.2f;
    [Tooltip("Сколько секунд предмет должен быть 'застрявшим' прежде чем упасть")]
    [SerializeField] private float stuckDropDuration = 0.35f;

    [Header("Events")]
    public UnityEvent<Rigidbody2D> OnItemGrabbed;
    public UnityEvent<Rigidbody2D> OnItemReleased;

    [Header("Smoothing")]
    [Tooltip("Скорость сглаживания позиции якоря — снижает пиковые силы при быстром движении стенда")]
    [SerializeField] private float anchorSmoothSpeed = 10f;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private Rigidbody2D              _heldItem;
    private float                    _heldItemOriginalGravity;
    private CollisionDetectionMode2D _heldItemOriginalCollision;
    private Vector2                  _smoothedAnchor;
    private float                    _stuckTimer;   // сколько секунд предмет вне допустимого радиуса

    // Переиспользуемый буфер + фильтр (Unity 6 — OverlapCircle с ContactFilter2D)
    private static readonly Collider2D[] s_overlapBuffer = new Collider2D[16];
    private static ContactFilter2D s_overlapFilter = new ContactFilter2D { useTriggers = true };

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void FixedUpdate()
    {
        if (_heldItem == null) return;

        // Сглаживаем якорь — стенд может телепортироваться за кадр,
        // сглаживание убирает пиковые импульсы
        Vector2 desiredAnchor = (Vector2)transform.position + holdOffset;
        _smoothedAnchor = Vector2.MoveTowards(
            _smoothedAnchor, desiredAnchor, anchorSmoothSpeed * Time.fixedDeltaTime);

        Vector2 delta = _smoothedAnchor - _heldItem.position;

        // Срезаем пиковую силу при большом расстоянии
        if (delta.magnitude > maxPullDistance)
            delta = delta.normalized * maxPullDistance;

        Vector2 force = delta * followStrength - _heldItem.linearVelocity * followDamping;
        _heldItem.AddForce(force, ForceMode2D.Force);

        // Ограничиваем скорость — предотвращаем tunneling
        if (_heldItem.linearVelocity.magnitude > maxItemSpeed)
            _heldItem.linearVelocity = _heldItem.linearVelocity.normalized * maxItemSpeed;

        // Проверка застревания: предмет слишком далеко от стенда N секунд подряд → уронить
        float actualDist = Vector2.Distance(_heldItem.position, (Vector2)transform.position + holdOffset);
        if (actualDist > stuckDropDistance)
        {
            _stuckTimer += Time.fixedDeltaTime;
            if (_stuckTimer >= stuckDropDuration)
                ReleaseItem();   // ReleaseItem сбросит таймер
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

    // ──────────────────────────────────────────
    // Захват
    // ──────────────────────────────────────────

    public void GrabItem(Rigidbody2D item)
    {
        if (item == null) return;
        if (_heldItem != null) ReleaseItem();

        _heldItem = item;

        // Оставляем Dynamic — физика со стенами работает
        _heldItemOriginalGravity   = item.gravityScale;
        _heldItemOriginalCollision = item.collisionDetectionMode;

        _heldItem.gravityScale           = 0f;
        _heldItem.linearVelocity         = Vector2.zero;
        _heldItem.angularVelocity        = 0f;
        _heldItem.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _smoothedAnchor = item.position;
        _stuckTimer     = 0f;

        OnItemGrabbed?.Invoke(_heldItem);
    }

    public void TryGrabNearest()
    {
        if (_heldItem != null) return;

        // grabbableLayer == 0 означает «все слои» — отключаем фильтр по слою
        if (grabbableLayer.value == 0)
            s_overlapFilter.useLayerMask = false;
        else
            s_overlapFilter.SetLayerMask(grabbableLayer);

        int count = Physics2D.OverlapCircle(transform.position, grabRadius, s_overlapFilter, s_overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            var col = s_overlapBuffer[i];
            if (col == null) continue;

            // Ищем ItemPickupInteractable на объекте и в иерархии
            var pickup = col.GetComponent<ItemPickupInteractable>()
                      ?? col.GetComponentInParent<ItemPickupInteractable>()
                      ?? col.GetComponentInChildren<ItemPickupInteractable>();

            if (pickup != null && !pickup.CanBeGrabbedByStand) continue;

            var rb = col.GetComponent<Rigidbody2D>();
            if (rb != null) { GrabItem(rb); return; }
        }
    }

    // ──────────────────────────────────────────
    // Отпускание
    // ──────────────────────────────────────────

    public void ReleaseItem()
    {
        if (_heldItem == null) return;

        var released = _heldItem;
        // Минимум 1f: предмет с нулевой гравитацией + любой импульс = вечный полёт.
        // Если предмет был намеренно плавающим (gravityScale == 0), он получит нормальное падение.
        _heldItem.gravityScale           = Mathf.Max(1f, _heldItemOriginalGravity);
        _heldItem.collisionDetectionMode = _heldItemOriginalCollision;

        _heldItem    = null;
        _stuckTimer  = 0f;

        OnItemReleased?.Invoke(released);
    }

    public void ThrowItem(Vector2 direction, float force)
    {
        if (_heldItem == null) return;
        var item = _heldItem;
        ReleaseItem();
        item.AddForce(direction.normalized * force, ForceMode2D.Impulse);
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, grabRadius);
    }

    public bool       IsHoldingItem => _heldItem != null;
    public Rigidbody2D HeldItem     => _heldItem;
}
