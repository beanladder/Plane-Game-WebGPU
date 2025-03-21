using UnityEngine;

[CreateAssetMenu(fileName = "NewBullet", menuName = "Weapons/Bullet")]
public class BulletData : ScriptableObject
{
    public float damage = 10f;
    public float speed = 100f;
    public float range = 500f; // Distance before disappearing
}
