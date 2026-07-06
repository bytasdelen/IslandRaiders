using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Bizim kullandığımız gemi controlleri, gemi hareket ettirmek ve dümene basan oyuncunun inputunu almak için kullanılır.
public class ShipController : NetworkBehaviour, IShipDeck
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private SteeringWheel steeringWheel;

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float maxReverseSpeed = 4f;
    [SerializeField] private float acceleration = 3f;
    [SerializeField] private float brakePower = 5f;
    [SerializeField] private float drag = 1.5f;
    [SerializeField] private float turnRate = 30f;
    [SerializeField] private float steerSmooth = 2f;

    private InputAction moveAction;
    private float currentSpeed;
    private float currentSteer;

    private readonly NetworkVariable<float> networkSteer =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public float Rudder => networkSteer.Value;

    public override void OnNetworkSpawn()
    {
        moveAction = inputActions.FindActionMap("Player").FindAction("Move");
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        float throttle = 0f;
        float steer = 0f;
        if (steeringWheel.HasDriver)
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            throttle = input.y;
            steer = input.x;
        }

        // hız: W hızlandırır, S önce frenler sonra yavaşça geri, bırakınca su sürtünmesiyle durur
        if (throttle > 0.1f)
        {
            currentSpeed += acceleration * Time.deltaTime;
        }
        else if (throttle < -0.1f)
        {
            currentSpeed -= brakePower * Time.deltaTime;
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, drag * Time.deltaTime);
        }
        currentSpeed = Mathf.Clamp(currentSpeed, -maxReverseSpeed, maxSpeed);

        // dümen yavaşça dönsün
        currentSteer = Mathf.MoveTowards(currentSteer, steer, steerSmooth * Time.deltaTime);
        networkSteer.Value = currentSteer;

        // ileri hareket düz düzlemde (dalga eğimi yönü etkilemesin)
        Vector3 forward = -transform.forward;
        forward.y = 0f;
        forward.Normalize();
        transform.position += forward * (currentSpeed * Time.deltaTime);

        // dönüş yalnızca hareket varken etkili (hıza oranılı)
        float speedFactor = currentSpeed / maxSpeed;
        transform.Rotate(Vector3.up, currentSteer * turnRate * speedFactor * Time.deltaTime, Space.World);
    }
}
