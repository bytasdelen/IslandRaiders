using System.Collections.Generic;
using UnityEngine;

// kayitli dunyalari (en yeniden eskiye) listeler. Her kayit icin scroll icerigine bir SaveSlotUI
// prefabi olusturur; Load host'u o dunyayla baslatir, Delete kaydi siler. Kac tanesinin ekranda
// gorunecegini (orn. 5) ve kaydirmayi Editor'daki ScrollRect + viewport yuksekligi belirler.
public class SaveMenuUI : MonoBehaviour
{
    [SerializeField] private ConnectionManager connectionManager;
    [SerializeField] private SaveSlotUI slotPrefab;
    [SerializeField] private Transform contentParent;   // ScrollRect'in Content objesi (VerticalLayoutGroup'lu)
    [SerializeField] private GameObject emptyLabel;      // opsiyonel: hic kayit yoksa gosterilir

    private readonly List<SaveSlotUI> spawnedSlots = new List<SaveSlotUI>();

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        foreach (SaveSlotUI slot in spawnedSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        spawnedSlots.Clear();

        List<SaveSystem.WorldSave> saves = SaveSystem.ListSaves();
        foreach (SaveSystem.WorldSave save in saves)
        {
            SaveSlotUI slot = Instantiate(slotPrefab, contentParent);
            slot.Bind(save.id, save.displayName, OnLoad, OnDelete);
            spawnedSlots.Add(slot);
        }

        if (emptyLabel != null)
        {
            emptyLabel.SetActive(saves.Count == 0);
        }
    }

    private void OnLoad(string saveId)
    {
        connectionManager.HostLoaded(saveId);
    }

    private void OnDelete(string saveId)
    {
        SaveSystem.DeleteWorld(saveId);
        Refresh();
    }
}
