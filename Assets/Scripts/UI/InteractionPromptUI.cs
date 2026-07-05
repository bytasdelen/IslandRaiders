using TMPro;
using UnityEngine;

// nisan alinan seyle etkilesim ipucunu ekranda gosterir ("(E) Loot", "(E) Interact").
// NotificationUI'nin aksine suresiz kalir: PlayerInteraction her frame Show/Hide cagirir
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI promptText;

    private void Awake()
    {
        promptText.enabled = false;
    }

    public void Show(string message)
    {
        // ayni metin tekrar tekrar atanmasin diye sadece degisince guncelle
        if (promptText.enabled && promptText.text == message)
        {
            return;
        }
        promptText.text = message;
        promptText.enabled = true;
    }

    public void Hide()
    {
        promptText.enabled = false;
    }
}
