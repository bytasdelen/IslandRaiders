using UnityEngine;
using UnityEngine.UI;

// tek bir hotbar slotunun gorseli: ikon ve secili cercevesi
public class HotbarSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject selectedHighlight;

    public Button Button => button;

    public void SetIcon(Sprite icon)
    {
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    public void SetSelected(bool selected)
    {
        selectedHighlight.SetActive(selected);
    }
}
