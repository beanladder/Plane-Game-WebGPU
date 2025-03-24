using UnityEngine;

[CreateAssetMenu(fileName = "NewGun", menuName = "Weapons/Gun")]
public class GunData : ScriptableObject
{
    [Header("Basic Settings")]
    public string gunName;
    public GameObject bulletPrefab;
    public float damage;
    public float speed;
    public float range;
    public float fireRate;
    public int magazineSize;
    public float reloadTime;

    [Header("Accuracy Settings")]
    [Range(0.1f, 1f)] public float baseAccuracy = 0.8f;
    public float accuracyLossPerShot = 0.05f;
    public float maxAccuracyLoss = 0.3f;
    public float accuracyRecoveryRate = 3f;
    public float maxCrosshairDrift = 40f;
}