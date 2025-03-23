using UnityEngine;

[CreateAssetMenu(fileName = "NewBullet", menuName = "Weapons/Bullet")]
public class BulletData : ScriptableObject
{
    public string bulletName;
    public GameObject bulletPrefab;
    public float damage;
    public float speed;
    public float range;
    public float accuracy; // Spread amount
    public float fireRate; // Fire delay
    public int magazineSize; // Max rounds per mag
    public float reloadTime; // Reload duration
}
