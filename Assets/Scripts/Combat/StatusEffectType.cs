/// <summary>
/// Типы статус-эффектов.
///
/// Blind            — цель не видит игрока (EnemyAI не срабатывает detection)
/// Burn             — DoT урон каждые tickInterval секунд
/// Freeze           — цель не может двигаться и атаковать (полная остановка AI)
/// Stun             — цель оглушена, пропускает ход / не двигается (короче Freeze)
/// Slow             — скорость движения × magnitude (0..1)
/// MassIncrease     — утяжеление (для физики): увеличивает Rigidbody2D.mass × magnitude
/// InfiniteRotation — бесконечное вращение спрайта/объекта (визуальный дизориентирующий эффект)
/// </summary>
public enum StatusEffectType
{
    Blind,
    Burn,
    Freeze,
    Stun,
    Slow,
    MassIncrease,
    InfiniteRotation
}
