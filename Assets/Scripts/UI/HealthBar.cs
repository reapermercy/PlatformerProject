using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полоска здоровья. Подписывается на PlayerStats.OnHealthChanged.
/// Плавная анимация заполнения.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image delayedFillImage; // догоняющая полоска (для эффекта)
    [SerializeField] private float smoothSpeed = 5f;

    private float _targetFill = 1f;

    private void Update()
    {
        if (fillImage != null)
            fillImage.fillAmount = _targetFill;

        if (delayedFillImage != null)
            delayedFillImage.fillAmount = Mathf.Lerp(
                delayedFillImage.fillAmount, _targetFill, smoothSpeed * Time.deltaTime);
    }

    /// <summary>Подписать на PlayerStats.OnHealthChanged</summary>
    public void OnHealthChanged(int current, int max)
    {
        _targetFill = max > 0 ? (float)current / max : 0f;
    }
}
