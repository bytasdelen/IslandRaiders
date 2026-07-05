using System.Collections.Generic;
using UnityEngine;

// bir ada; gemilerin once geldigi yaklasma noktasini ve yanasacagi dock noktalarini tutar.
// dock slotlari sadece sunucuda rezerve edilir (tum ai zaten sunucuda kosuyor) - ayni slota
// iki gemi yanasamaz. Aglama gerekmez, gemi hareketini NetworkTransform senkronlar.
// Deniz yolu NavMesh uzerinden bulundugu icin burada graf/node yok; approachPoint ve
// dockPoints sadece dunya konumlaridir.
public class Island : MonoBehaviour
{
    [SerializeField] private Transform approachPoint;   // gemi once buraya gelir, slot bekliyorsa burada bekler
    [SerializeField] private Transform[] dockPoints;    // gemilerin yanasacagi noktalar

    // gemiler random hedef secerken tum adalari gezebilsin diye kendini kaydeder
    private static readonly List<Island> all = new List<Island>();
    public static IReadOnlyList<Island> All => all;

    // her slot icin o an rezerve eden gemi (null = bos)
    private AIShipController[] occupants;

    public Transform ApproachPoint => approachPoint;

    private void Awake()
    {
        occupants = new AIShipController[dockPoints.Length];
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    // bos bir slot ayirir; hepsi doluysa -1 doner
    public int TryReserveDock(AIShipController ship)
    {
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == null)
            {
                occupants[i] = ship;
                return i;
            }
        }
        return -1;
    }

    public Transform GetDockPoint(int slot)
    {
        return dockPoints[slot];
    }

    // gemi ayrilirken slotu birakir; baskasinin rezervasyonunu silmemek icin sadece
    // o slotu gercekten tutan gemi birakabilir
    public void ReleaseDock(int slot, AIShipController ship)
    {
        if (slot >= 0 && slot < occupants.Length && occupants[slot] == ship)
        {
            occupants[slot] = null;
        }
    }
}
