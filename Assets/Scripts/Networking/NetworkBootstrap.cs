using UnityEngine;
public class NetworkBootstrap : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}

