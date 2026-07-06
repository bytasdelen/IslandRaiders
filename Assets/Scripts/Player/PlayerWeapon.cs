using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeapon : NetworkBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform aimSource;
    [SerializeField] private HeldItemDisplay heldItemDisplay;
    [SerializeField] private BulletVisual bulletPrefab;
    [SerializeField] private PlayerRider rider;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private NotificationUI notificationUI;
    [SerializeField] private PlayerNotifications notifications;

    private InputAction attackAction;
    private float nextFireTime;
    private float nextEmptyAmmoWarningTime;

    public override void OnNetworkSpawn()
    { 
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

        if (inventory.GetSelectedAmmo() <= 0)
        {
            // otomatik silahta basili tutulunca her frame tetiklenmesin diye cooldown
            if (Time.time >= nextEmptyAmmoWarningTime)
            {
                notificationUI.Show("Insufficient ammo");
                nextEmptyAmmoWarningTime = Time.time + 1f;
            }
            return;
        }
        nextFireTime = Time.time + 1f / weapon.FireRate;

        // isabet kameradan (crosshair), mermi gorseli silahin namlusundan çıkar
        FireServerRpc(aimSource.position, aimSource.forward, weapon.Muzzle.position);
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 origin, Vector3 direction, Vector3 muzzlePosition)
    {
        Weapon weapon = heldItemDisplay.CurrentWeapon;
        if (weapon == null || inventory.GetSelectedAmmo() <= 0)
        {
            return;
        }
        inventory.ConsumeSelectedAmmo();

        bool hitSomething = TryGetWeaponHit(origin, direction, weapon.Range, out RaycastHit hit);

        if (hitSomething && hit.collider.GetComponent<Hitbox>() is Hitbox hitbox)
        {
            bool hitSelf = hit.collider.GetComponentInParent<NetworkObject>() == NetworkObject;
            if (!hitSelf)
            {
                hitbox.TakeHit(weapon.Damage, OwnerClientId);
            }
        }

        // sadece gemideyken ates edersek o geminin mürettebatini uyarir, baska gemiye sizmaz
        if (rider.CurrentShip != null && CrewAlertSystem.Notify(origin, rider.CurrentShip))
        {
            notifications.NotifyOwner("The crew has launched an attack!");
        }

        Vector3 endPoint = hitSomething ? hit.point : origin + direction * weapon.Range;
        FireEffectClientRpc(muzzlePosition, endPoint);
    }

    private bool TryGetWeaponHit(Vector3 origin, Vector3 direction, float range, out RaycastHit validHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, range,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            validHit = hit;
            return true;
        }

        validHit = default;
        return false;
    }

    [ClientRpc]
    private void FireEffectClientRpc(Vector3 start, Vector3 end)
    {
        BulletVisual bullet = PoolManager.Instance.GetBullet(bulletPrefab);
        bullet.Launch(start, end);
    }
}
