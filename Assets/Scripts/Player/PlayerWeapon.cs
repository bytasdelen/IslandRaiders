using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeapon : NetworkBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform aimSource;
    [SerializeField] private HeldItemDisplay heldItemDisplay;
    [SerializeField] private BulletVisual bulletPrefab;

    private InputAction attackAction;
    private float nextFireTime;

    public override void OnNetworkSpawn()
    {
        // sadece owner input'a abone olur; behaviour acik kalir ki sunucu tarafinda RPC islenebilsin
        if (!IsOwner)
        {
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player");
        attackAction = playerMap.FindAction("Attack");
        playerMap.Enable();
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        Weapon weapon = heldItemDisplay.CurrentWeapon;
        if (weapon == null)
        {
            return;
        }

        // otomatik silah basili tuttukca, yari-otomatik her basista ates eder
        bool wantsToFire = weapon.Automatic ? attackAction.IsPressed() : attackAction.WasPressedThisFrame();
        if (!wantsToFire || Time.time < nextFireTime)
        {
            return;
        }
        nextFireTime = Time.time + 1f / weapon.FireRate;

        // isabet kameradan (crosshair), mermi gorseli silahin namlusundan cikar
        FireServerRpc(aimSource.position, aimSource.forward, weapon.Muzzle.position);
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 origin, Vector3 direction, Vector3 muzzlePosition)
    {
        // silah statlari sunucudaki kendi kopyasindan okunur (guvenli)
        Weapon weapon = heldItemDisplay.CurrentWeapon;
        if (weapon == null)
        {
            return;
        }

        bool hitSomething = Physics.Raycast(origin, direction, out RaycastHit hit, weapon.Range);

        if (hitSomething && hit.collider.GetComponent<Hitbox>() is Hitbox hitbox)
        {
            bool hitSelf = hit.collider.GetComponentInParent<NetworkObject>() == NetworkObject;
            if (!hitSelf)
            {
                hitbox.TakeHit(weapon.Damage, OwnerClientId);
            }
        }

        Vector3 endPoint = hitSomething ? hit.point : origin + direction * weapon.Range;
        FireEffectClientRpc(muzzlePosition, endPoint);
    }

    [ClientRpc]
    private void FireEffectClientRpc(Vector3 start, Vector3 end)
    {
        BulletVisual bullet = Instantiate(bulletPrefab, start, Quaternion.identity);
        bullet.Launch(start, end);
    }
}
