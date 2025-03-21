using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName = "Machine Gun";
    public BulletData bullet;
    public GameObject bulletPrefab;
    public float fireRate = 0.1f;
    public bool alternateFire = true;

    [Header("Weapon Fire Points")]
    public Transform[] firePoints; // Assign fire points directly in the Editor
}
