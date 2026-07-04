using UnityEngine;

[System.Serializable]
public class ItemDefinition
{
    public string DisplayName;
    public Sprite Icon;
    public GameObject HeldPrefab;   // elde gosterilecek model (silahsa Weapon componenti icerir)
    public GameObject WorldPrefab;  // yere birakilinca spawn edilecek pickup objesi
}

// tum item turlerinin tek listesi; network'te sadece itemId (liste index'i) tasinir
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
