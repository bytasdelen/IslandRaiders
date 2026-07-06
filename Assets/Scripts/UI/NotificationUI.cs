using System.Collections;
using TMPro;
using UnityEngine;
 
public class NotificationUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float displayDuration = 2.5f;
    [SerializeField] private float slideDistance = 40f;   // kac piksel yukaridan kayarak gelsin
    [SerializeField] private float slideDuration = 0.25f; // giriş/çıkış kayma suresi

    private RectTransform textTransform;
    private Vector2 restPosition;
    private Coroutine activeRoutine;

    private void Awake()
    { 
        textTransform = notificationText.rectTransform;
        restPosition = textTransform.anchoredPosition;
        SetAlpha(0f);
    }

    public void Show(string message)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(ShowRoutine(message));
    }

    private IEnumerator ShowRoutine(string message)
    {
        notificationText.text = message;

        Vector2 abovePosition = restPosition + Vector2.up * slideDistance;

        // yukaridan asagi kayarak belirginlesir
        yield return Animate(abovePosition, restPosition, 0f, 1f);

        yield return new WaitForSeconds(displayDuration);

        // yukari kayarak solar
        yield return Animate(restPosition, abovePosition, 1f, 0f);

        activeRoutine = null;
    }
     
    private IEnumerator Animate(Vector2 fromPos, Vector2 toPos, float fromAlpha, float toAlpha)
    {
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            textTransform.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, t);
            SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }
        textTransform.anchoredPosition = toPos;
        SetAlpha(toAlpha);
    }

    private void SetAlpha(float alpha)
    {
        notificationText.alpha = alpha;
    }
}
