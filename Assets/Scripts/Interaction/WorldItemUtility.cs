using UnityEngine;

// yere birakilan/dusen esyalari konumlandirma yardimcilari (oyuncu drop'u, dusman silahi vb.)
public static class WorldItemUtility
{
    // pivotu merkezde olan objeler zemine gomulmesin diye collider alt sinirina gore yukari kaydirir
    public static void SnapToGround(GameObject obj, float groundY)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            return;
        }

        float minY = float.MaxValue;
        foreach (Collider col in colliders)
        {
            minY = Mathf.Min(minY, col.bounds.min.y);
        }

        float pivotToBottom = obj.transform.position.y - minY;
        Vector3 pos = obj.transform.position;
        pos.y = groundY + pivotToBottom;
        obj.transform.position = pos;
    }
}
