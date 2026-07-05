using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// oyuncunun bakisi su altina inince, elle kurulmus tam ekran bir Image'in alpha'sini
// derinlige gore acar/kapatir. HICBIR SEY runtime'da uretilmez - overlay Image'i sen
// Canvas altinda olusturup asagidaki underwaterOverlay alanina baglarsin. Sadece owner'da calisir.
[RequireComponent(typeof(PlayerController))]
public class PlayerSwimEffects : NetworkBehaviour
{
    // goz hizasindaki referans (kamera pitch pivotu); su altinda miyiz diye bunun Y'sine bakariz
    [SerializeField] private Transform viewpoint;
    // Canvas altinda elle kurulan, ekrani kaplayan Image (Inspector'dan baglanir)
    [SerializeField] private Image underwaterOverlay;
    [SerializeField] private Color underwaterTint = new Color(0.1f, 0.35f, 0.55f, 1f);
    // bu derinlikte (metre) ton en koyu haline ulasir
    [SerializeField] private float maxTintDepth = 3f;
    [SerializeField] private float maxAlpha = 0.55f;
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private float waterLevelOffset = 1f;

    private PlayerController controller;
    private float currentAlpha;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        // baslangicta gizli; owner degilsek hic dokunma
        SetAlpha(0f);

        if (!IsOwner)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        if (underwaterOverlay == null || viewpoint == null)
        {
            return;
        }

        float targetAlpha = 0f;

        // gemideyken (PlayerController zaten rider.CurrentShip kontrolu yapiyor) yuzme
        // moduna hic girilmiyor - ayni bayragi burada da kullanip alt katta yanlis mavi
        // tonun tetiklenmesini onluyoruz
        if (controller.IsSwimming)
        {
            // gozun su yuzeyinin ne kadar altinda oldugu; yuzeydeyken 0, dalinca artar
            float depth = controller.WaterLevel - viewpoint.position.y + waterLevelOffset;
            if (depth > 0f)
            {
                targetAlpha = Mathf.Clamp01(depth / maxTintDepth) * maxAlpha;
            }
        }

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
        SetAlpha(currentAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (underwaterOverlay == null)
        {
            return;
        }

        Color color = underwaterTint;
        color.a = alpha;
        underwaterOverlay.color = color;
    }
}
