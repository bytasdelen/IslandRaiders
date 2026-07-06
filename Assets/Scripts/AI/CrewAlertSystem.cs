using UnityEngine;


public static class CrewAlertSystem
{
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
