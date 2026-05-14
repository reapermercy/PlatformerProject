using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Иконка стенда в HUD.
/// — В режиме Attached: пульсирует (scale)
/// — В режиме Free/Auto: статичный, меняется цвет
/// — Показывает текстовую метку режима
///
/// Подписывается на StandController.OnModeChanged UnityEvent.
/// Также можно вызывать OnStandSwitched при смене стенда.
/// </summary>
public class StandIndicator : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Ссылки
    // ──────────────────────────────────────────

    [Header("References")]
    [SerializeField] private StandController standController;

    [Header("UI")]
    [SerializeField] private Image    standIcon;
    [SerializeField] private TMP_Text modeLabel;

    // ──────────────────────────────────────────
    // Цвета по режиму
    // ──────────────────────────────────────────

    [Header("Colors")]
    [SerializeField] private Color colorAttached = new Color(0.3f, 0.8f, 1f);   // голубой
    [SerializeField] private Color colorFree     = new Color(1f,   0.8f, 0.2f); // жёлтый
    [SerializeField] private Color colorAuto     = new Color(0.6f, 1f,   0.4f); // зелёный

    // ──────────────────────────────────────────
    // Пульсация (только Attached)
    // ──────────────────────────────────────────

    [Header("Pulse (Attached mode)")]
    [SerializeField] private float pulseSpeed     = 2f;
    [SerializeField] private float pulseAmplitude = 0.08f;

    // ──────────────────────────────────────────
    // Приватные
    // ──────────────────────────────────────────

    private Vector3   _baseScale;
    private StandMode _currentMode = StandMode.Attached;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (standIcon != null)
            _baseScale = standIcon.transform.localScale;
    }

    private void OnEnable()
    {
        if (standController != null)
            standController.OnModeChanged.AddListener(OnModeChanged);
    }

    private void OnDisable()
    {
        if (standController != null)
            standController.OnModeChanged.RemoveListener(OnModeChanged);
    }

    private void Start()
    {
        // Синхронизация с текущим режимом при старте
        if (standController != null)
            OnModeChanged(standController.CurrentMode);
    }

    private void Update()
    {
        if (standIcon == null) return;

        if (_currentMode == StandMode.Attached)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * pulseAmplitude;
            standIcon.transform.localScale = _baseScale * pulse;
        }
        else
        {
            standIcon.transform.localScale = _baseScale;
        }
    }

    // ──────────────────────────────────────────
    // Callbacks
    // ──────────────────────────────────────────

    /// <summary>
    /// Вызывается при смене режима стенда (через UnityEvent или вручную).
    /// </summary>
    public void OnModeChanged(StandMode mode)
    {
        _currentMode = mode;

        Color col = mode switch
        {
            StandMode.Free      => colorFree,
            StandMode.Automatic => colorAuto,
            StandMode.SemiAuto  => colorAuto,
            _                   => colorAttached,
        };

        string label = mode switch
        {
            StandMode.Free      => "СВОБОДЕН",
            StandMode.Automatic => "АВТО",
            StandMode.SemiAuto  => "ПОЛУ-АВТО",
            _                   => "СТЕНД",
        };

        if (standIcon != null) standIcon.color = col;
        if (modeLabel  != null) modeLabel.text  = label;
    }

    /// <summary>
    /// Вызывается при смене активного стенда.
    /// </summary>
    public void OnStandSwitched(StandData data)
    {
        if (standIcon == null || data == null) return;

        if (data.standSprite != null)
        {
            standIcon.sprite  = data.standSprite;
            standIcon.enabled = true;
        }
    }
}
