using UnityEngine;

// silah sesi / sandik hirsizligi gibi olaylari SADECE ilgili geminin mürettebatina duyurur.
// mesafeye degil gemi hiyerarsisine bakar - baska bir gemiye asla sizmaz
public static class CrewAlertSystem
{
    // en az bir mürettebat bu cagriyla YENI dusman olduysa true doner (bildirim spamini onlemek icin)
    public static bool Notify(Vector3 sourcePosition, IShipDeck ship)
    {
        if (ship == null)
        {
            return false;
        }

        bool newlyAlerted = false;
        foreach (CrewMember crew in ship.NetworkObject.GetComponentsInChildren<CrewMember>())
        {
            if (crew.Alert(sourcePosition))
            {
                newlyAlerted = true;
            }
        }
        return newlyAlerted;
    }
}
