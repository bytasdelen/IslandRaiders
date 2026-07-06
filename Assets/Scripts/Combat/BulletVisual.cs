using UnityEngine;

// sadece gorsel amacli, network'e dahil degil - hasar zaten sunucuda aninda hesaplan�yor
public class BulletVisual : MonoBehaviour
{
    [SerializeField] private float speed = 80f;

    private TrailRenderer trail;
    private Vector3 targetPosition;
    private bool launched;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
    }

    private void OnEnable()
    {
        launched = false;
        ResetTrail(false);
    }

    private void OnDisable()
    {
        launched = false;
        ResetTrail(false);
    }

    public void Launch(Vector3 start, Vector3 end)
    {
        Vector3 travel = end - start;
        if (travel.sqrMagnitude > 0.0001f)
        {
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(travel.normalized));
        }
        else
        {
            transform.position = start;
        }

        targetPosition = end;
        launched = true;
        ResetTrail(true);
    }

    private void Update()
    {
        if (!launched)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
        {
            PoolManager.Instance.ReleaseBullet(this);
        }
    }

    private void ResetTrail(bool emitting)
    {
        if (trail == null)
        {
            return;
        }

        trail.emitting = false;
        trail.Clear();
        trail.emitting = emitting;
    }
}
