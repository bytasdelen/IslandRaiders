using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    // yuzerken hareket yonu bakis yonunden alinir, bu pivot kamera pitch'ini tutar
    [SerializeField] private Transform lookPivot;
    // gemideyken (alt kat dahil) yuzme kontrolu tamamen atlanir - gemi govdesi denizin
    // gercek yuzeyinden daha dusuk Y'de olabilir, sabit waterLevel kiyaslamasi orada yaniliyordu
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
    // yuzme hizi kara hizinin bu kati kadar; 0.5 alti tutulmasi "en az 2 kat yavas" demektir
    [SerializeField] private float swimSpeedMultiplier = 0.4f;
    [SerializeField] private float swimAcceleration = 0.3f;
    // sudan yukari dogru itme kuvveti, karakteri yuzeye tasir
    [SerializeField] private float buoyancy = 4f;
    // ayaktan olculen, karakterin sakin haldeyken oturdugu su derinligi (dusuk deger = daha yuksekte yuzer)
    [SerializeField] private float floatDepth = 1.1f;
    // yuzmeye baslama/bitme esikleri arasindaki fark mod titremesini onler
    [SerializeField] private float swimStartDepth = 1.3f;
    [SerializeField] private float swimStopDepth = 0.8f;

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Vector3 currentVelocity;
    private Vector3 velocitySmoothDamp;
    private float verticalVelocity;
    private bool isSwimming;

    // su alti efekti gibi gorsel bilesenler bu durumu okur
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
            SwimMovement();
        }
        else
        {
            GroundMovement();
        }
    }

    // ayaklarin ne kadar suya girdigine gore yuzme moduna gec/cik, esikler arasi bosluk histerezis saglar
    private void UpdateSwimState()
    {
        // gemi uzerindeysen (dumende, ust guverte veya alt kat) asla yuzme moduna girme
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
            // suya girerken birikmis dusme hizini sifirla ki dibe cakilmasin
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
        // ileri/geri bakis yonunde ilerler (asagi bakinca dalis, yukari bakinca yuzeye), saga/sola govde ekseninde
        Vector3 look = lookPivot != null ? lookPivot.forward : transform.forward;
        Vector3 swimInput = look * input.y + transform.right * input.x;

        // zipla tusu yukari, yuzeye cikma icin
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
