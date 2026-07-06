using TMPro;
using UnityEngine;
 
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI promptText;

    private void Awake()
    {
        promptText.enabled = false;
    }

    public void Show(string message)
    { 
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
