using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : NetworkBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform pivot;
    [SerializeField] private Transform armPivot;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 80f;

    // pitch acisi sunucuya ve digerlerine yayilir, ateş sirasinda sunucunun
    // FirePoint yonunu dogru hesaplayabilmesi icin gereklidir
    private readonly NetworkVariable<float> networkPitch =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private InputAction lookAction;
    private float pitch;

    public override void OnNetworkSpawn()
    {
        ApplyPitch(networkPitch.Value);
        networkPitch.OnValueChanged += OnNetworkPitchChanged;

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        playerCamera.gameObject.SetActive(true);

        InputActionMap playerMap = inputActions.FindActionMap("Player");
        lookAction = playerMap.FindAction("Look");
        playerMap.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnNetworkPitchChanged(float previousValue, float newValue)
    {
        if (!IsOwner)
        {
            ApplyPitch(newValue);
        }
    }

    private void ApplyPitch(float pitchValue)
    {
        // kamera goz hizasindaki pivotta, kol/silah omuz hizasindaki pivotta ama ayni aciyla doner
        Quaternion rotation = Quaternion.Euler(pitchValue, 0f, 0f);
        pivot.localRotation = rotation;
        armPivot.localRotation = rotation;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool isLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isLocked;
        }

        Vector2 lookDelta = lookAction.ReadValue<Vector2>() * mouseSensitivity;

        // dumendeyken de govde donuyor (BoatCamera govdenin child'i oldugu icin onunla doner) -
        // bilerek boyle, ayri bir kamera-only rotasyon denemesi daha kotu hissettirmisti
        transform.Rotate(Vector3.up * lookDelta.x);

        pitch = Mathf.Clamp(pitch - lookDelta.y, minPitch, maxPitch);
        ApplyPitch(pitch);
        networkPitch.Value = pitch;
    }
}
