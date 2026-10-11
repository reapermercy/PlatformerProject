using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Гейт метроидвании: открывается, если у активного стенда игрока есть
/// требуемый тег способности. Спрашивает ТЕГ, а не имя стенда/персонажа —
/// поэтому любой будущий стенд с нужным тегом откроет тот же проход.
///
/// Как работает: на объекте триггер-коллайдер (перекрывает проход).
/// Игрок входит в триггер → проверяется StandController.PlayerStand.
/// Есть тег → OnOpened (анимация/выключение стены/SetActive), иначе —
/// OnLocked (подсказка «нужна способность X»).
///
/// Это тот самый «один AbilityGate на карте» из шага 2 — проверка гейтинга.
/// Логику открытия (что именно происходит) вешать на OnOpened в инспекторе,
/// как у LeverInteractable.OnActivated.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AbilityGate : MonoBehaviour
{
    [Header("Requirement")]
    [Tooltip("Какой тег способности нужен, чтобы гейт открылся")]
    [SerializeField] private AbilityTag requiredTag = AbilityTag.HeavyImpact;

    [Header("Behaviour")]
    [Tooltip("Открывшись один раз, больше не проверять (стена сломана навсегда)")]
    [SerializeField] private bool openOnce = true;

    [Tooltip("Лог в консоль при попытке — удобно для проверки гейтинга")]
    [SerializeField] private bool debugLog = false;

    [Header("Events")]
    [Tooltip("Есть требуемый тег → открыть проход (анимация, выкл. коллайдера, SetActive…)")]
    public UnityEvent OnOpened;
    [Tooltip("Тега нет → показать подсказку игроку")]
    public UnityEvent OnLocked;

    private bool _opened;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_opened) return;

        // Реагируем только на игрока — не на стенд, не на врагов/предметы.
        // Стенд на поводке может не дотянуться до прохода, поэтому вход
        // считаем по телу игрока, а способность спрашиваем у его стенда.
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        TryOpen();
    }

    /// <summary>
    /// Проверить доступ и открыть/отказать. Публичный — можно дёрнуть
    /// из E-взаимодействия или скрипта, не только из триггера.
    /// </summary>
    public bool TryOpen()
    {
        if (_opened) return true;

        var stand = StandController.PlayerStand;
        bool hasTag = stand != null && stand.HasAbilityTag(requiredTag);

        if (debugLog)
        {
            string standName = stand == null ? "нет" : (stand.Data != null ? stand.Data.standName : "—");
            string access    = hasTag ? "открыт" : "закрыт";
            Debug.Log($"[AbilityGate] требуется {requiredTag}; стенд={standName}; доступ={access}", this);
        }

        if (!hasTag)
        {
            OnLocked?.Invoke();
            return false;
        }

        Open();
        return true;
    }

    private void Open()
    {
        _opened = true;
        OnOpened?.Invoke();
        if (openOnce)
            enabled = false; // больше не проверяем; сам объект/коллайдер гасит OnOpened
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        Gizmos.color = _opened
            ? new Color(0f, 1f, 0f, 0.15f)
            : new Color(1f, 0.55f, 0f, 0.2f);
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
