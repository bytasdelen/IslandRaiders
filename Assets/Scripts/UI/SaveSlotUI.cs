using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// tek bir kayit satiri (prefab): kullanici adini gosterir, Load ve Delete butonlari.
// SaveMenuUI her kayit icin bu prefabtan bir tane olusturur.
public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;

    private string saveId;
    private Action<string> onLoad;
    private Action<string> onDelete;

    public void Bind(string id, string displayName, Action<string> loadCallback, Action<string> deleteCallback)
    {
        saveId = id;
        onLoad = loadCallback;
        onDelete = deleteCallback;
        label.text = displayName;

        loadButton.onClick.AddListener(HandleLoad);
        deleteButton.onClick.AddListener(HandleDelete);
    }

    private void HandleLoad()
    {
        onLoad?.Invoke(saveId);
    }

    private void HandleDelete()
    {
        onDelete?.Invoke(saveId);
    }
}
