using UnityEngine;

// sadece gorsel amacli, network'e dahil degil - hasar zaten sunucuda aninda hesaplandi
public class BulletVisual : MonoBehaviour
{
    [SerializeField] private float speed = 80f;

    private Vector3 targetPosition;

    public void Launch(Vector3 start, Vector3 end)
    {
        transform.position = start;
        transform.rotation = Quaternion.LookRotation((end - start).normalized);
        targetPosition = end;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
        {
            Destroy(gameObject);
        }
    }
}
