using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    // yüzerken hareket yönü bakış yönünden alınır, bu pivot kamera pitch'ini tutar
    [SerializeField] private Transform lookPivot;
    [SerializeField] private PlayerRider rider;

    [Header("Ground Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2.2f;
    [SerializeField] private float acceleration = 0.12f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;

    [Header("Swimming")]
    [SerializeField] private float waterLevel = -1.2f;
    [SerializeField] private float swimSpeedMultiplier = 0.4f;
    [SerializeField] private float swimAcceleration = 0.3f;
    // sudan yukari dogru itme kuvveti, karakteri yüzeye taşır
    [SerializeField] private float buoyancy = 4f;
    // ayaktan ölçülen, karakterin sakin haldeyken oturduğu su derinligi (düşük değer = daha yüksekte yüzer)
    [SerializeField] private float floatDepth = 1.1f;
    // yüzmeye başlama/bitme eşikleri arasındaki fark
    [SerializeField] private float swimStartDepth = 1.3f;
    [SerializeField] private float swimStopDepth = 0.8f;

    [Header("Climbing")]
    // yuzerken bakilan yondeki gemi guvertesine ya da ada kiyisina Space ile cikmak icin
    [SerializeField] private float climbReach = 1.6f;      // bakis yonunde govdenin ne kadar onune uzanip kenar aranir
    [SerializeField] private float maxClimbHeight = 2.5f;  // ayaklardan en fazla bu kadar yukaridaki yuzeye cikilir

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Vector3 currentVelocity;
    private Vector3 velocitySmoothDamp;
    private float verticalVelocity;
    private bool isSwimming; 
    public bool IsSwimming => isSwimming;
    public float WaterLevel => waterLevel;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player");
        moveAction = playerMap.FindAction("Move");
        sprintAction = playerMap.FindAction("Sprint");
        jumpAction = playerMap.FindAction("Jump");
        playerMap.Enable();
    }

    private void Update()
    {
        UpdateSwimState();

        if (isSwimming)
        {
            // yuzerken kenara bakip Space'e basinca yukari tirman; cikilacak yer yoksa normal yuzme
            if (jumpAction.WasPressedThisFrame() && TryClimbOut())
            {
                return;
            }
            SwimMovement();
        }
        else
        {
            GroundMovement();
        }
    }

    // bakis yonundeki yakin bir gemi guvertesine / ada kiyisina cikmayi dener, basarirsa true doner
    private bool TryClimbOut()
    {
        Vector3 forward = lookPivot != null ? lookPivot.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            return false;
        }
        forward.Normalize();

        float feetY = transform.position.y + characterController.center.y - characterController.height * 0.5f;

        // govdenin biraz onunde, tirmanma tepesinden asagi isin at; ustune basilacak yuzeyi ara
        Vector3 probeTop = transform.position + forward * climbReach;
        probeTop.y = feetY + maxClimbHeight;

        if (!Physics.Raycast(probeTop, Vector3.down, out RaycastHit hit, maxClimbHeight, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // sadece gemi ya da adaya cikilir; yuzey ayaklardan yukarida ve yurunebilir egimde olmali
        bool boardable = hit.collider.GetComponentInParent<IShipDeck>() != null
                      || hit.collider.GetComponentInParent<Island>() != null;
        if (!boardable || hit.point.y < feetY - 0.2f || Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
        {
            return false;
        }

        // ayaklari bulunan yuzeyin ustune oturt, kenardan biraz iceri kaydir
        float bodyOffset = characterController.height * 0.5f - characterController.center.y + characterController.skinWidth;
        Vector3 landing = hit.point + forward * 0.2f;
        landing.y = hit.point.y + bodyOffset;

        characterController.enabled = false;
        transform.position = landing;
        characterController.enabled = true;

        isSwimming = false;
        verticalVelocity = 0f;
        currentVelocity = Vector3.zero;
        velocitySmoothDamp = Vector3.zero;
        return true;
    }

    // ayaklarin ne kadar suya girdiğine göre yüzme moduna geç/çık, esikler arasi bosluk histerezis sağlar
    private void UpdateSwimState()
    {
        // gemi uzerindeysen asla yüzme moduna girme
        if (rider != null && rider.CurrentShip != null)
        {
            isSwimming = false;
            return;
        }

        float feetY = transform.position.y + characterController.center.y - characterController.height * 0.5f;
        float submersion = waterLevel - feetY;

        if (!isSwimming && submersion > swimStartDepth)
        {
            isSwimming = true;
            // suya girerken birikmis dusme hizini sifirl�yoruz ki dibe cakilmasin
            verticalVelocity = 0f;
        }
        else if (isSwimming && submersion < swimStopDepth)
        {
            isSwimming = false;
        }
    }

    private void GroundMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 inputDirection = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        float speed = moveSpeed * (sprintAction.IsPressed() ? sprintMultiplier : 1f);
        Vector3 targetVelocity = transform.TransformDirection(inputDirection) * speed;
        currentVelocity = Vector3.SmoothDamp(currentVelocity, targetVelocity, ref velocitySmoothDamp, acceleration);

        if (characterController.isGrounded)
        {
            verticalVelocity = -1f;
            if (jumpAction.WasPressedThisFrame())
            {
                verticalVelocity = jumpForce;
            }
        }
        else
        {
            // duserken yer cekimini guclendirerek zipramayi daha "keskin" hissettir
            float multiplier = verticalVelocity < 0f ? fallGravityMultiplier : 1f;
            verticalVelocity += gravity * multiplier * Time.deltaTime;
        }

        characterController.Move((currentVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private void SwimMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        // ileri/geri bakış yonunde ilerler (asagi bakinca dalis, yukari bakinca yüzeye), saga/sola gövde ekseninde
        Vector3 look = lookPivot != null ? lookPivot.forward : transform.forward;
        Vector3 swimInput = look * input.y + transform.right * input.x;

        // zipla tusu yukari, yüzeye cikmak icin
        float ascend = jumpAction.IsPressed() ? 1f : 0f;

        Vector3 desired = Vector3.ClampMagnitude(swimInput + Vector3.up * ascend, 1f);
        float speed = moveSpeed * swimSpeedMultiplier * (sprintAction.IsPressed() ? sprintMultiplier : 1f);
        Vector3 targetVelocity = desired * speed;

        // aktif dikey/ileri girdi yokken karakteri hedef derinlige (floatDepth) getirip yuzeyde salinmasini sagla
        bool givingInput = ascend > 0f || swimInput.sqrMagnitude > 0.01f;
        if (!givingInput)
        {
            float feetY = transform.position.y + characterController.center.y - characterController.height * 0.5f;
            float submersion = waterLevel - feetY;
            float depthError = submersion - floatDepth; // pozitif = fazla derin, yukari itmeli
            targetVelocity.y += Mathf.Clamp(depthError, -1f, 1f) * buoyancy;
        }

        currentVelocity = Vector3.SmoothDamp(currentVelocity, targetVelocity, ref velocitySmoothDamp, swimAcceleration);
        characterController.Move(currentVelocity * Time.deltaTime);
    }
}
