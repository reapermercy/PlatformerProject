using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Управляет стендами персонажа.
///
/// Инициализируется из CharacterData — туда смотреть за настройками.
///
/// Для персонажей с canManuallySwitch = true (Коичи):
///   SwitchNext() / SwitchPrev() — вызывать из Input.
///
/// Для сюжетной эволюции (Пуччи):
///   UnlockNextStand() — вызывать из триггера/диалога/события.
/// </summary>
public class StandSwitchManager : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Character")]
    [Tooltip("ScriptableObject текущего персонажа")]
    [SerializeField] private CharacterData characterData;

    [Header("References")]
    [SerializeField] private StandController standController;
    [SerializeField] private PlayerStats     playerStats;

    // ──────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────

    [Header("Events")]
    public UnityEvent<StandData>     OnStandSwitched;   // (новый стенд)
    public UnityEvent<CharacterData> OnCharacterSet;    // при смене персонажа

    // ──────────────────────────────────────────
    // Состояние
    // ──────────────────────────────────────────

    private readonly List<StandData> _unlocked = new(); // разблокированные стенды
    private int _currentIndex;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Start()
    {
        if (standController == null)
        {
            Debug.LogError("[StandSwitchManager] standController не назначен!", this);
            return;
        }

        if (characterData != null)
            ApplyCharacter(characterData);
    }

    // ──────────────────────────────────────────
    // Смена персонажа (например, при загрузке другого уровня)
    // ──────────────────────────────────────────

    /// <summary>
    /// Установить нового персонажа — обновляет список стендов и применяет первый.
    /// Вызывай при старте уровня или загрузке сохранения.
    /// </summary>
    public void ApplyCharacter(CharacterData data)
    {
        if (data == null) return;

        characterData = data;
        _unlocked.Clear();
        _currentIndex = 0;

        // Разблокируем initialUnlockedCount стендов
        int count = Mathf.Clamp(data.initialUnlockedCount, 1, data.stands.Count);
        for (int i = 0; i < count; i++)
            if (data.stands[i] != null)
                _unlocked.Add(data.stands[i]);

        ApplyCurrentStand();
        OnCharacterSet?.Invoke(data);
    }

    // ──────────────────────────────────────────
    // Ручное переключение (Коичи / любой canManuallySwitch)
    // ──────────────────────────────────────────

    /// <summary>Переключить на следующий стенд. Только если canManuallySwitch = true.</summary>
    public void SwitchNext()
    {
        if (!CanSwitch()) return;
        _currentIndex = (_currentIndex + 1) % _unlocked.Count;
        ApplyCurrentStand();
    }

    /// <summary>Переключить на предыдущий стенд. Только если canManuallySwitch = true.</summary>
    public void SwitchPrev()
    {
        if (!CanSwitch()) return;
        _currentIndex = (_currentIndex - 1 + _unlocked.Count) % _unlocked.Count;
        ApplyCurrentStand();
    }

    /// <summary>Переключить на конкретный индекс (0-based).</summary>
    public void SwitchToIndex(int index)
    {
        if (!CanSwitch()) return;
        if (index < 0 || index >= _unlocked.Count) return;
        _currentIndex = index;
        ApplyCurrentStand();
    }

    // ──────────────────────────────────────────
    // Сюжетная разблокировка (Пуччи)
    // ──────────────────────────────────────────

    /// <summary>
    /// Разблокировать следующий стенд из списка персонажа.
    /// Whitesnake → C-Moon → Made in Heaven.
    /// После разблокировки автоматически переключает на новый стенд.
    /// </summary>
    public void UnlockNextStand()
    {
        if (characterData == null) return;

        int nextIndex = _unlocked.Count; // следующий ещё не разблокированный
        if (nextIndex >= characterData.stands.Count)
        {
            Debug.Log($"[StandSwitch] {characterData.characterName}: все стенды уже разблокированы.");
            return;
        }

        var newStand = characterData.stands[nextIndex];
        if (newStand == null) return;

        _unlocked.Add(newStand);
        _currentIndex = _unlocked.Count - 1; // сразу переходим на новый
        ApplyCurrentStand();

        Debug.Log($"[StandSwitch] {characterData.characterName} → {newStand.standName} разблокирован!");
    }

    /// <summary>Разблокировать конкретный стенд по ссылке (если не по порядку).</summary>
    public void UnlockStand(StandData stand)
    {
        if (stand == null || _unlocked.Contains(stand)) return;
        if (characterData == null || !characterData.stands.Contains(stand)) return;

        _unlocked.Add(stand);
        Debug.Log($"[StandSwitch] Разблокирован: {stand.standName}");
    }

    // ──────────────────────────────────────────
    // Внутренние
    // ──────────────────────────────────────────

    private void ApplyCurrentStand()
    {
        if (_unlocked.Count == 0 || standController == null) return;

        var stand = _unlocked[_currentIndex];
        standController.ApplyStandData(stand);
        OnStandSwitched?.Invoke(stand);
    }

    private bool CanSwitch()
    {
        if (characterData == null || !characterData.canManuallySwitch)
        {
            Debug.Log($"[StandSwitch] Ручное переключение недоступно для {characterData?.characterName}.");
            return false;
        }
        if (_unlocked.Count <= 1) return false;
        return true;
    }

    // ──────────────────────────────────────────
    // Свойства
    // ──────────────────────────────────────────

    public CharacterData              Character       => characterData;
    public StandData                  CurrentStand    => _unlocked.Count > 0 ? _unlocked[_currentIndex] : null;
    public IReadOnlyList<StandData>   UnlockedStands  => _unlocked;
    public bool                       CanManualSwitch => characterData != null && characterData.canManuallySwitch;
}
