using UnityEngine;

/// <summary>
/// Ссылки на физическое тело стенда, которые воплощение получает при Setup().
///
/// ВАЖНО (отличие от скелета docs/StandSystem/StandSystem.cs):
/// тело здесь НЕ инстанцируется воплощением. Текущий стенд — отдельный
/// GameObject, на котором и живёт StandController, а его методы прошиты
/// в Player.prefab через UnityEvent по именам. Пересборка топологии сломала бы
/// эти связи, поэтому на шаге 1 воплощение работает с уже существующим телом.
/// Инстанцирование префаба появится, когда сущность переедет на владельца.
/// </summary>
public sealed class StandBody
{
    public Transform       Transform  { get; }
    public Rigidbody2D     Rigidbody  { get; }
    public Collider2D      Collider   { get; }
    public GameObject      VisualRoot { get; }
    public Animator        Animator   { get; }

    /// <summary>Контроллер — источник живых StandData и владельца (меняются в рантайме).</summary>
    public StandController Controller { get; }

    public StandBody(
        StandController controller,
        Transform       transform,
        Rigidbody2D     rigidbody,
        Collider2D      collider,
        GameObject      visualRoot,
        Animator        animator)
    {
        Controller = controller;
        Transform  = transform;
        Rigidbody  = rigidbody;
        Collider   = collider;
        VisualRoot = visualRoot;
        Animator   = animator;
    }

    // Живые данные — читаются каждый раз, а не кэшируются:
    // StandSwitchManager меняет StandData на лету.
    public StandData Data  => Controller.Data;
    public Transform Owner => Controller.Owner;
}

/// <summary>
/// Настройки следования, отданные воплощению из инспектора StandController.
/// Поля остаются сериализованными на контроллере — иначе Unity потеряет
/// значения, настроенные в Stand.prefab.
/// </summary>
public struct FollowerTuning
{
    public float   returnDelay;
    public float   idleReturnDelay;
    public Vector2 idleOffset;
    public float   springStrength;
    public float   springDamping;
}

/// <summary>Хэши параметров аниматора — общие для контроллера и воплощений.</summary>
public static class StandAnimHashes
{
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");
    public static readonly int Return   = Animator.StringToHash("Return");
    public static readonly int Summon   = Animator.StringToHash("Summon");
    public static readonly int Attack   = Animator.StringToHash("Attack");
    public static readonly int Ability  = Animator.StringToHash("Ability");
}
