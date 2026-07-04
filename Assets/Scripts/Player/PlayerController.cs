using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2.2f;
    [SerializeField] private float acceleration = 0.12f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Vector3 currentVelocity;
    private Vector3 velocitySmoothDamp;
    private float verticalVelocity;

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
}
