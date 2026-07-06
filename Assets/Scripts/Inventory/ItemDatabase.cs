using UnityEngine;

[System.Serializable]
public class ItemDefinition
{
    public string DisplayName;
    public Sprite Icon;
    public GameObject HeldPrefab;   // elde gösterilecek model
    public GameObject WorldPrefab;  // yere bırakılınca spawn edilecek pickup objesi
    public bool IsChest;            
}

// tüm item türlerinin tek listesi; network'te sadece itemId (liste index'i) taşınır
[CreateAssetMenu(menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public ItemDefinition[] Items;

    public ItemDefinition Get(int id)
    {
        if (id < 0 || id >= Items.Length)
        {
            return null;
        }
        return Items[id];
    }
}
