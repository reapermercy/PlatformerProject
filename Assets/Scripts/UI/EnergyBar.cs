using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD-шкала энергии (Хамон / Спин).
///
/// Настройка:
/// 1. Продублируй HealthBar в Canvas → переименуй в EnergyBar.
/// 2. Назначь этот компонент.
/// 3. Заполни fillImage (мгновенное изменение) и delayedFillImage (сглаженный хвост).
/// 4. В инспекторе EnergySystem → OnEnergyChanged → назначь этот объект → метод OnEnergyChanged.
///
/// Цвет fill:
///   Хамон → #FFD84D (жёлтый)
///   Спин  → #4DB8FF (голубой)
/// </summary>
public class EnergyBar : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("References")]
    [SerializeField] private Image fillImage;         // мгновенное изменение
    [SerializeField] private Image delayedFillImage;  // плавный хвост

    [Header("Animation")]
    [Tooltip("Скорость сглаживания хвоста")]
    [SerializeField] private float smoothSpeed = 5f;

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private float _targetFill = 1f;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Update()
    {
        if (delayedFillImage == null) return;
        delayedFillImage.fillAmount = Mathf.Lerp(
            delayedFillImage.fillAmount,
            _targetFill,
            Time.deltaTime * smoothSpeed);
    }

    // ──────────────────────────────────────────
    // API — подключить к EnergySystem.OnEnergyChanged в инспекторе
    // ──────────────────────────────────────────

    /// <summary>
    /// Вызывается при изменении энергии.
    /// Подключить: EnergySystem.OnEnergyChanged → EnergyBar.OnEnergyChanged
    /// </summary>
    public void OnEnergyChanged(float current, float max)
    {
        _targetFill = max > 0f ? current / max : 0f;

        if (fillImage != null)
            fillImage.fillAmount = _targetFill;
    }
}
