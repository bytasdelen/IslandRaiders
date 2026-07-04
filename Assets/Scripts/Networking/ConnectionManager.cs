using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private UnityTransport unityTransport; 

    private void Awake()
    {
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);
    }

    private void OnDestroy()
    {
        hostButton.onClick.RemoveListener(StartHost);
        joinButton.onClick.RemoveListener(StartClient);
    }

    private void StartHost()
    {
        SetStatus("Starting host...");
        NetworkManager.Singleton.OnClientConnectedCallback += OnAnyClientConnected;
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void StartClient()
    {
        string ip = ipInputField.text;
        unityTransport.ConnectionData.Address = ip;

        SetStatus($"Connecting to {ip}...");
        NetworkManager.Singleton.OnClientConnectedCallback += OnAnyClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.StartClient();
    }

    private void OnAnyClientConnected(ulong clientId)
    {
        SetStatus($"Connected (clientId: {clientId})");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        SetStatus("Failed to connect.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}

