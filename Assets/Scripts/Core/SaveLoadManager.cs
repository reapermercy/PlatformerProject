using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Сохранение / загрузка через JSON.
/// 3 слота. Хранит: сцену, счёт, дату.
/// </summary>
public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    public const int MaxSlots = 3;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ──────────────────────────────────────────
    // Сохранение
    // ──────────────────────────────────────────

    /// <summary>
    /// Сохранить в слот. Данные берутся автоматически из синглтонов.
    /// </summary>
    public void Save(int slot)
    {
        slot = Clamp(slot);

        var score   = ScoreManager.Instance;
        var gm      = GameManager.Instance;
        var charSel = gm != null ? gm.SelectedCharacter : null;

        var data = new SaveData
        {
            totalScore       = score != null ? score.TotalScore + score.LevelScore : 0,
            totalPlayTime    = score != null ? score.TotalPlayTime : 0f,
            lastScene        = SceneManager.GetActiveScene().name,
            currentLevel     = gm != null ? gm.CurrentLevel : 1,
            characterName    = charSel != null ? charSel.characterName : string.Empty,
            saveDate         = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            slotName         = $"Слот {slot + 1}"
        };

        try
        {
            File.WriteAllText(GetPath(slot), JsonUtility.ToJson(data, true));
            Debug.Log($"[Save] Slot {slot} saved → {data.lastScene}, score {data.totalScore}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Не удалось записать слот {slot}: {e.Message}");
        }
    }

    // ──────────────────────────────────────────
    // Загрузка
    // ──────────────────────────────────────────

    /// <summary>
    /// Загрузить слот и перейти в сохранённую сцену.
    /// </summary>
    public void LoadGame(int slot)
    {
        var data = ReadSlot(slot);
        if (data == null) { Debug.LogWarning($"[Load] Slot {slot} is empty."); return; }

        // Восстановить счёт и время до загрузки сцены
        ScoreManager.Instance?.SetTotalScore(data.totalScore);
        ScoreManager.Instance?.SetTotalPlayTime(data.totalPlayTime);
        GameManager.Instance?.SetCurrentLevel(data.currentLevel);

        // Восстановить выбранного персонажа через реестр
        if (!string.IsNullOrEmpty(data.characterName))
        {
            var registry = CharacterRegistry.Get();
            var ch       = registry?.FindByName(data.characterName);
            if (ch != null) GameManager.Instance?.SelectCharacter(ch);
            else Debug.LogWarning($"[Load] Персонаж '{data.characterName}' не найден в CharacterRegistry.");
        }

        GameManager.Instance?.LoadScene(data.lastScene);

        Debug.Log($"[Load] Slot {slot} → {data.lastScene}, score {data.totalScore}");
    }

    // ──────────────────────────────────────────
    // Удаление
    // ──────────────────────────────────────────

    public void DeleteSlot(int slot)
    {
        try
        {
            string path = GetPath(Clamp(slot));
            if (File.Exists(path)) File.Delete(path);
            Debug.Log($"[Save] Slot {slot} deleted.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Не удалось удалить слот {slot}: {e.Message}");
        }
    }

    // ──────────────────────────────────────────
    // Инфо о слоте
    // ──────────────────────────────────────────

    /// <summary>Прочитать данные слота для отображения в UI. null если пуст.</summary>
    public SaveData ReadSlot(int slot)
    {
        try
        {
            string path = GetPath(Clamp(slot));
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Не удалось прочитать слот {slot}: {e.Message}");
            return null;
        }
    }

    public bool SlotExists(int slot)
    {
        try   { return File.Exists(GetPath(Clamp(slot))); }
        catch { return false; }
    }

    // ──────────────────────────────────────────
    // Вспомогательные
    // ──────────────────────────────────────────

    private string GetPath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save_slot{slot}.json");

    private int Clamp(int slot) => Mathf.Clamp(slot, 0, MaxSlots - 1);
}

// ──────────────────────────────────────────
// Структура данных сохранения
// ──────────────────────────────────────────

[Serializable]
public class SaveData
{
    public string slotName;        // "Слот 1"
    public string lastScene;       // имя сцены Unity
    public int    currentLevel;    // 1-5
    public int    totalScore;      // накопленный счёт
    public float  totalPlayTime;   // суммарное время игры в секундах
    public string characterName;   // имя выбранного CharacterData (резолвится через CharacterRegistry)
    public string saveDate;        // "16.04.2026 21:30"
}
