using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    private readonly Dictionary<GameObject, Queue<NetworkObject>> pools = new Dictionary<GameObject, Queue<NetworkObject>>();
    private readonly Dictionary<BulletVisual, Queue<BulletVisual>> bulletPools = new Dictionary<BulletVisual, Queue<BulletVisual>>();
    private readonly Dictionary<BulletVisual, BulletVisual> bulletPrefabOf = new Dictionary<BulletVisual, BulletVisual>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        foreach (GameObject prefab in pools.Keys)
        {
            NetworkManager.Singleton.PrefabHandler.RemoveHandler(prefab);
        }
    }

    public NetworkObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(prefab, out Queue<NetworkObject> queue))
        {
            queue = new Queue<NetworkObject>();
            pools[prefab] = queue;
            NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, new PoolHandler(this, prefab));
        }

        NetworkObject instance = queue.Count > 0 ? queue.Dequeue() : Instantiate(prefab).GetComponent<NetworkObject>();
        instance.transform.SetParent(null);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        return instance;
    }

    private void Release(GameObject prefab, NetworkObject instance)
    {
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(null, false);
        pools[prefab].Enqueue(instance);
    }

    private class PoolHandler : INetworkPrefabInstanceHandler
    {
        private readonly PoolManager owner;
        private readonly GameObject prefab;

        public PoolHandler(PoolManager owner, GameObject prefab)
        {
            this.owner = owner;
            this.prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return owner.Get(prefab, position, rotation);
        }

        public void Destroy(NetworkObject networkObject)
        {
            owner.Release(prefab, networkObject);
        }
    }

    public BulletVisual GetBullet(BulletVisual prefab)
    {
        if (!bulletPools.TryGetValue(prefab, out Queue<BulletVisual> queue))
        {
            queue = new Queue<BulletVisual>();
            bulletPools[prefab] = queue;
        }

        BulletVisual instance = queue.Count > 0 ? queue.Dequeue() : Instantiate(prefab);
        bulletPrefabOf[instance] = prefab;
        instance.gameObject.SetActive(true);
        return instance;
    }

    public void ReleaseBullet(BulletVisual instance)
    {
        instance.gameObject.SetActive(false);
        if (bulletPrefabOf.TryGetValue(instance, out BulletVisual prefab))
        {
            bulletPools[prefab].Enqueue(instance);
        }
    }
}
