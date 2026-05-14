using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Глобальный реестр всех IInteractable на сцене.
/// Объекты регистрируются сами через OnEnable/OnDisable.
/// Позволяет стенду находить рычаги и двери без коллайдеров.
/// </summary>
public static class InteractableRegistry
{
    private static readonly List<MonoBehaviour> _all = new();

    /// <summary>Зарегистрировать интерактивный объект (вызывать из OnEnable).</summary>
    public static void Register(MonoBehaviour mb)
    {
        if (!_all.Contains(mb))
            _all.Add(mb);
    }

    /// <summary>Снять регистрацию (вызывать из OnDisable).</summary>
    public static void Unregister(MonoBehaviour mb)
    {
        _all.Remove(mb);
    }

    /// <summary>Все зарегистрированные объекты.</summary>
    public static IReadOnlyList<MonoBehaviour> All => _all;
}
