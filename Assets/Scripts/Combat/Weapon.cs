using UnityEngine;

// bir silahin istatistiklerini tutar, hem eldeki hem yerdeki silahta bulunur
public class Weapon : MonoBehaviour
{
    [SerializeField] private string weaponName = "Pistol";
    [SerializeField] private int damage = 20;
    [SerializeField] private float range = 50f;
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private bool automatic;
    [SerializeField] private Transform muzzle;

    public string WeaponName => weaponName;
    public int Damage => damage;
    public float Range => range;
    public float FireRate => fireRate;
    public bool Automatic => automatic;
    public Transform Muzzle => muzzle;
}
