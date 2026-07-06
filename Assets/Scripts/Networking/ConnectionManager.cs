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
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private UnityTransport unityTransport; 

    private void Awake()
    {
        hostButton.onClick.AddListener(HostNewGame);
        joinButton.onClick.AddListener(StartClient);
    }

    private void OnDestroy()
    {
        hostButton.onClick.RemoveListener(HostNewGame);
        joinButton.onClick.RemoveListener(StartClient);
    }

    // Host butonu: bos bir dunyayla baslar; girilen kullanici adi kaydin anahtari olur
    public void HostNewGame()
    {
        SaveSystem.NewGame(nameInputField.text);
        StartHost();
    }

    // menudeki bir kayit satirindan cagirilir: o dunyayi yukleyip host baslatir.
    // isim alani kaydin sahibi isimle DOLDURULUR - SendPlayerName bunu gonderir, boylece baglanan
    // isim kayittaki isimle birebir eslesir (aksi halde oyuncunun kendi verisi/konumu bulunamaz)
    public void HostLoaded(string saveId)
    {
        SaveSystem.WorldSave save = SaveSystem.PrepareLoad(saveId);
        if (save != null)
        {
            nameInputField.text = save.displayName;
        }
        StartHost();
    }

    private void StartHost()
    {
        SetStatus("Starting host...");
        SendPlayerName();
        NetworkManager.Singleton.OnClientConnectedCallback += OnAnyClientConnected;
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void StartClient()
    {
        string ip = ipInputField.text;
        unityTransport.ConnectionData.Address = ip;

        SetStatus($"Connecting to {ip}...");
        SendPlayerName();
        NetworkManager.Singleton.OnClientConnectedCallback += OnAnyClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.StartClient();
    }
     
    private void SendPlayerName()
    {
        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(nameInputField.text);
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

