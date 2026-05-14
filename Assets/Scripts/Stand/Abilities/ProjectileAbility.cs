using UnityEngine;

/// <summary>
/// Способность стенда — выпустить снаряд в направлении курсора мыши.
///
/// Настройка в StandData.abilities:
///   1. Create → Stand/Abilities/Projectile
///   2. Назначить projectilePrefab (содержит StandProjectile.cs)
///   3. Настроить speed, damage, lifetime
///   4. isHoming = true для Tusk Act 2 (пуля с наведением)
///   5. energyCost > 0 для расхода Хамона / Спина
///
/// Снаряд летит из позиции стенда к позиции курсора.
/// </summary>
[CreateAssetMenu(fileName = "ProjectileAbility", menuName = "Stand/Abilities/Projectile")]
public class ProjectileAbility : StandAbility
{
    // ──────────────────────────────────────────
    // Настройки снаряда
    // ──────────────────────────────────────────

    [Header("Projectile")]
    [Tooltip("Prefab с компонентом StandProjectile")]
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] private float speed          = 12f;
    [SerializeField] private int   damage         = 20;
    [SerializeField] private float lifetime       = 3f;

    [Header("Homing (Tusk Act 2)")]
    [Tooltip("Снаряд поворачивает к ближайшему врагу")]
    [SerializeField] private bool  isHoming       = false;
    [Tooltip("Скорость поворота в градусах/сек")]
    [SerializeField] private float homingStrength = 90f;

    [Header("Energy Cost (optional)")]
    [Tooltip("0 = бесплатно. > 0 = расход EnergySystem первого компонента на игроке.")]
    [SerializeField] private float energyCost = 0f;

    // ──────────────────────────────────────────
    // Реализация
    // ──────────────────────────────────────────

    protected override void Execute(StandController stand)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[ProjectileAbility] projectilePrefab не назначен!", this);
            return;
        }

        // Проверяем и тратим энергию
        if (energyCost > 0f)
        {
            var energy = stand.Owner?.GetComponent<EnergySystem>();
            if (energy != null && !energy.Consume(energyCost)) return;
        }

        Vector2 origin    = stand.Position;
        Vector2 mousePos  = stand.MouseWorldPosition;
        Vector2 direction = (mousePos - origin).normalized;

        // Если курсор в той же точке что и стенд — летим вправо
        if (direction == Vector2.zero)
            direction = stand.transform.right;

        var go   = Object.Instantiate(projectilePrefab, origin, Quaternion.identity);
        var proj = go.GetComponent<StandProjectile>();

        if (proj != null)
            proj.Init(direction, speed, damage, lifetime, isHoming, homingStrength, stand.EnemyLayers);
        else
            Debug.LogWarning("[ProjectileAbility] В projectilePrefab нет компонента StandProjectile!", go);
    }
}
