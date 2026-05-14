using System.Collections;
using UnityEngine;

/// <summary>
/// Опциональный компонент для зомби: один раз «поднимается» после смерти с частью HP.
/// Добавь на зомби-префаб рядом с EnemyStats + EnemyAI.
///
/// Как работает:
///   1. Подписывается на EnemyStats.OnDeath
///   2. Через reviveDelay вызывает EnemyStats.Revive(percent):
///      — отменяется Destroy,
///      — HP восстанавливается,
///      — коллайдер возвращается,
///      — вызывается OnRevive → EnemyAI возвращается в Idle.
///
/// Требование: EnemyStats.destroyDelay &gt; reviveDelay + 0.1f.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
public class ZombieRevive : MonoBehaviour
{
    [Header("Revive Settings")]
    [Tooltip("Задержка перед подъёмом, сек")]
    [SerializeField] private float reviveDelay = 1.5f;

    [Tooltip("Сколько HP восстанавливается при подъёме (0..1 от maxHP)")]
    [SerializeField, Range(0.05f, 1f)] private float reviveHPPercent = 0.5f;

    [Tooltip("Сколько раз может воскреснуть. 1 = один раз, потом — окончательная смерть.")]
    [SerializeField] private int maxRevives = 1;

    [Tooltip("Аниматор-триггер при подъёме (опционально)")]
    [SerializeField] private string reviveTrigger = "Revive";

    private EnemyStats _stats;
    private Animator   _anim;
    private int        _revivesUsed;
    private Coroutine  _reviveCoroutine;

    private void Awake()
    {
        _stats = GetComponent<EnemyStats>();
        _anim  = GetComponentInChildren<Animator>();
    }

    private void OnEnable()  => _stats.OnDeath.AddListener(OnDied);
    private void OnDisable() => _stats.OnDeath.RemoveListener(OnDied);

    private void OnDestroy()
    {
        if (_reviveCoroutine != null) StopCoroutine(_reviveCoroutine);
    }

    private void OnDied()
    {
        if (_revivesUsed >= maxRevives) return;
        _revivesUsed++;
        _reviveCoroutine = StartCoroutine(ReviveRoutine());
    }

    private IEnumerator ReviveRoutine()
    {
        yield return new WaitForSeconds(reviveDelay);
        _reviveCoroutine = null;

        if (this == null || gameObject == null || _stats == null) yield break;

        if (_anim != null && !string.IsNullOrEmpty(reviveTrigger))
            _anim.SetTrigger(reviveTrigger);

        _stats.Revive(reviveHPPercent);
    }
}
