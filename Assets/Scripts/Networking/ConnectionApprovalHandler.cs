using Unity.Netcode;
using UnityEngine;

// her baglanan oyuncuyu farkli bir spawn noktasinda olusturur
public class ConnectionApprovalHandler : MonoBehaviour
{
    [SerializeField] private Vector3[] spawnPositions;

    private int spawnIndex;

    private void Start()
    {
        // NGO'da bu property tek callback kabul eder, = ile atanir (+= degil)
        NetworkManager.Singleton.ConnectionApprovalCallback = HandleApproval;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
        }
    }

    private void HandleApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = true;

        if (spawnPositions.Length > 0)
        {
            response.Position = spawnPositions[spawnIndex % spawnPositions.Length];
            response.Rotation = Quaternion.identity;
            spawnIndex++;
        }
    }
}
