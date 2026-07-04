public interface IDamageable
{
    void ApplyDamage(int weaponDamage, HitRegion region, ulong attackerClientId);
}
