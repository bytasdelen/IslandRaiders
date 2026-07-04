using UnityEngine;

// head/govde gibi vurulabilir bolgeleri temsil eder, hasari parent'taki cana yonlendirir
public class Hitbox : MonoBehaviour
{
    [SerializeField] private HitRegion region;

    private IDamageable damageable;

    private void Awake()
    {
        damageable = GetComponentInParent<IDamageable>();
    }

    public void TakeHit(int weaponDamage, ulong attackerClientId)
    {
        damageable?.ApplyDamage(weaponDamage, region, attackerClientId);
    }
}
