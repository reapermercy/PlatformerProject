using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Три слота способностей стенда.
/// Каждый слот: иконка + радиальный кулдаун-оверлей + текст оставшегося времени.
/// Обновляется каждый кадр из StandController.Data.abilities.
/// </summary>
public class AbilityIcons : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Вложенный тип
    // ──────────────────────────────────────────

    [System.Serializable]
    public class AbilitySlot
    {
        public Image    icon;
        public Image    cooldownOverlay;  // FillMethod = Radial360, fillAmount = 1 когда кулдаун, 0 когда готово
        public TMP_Text cooldownText;     // "2.5" сек, скрыт когда готово
        public TMP_Text keyHint;          // "Q" / "E" / "R" — статичная подсказка
    }

    // ──────────────────────────────────────────
    // Ссылки
    // ──────────────────────────────────────────

    [Header("References")]
    [SerializeField] private StandController standController;

    [Header("Slots (0 = Q, 1 = E, 2 = R)")]
    [SerializeField] private AbilitySlot[] slots = new AbilitySlot[3];

    // Последнее отображённое значение (в десятых долях сек) — чтобы не аллоцировать
    // новую строку через ToString("F1") каждый кадр. Обновляем только когда изменилось.
    private int[] _lastDisplayedDeci;

    // Предкешированные строки "0.0" .. "9.9" — хит на 99% случаев
    private static readonly string[] s_cooldownStrings = BuildCooldownStringCache();

    private static string[] BuildCooldownStringCache()
    {
        var arr = new string[100];  // 0.0 .. 9.9 — 100 значений
        for (int i = 0; i < arr.Length; i++)
            arr[i] = (i / 10f).ToString("F1");
        return arr;
    }

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _lastDisplayedDeci = new int[slots.Length];
        for (int i = 0; i < _lastDisplayedDeci.Length; i++)
            _lastDisplayedDeci[i] = -1;
    }

    private void Update()
    {
        if (standController == null || standController.Data == null) return;

        var abilities = standController.Data.abilities;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i >= abilities.Count || abilities[i] == null)
            {
                SetSlotVisible(slots[i], false);
                continue;
            }

            SetSlotVisible(slots[i], true);

            var ability = abilities[i];

            // Иконка способности
            if (slots[i].icon != null && ability.icon != null)
                slots[i].icon.sprite = ability.icon;

            // Прогресс кулдауна: 0 = готово, 1 = только что использовано
            float cooldownLeft = 1f - ability.CooldownProgress; // CooldownProgress идёт 0→1 при восстановлении
            // Если CooldownProgress == 1 → способность готова
            bool ready = ability.CooldownProgress >= 1f;

            if (slots[i].cooldownOverlay != null)
                slots[i].cooldownOverlay.fillAmount = ready ? 0f : cooldownLeft;

            if (slots[i].cooldownText != null)
            {
                if (ready)
                {
                    if (_lastDisplayedDeci[i] != 0)
                    {
                        slots[i].cooldownText.text    = string.Empty;
                        slots[i].cooldownText.enabled = false;
                        _lastDisplayedDeci[i] = 0;
                    }
                }
                else
                {
                    float remaining = ability.cooldown * cooldownLeft;
                    int   deci      = Mathf.CeilToInt(remaining * 10f);

                    if (deci != _lastDisplayedDeci[i])
                    {
                        // Используем кешированные строки для 0.0..9.9, иначе ToString
                        slots[i].cooldownText.text =
                            deci >= 0 && deci < s_cooldownStrings.Length
                                ? s_cooldownStrings[deci]
                                : remaining.ToString("F1");
                        slots[i].cooldownText.enabled = true;
                        _lastDisplayedDeci[i] = deci;
                    }
                }
            }
        }
    }

    // ──────────────────────────────────────────
    // Вспомогательные
    // ──────────────────────────────────────────

    private void SetSlotVisible(AbilitySlot slot, bool visible)
    {
        if (slot.icon            != null) slot.icon.enabled            = visible;
        if (slot.cooldownOverlay != null) slot.cooldownOverlay.enabled = visible;
        if (slot.cooldownText    != null) slot.cooldownText.enabled    = visible && false; // скрыт по дефолту
        if (slot.keyHint         != null) slot.keyHint.enabled         = visible;
    }
}
