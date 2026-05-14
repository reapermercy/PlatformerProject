using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр всех доступных CharacterData в игре.
/// Нужен чтобы сохранять/загружать выбранного персонажа по имени
/// (ScriptableObject нельзя сериализовать в JSON напрямую).
///
/// Создать через: Assets → Create → JoJo → Character Registry
/// Положить в Assets/Resources/ с именем "CharacterRegistry", чтобы
/// GameManager мог загрузить через Resources.Load.
///
/// В инспекторе заполнить список всех CharacterData.asset проекта.
/// </summary>
[CreateAssetMenu(fileName = "CharacterRegistry", menuName = "JoJo/Character Registry")]
public class CharacterRegistry : ScriptableObject
{
    [Tooltip("Все CharacterData проекта. Порядок не важен.")]
    [SerializeField] private List<CharacterData> characters = new();

    private static CharacterRegistry _cachedInstance;

    /// <summary>Получить реестр (кешируется).</summary>
    public static CharacterRegistry Get()
    {
        if (_cachedInstance != null) return _cachedInstance;
        _cachedInstance = Resources.Load<CharacterRegistry>("CharacterRegistry");
        if (_cachedInstance == null)
            Debug.LogWarning("[CharacterRegistry] Не найден в Assets/Resources/CharacterRegistry.asset");
        return _cachedInstance;
    }

    /// <summary>Найти персонажа по имени (case-insensitive).</summary>
    public CharacterData FindByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var c in characters)
        {
            if (c == null) continue;
            if (string.Equals(c.characterName, name, System.StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    public IReadOnlyList<CharacterData> All => characters;
}
