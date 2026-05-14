using UnityEngine;

/// <summary>
/// Текущее контекстное состояние игрока.
/// Читается PlayerInputHandler для принятия решений по W и другим кнопкам.
/// </summary>
public class PlayerContext
{
    public bool IsNearLadder    { get; set; }
    public bool IsClimbing      { get; set; }
    public bool IsGrounded      { get; set; }
    public bool IsCrouching     { get; set; }
    public bool IsBlocking      { get; set; }
    public bool IsMounted       { get; set; }
    public bool StandIsFree          { get; set; } // стенд призван в Free режиме
    public bool StandManualControl   { get; set; } // RMB зажат, стенд управляется вручную
}
