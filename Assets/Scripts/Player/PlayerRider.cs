using Unity.Netcode;
using UnityEngine;

// oyuncunun gemide olma (parent) ve dumen surme durumunu yonetir.
// parent + local space senkronu karsi makinelerdeki kaymayi cozer;
// owner'in kendi makinesinde ise gemi hareketi CharacterController.Move ile uygulanir
// (aktif bir CharacterController parent'tan gelen tasimayi kabul etmez).
public class PlayerRider : NetworkBehaviour
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private GameObject fpsCamera;
    [SerializeField] private GameObject boatCamera;
    [SerializeField] private float groundCheckDistance = 1.5f;

    private IShipDeck currentBoat;
    public IShipDeck CurrentShip => currentBoat;

    private bool isDriving;
    private Transform pendingSeat;
    private Vector3 lastBoatPosition;
    private Quaternion lastBoatRotation;

    private void Update()
    {
        if (!IsOwner || isDriving)
        {
            return;
        }

        IShipDeck boatBelow = null;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance))
        {
            boatBelow = hit.collider.GetComponentInParent<IShipDeck>();
        }

        if (boatBelow == currentBoat)
        {
            return;
        }
        currentBoat = boatBelow;

        if (boatBelow != null)
        {
            lastBoatPosition = boatBelow.NetworkObject.transform.position;
            lastBoatRotation = boatBelow.NetworkObject.transform.rotation;
            SetParentServerRpc(boatBelow.NetworkObject);
        }
        else
        {
            ClearParentServerRpc();
        }
    }

    private void LateUpdate()
    {
        if (!IsOwner || currentBoat == null)
        {
            return;
        }

        Transform boat = currentBoat.NetworkObject.transform;

        // dumendeyken parent tasir (CC kapali); delta birikmesin diye referans guncel tutulur
        if (isDriving || !characterController.enabled)
        {
            lastBoatPosition = boat.position;
            lastBoatRotation = boat.rotation;
            return;
        }

        Vector3 posDelta = boat.position - lastBoatPosition;

        // gemi donusunun (yaw + dalga pitch/roll) pozisyon etkisi manuel uygulanir;
        // yon donusu parent hiyerarsisinden otomatik gelir, tekrar dondurulmez
        Quaternion rotDelta = boat.rotation * Quaternion.Inverse(lastBoatRotation);
        Vector3 offset = transform.position - boat.position;
        Vector3 followMove = posDelta + (rotDelta * offset - offset);

        // yatay Move isGrounded'i sifirlamasin diye yerdeyken minik asagi itis eklenir
        if (characterController.isGrounded)
        {
            followMove += Vector3.down * 0.05f;
        }
        characterController.Move(followMove);

        lastBoatPosition = boat.position;
        lastBoatRotation = boat.rotation;
    }

    [ServerRpc]
    private void SetParentServerRpc(NetworkObjectReference boatRef)
    {
        if (boatRef.TryGet(out NetworkObject boat))
        {
            NetworkObject.TrySetParent(boat, true);
        }
    }

    [ServerRpc]
    private void ClearParentServerRpc()
    {
        NetworkObject.TryRemoveParent(true);
    }

    public void SetPendingSeat(Transform seat)
    {
        pendingSeat = seat;
    }

    // dumene gecince/cikinca PlayerInteraction cagirir
    public void SetDriving(bool driving)
    {
        isDriving = driving;

        if (!IsOwner)
        {
            return;
        }

        characterController.enabled = !driving;
        playerController.enabled = !driving;
        playerWeapon.enabled = !driving;
        // playerLook acik kalir: dumendeyken de fare govdeyi (ve onunla BoatCamera'yi) dondurur
        fpsCamera.SetActive(!driving);
        boatCamera.SetActive(driving);

        if (driving && pendingSeat != null)
        {
            transform.position = pendingSeat.position;
            transform.rotation = pendingSeat.rotation;
        }
    }
}
