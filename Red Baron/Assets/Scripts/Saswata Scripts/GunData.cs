using UnityEngine;

[CreateAssetMenu(fileName = "NewGun", menuName = "Weapons/Gun")]
public class GunData : ScriptableObject
{
    public string gunName;
    public GameObject bulletPrefab;
    public float damage;
    public float speed;
    public float range;
    public float accuracy; // Spread amount
    public float fireRate; // Fire delay
    public int magazineSize; // Max rounds per mag
    public float reloadTime; // Reload duration
}
