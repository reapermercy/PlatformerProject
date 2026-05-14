using UnityEngine;

/// <summary>
/// Данные предмета. Создаётся через Assets → Create → JoJo → Item Data.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "JoJo/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Effect")]
    public ItemType itemType;
    [Tooltip("Heal — HP, Score — очки")]
    public int value;

    [Header("World")]
    [Tooltip("Префаб спавнится когда предмет выбрасывают из инвентаря")]
    public GameObject worldPrefab;

    [Tooltip("Подбирается автоматически при касании игрока (без нажатия E)")]
    public bool autoPickup = false;

    [Tooltip("Стенд может физически схватить этот предмет")]
    public bool canBeGrabbedByStand = true;
}

public enum ItemType
{
    Generic,    // просто добавляется в инвентарь
    Heal,       // восстанавливает HP (value = кол-во HP)
    Score,      // добавляет очки (value = кол-во очков)
    Key         // открывает двери / рычаги
}
