using UnityEngine;

/// <summary>
/// ORA ORA ORA — серия быстрых ударов в радиусе вокруг стенда.
/// </summary>
[CreateAssetMenu(fileName = "RapidPunch", menuName = "Stand/Abilities/RapidPunch")]
public class RapidPunchAbility : StandAbility
{
    [Header("Rapid Punch")]
    [SerializeField] private int hitCount = 5;
    [SerializeField] private int damagePerHit = 4;
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private float knockback = 6f;

    // Буфер — локальный, не статический:
    // RapidPunch — ScriptableObject-ассет; статические Collider2D[] в ScriptableObject
    // попадают под Unity PPtr-remap и вызывают assertion при горячей перезагрузке.
    // Способность срабатывает редко (не каждый кадр), поэтому локальная аллокация допустима.

    protected override void Execute(StandController stand)
    {
        Vector2 origin = stand.Position;

        // Бьём только по вражеским слоям — игрок не задет
        var filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(stand.EnemyLayers);

        var hits  = new Collider2D[16];
        int count = Physics2D.OverlapCircle(origin, radius, filter, hits);

        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit == null) continue;

            var handler = hit.GetComponent<DamageHandler>();
            if (handler != null)
                handler.ApplyDamage(damagePerHit * hitCount, DamageSource.Stand);

            var rb = hit.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 dir = ((Vector2)hit.transform.position - origin).normalized;
                rb.AddForce(dir * knockback, ForceMode2D.Impulse);
            }
        }
    }
}
