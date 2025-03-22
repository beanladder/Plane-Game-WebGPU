using UnityEngine;
using System.Collections;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapons")]
    public GameObject machineGun2x; // Only Machine Gun (2x) available for now

    [Header("Fire Points")]
    public Transform[] firePoints2x;

    [Header("Bullet Data")]
    public BulletData machineGun2xBullet;

    private GameObject activeWeapon;
    private Transform[] activeFirePoints;
    private BulletData activeBulletData;
    private bool canShoot = true;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        SelectWeapon(2); // Only Machine Gun (2x) exists
    }

    public void SelectWeapon(int weaponType)
    {
        if (machineGun2x != null) machineGun2x.SetActive(false);

        if (weaponType == 2 && machineGun2x != null)
        {
            activeWeapon = machineGun2x;
            activeFirePoints = firePoints2x;
            activeBulletData = machineGun2xBullet;
        }
        else
        {
            Debug.LogError("❌ Selected weapon is not available!");
            return;
        }

        activeWeapon.SetActive(true);
        Debug.Log($"✅ Weapon Selected: {activeWeapon.name}");
    }

    void Update()
    {
        if (Input.GetButton("Fire1") && canShoot)
        {
            StartCoroutine(FireWeapon());
        }
    }

    IEnumerator FireWeapon()
    {
        if (activeWeapon == null || activeFirePoints.Length == 0 || activeBulletData == null)
        {
            Debug.LogError("❌ No Active Weapon or Fire Points!");
            yield break;
        }

        canShoot = false;

        foreach (var firePoint in activeFirePoints)
        {
            FireBullet(firePoint);
        }

        yield return new WaitForSeconds(activeBulletData.fireRate);
        canShoot = true;
    }

    void FireBullet(Transform firePoint)
    {
        if (mainCam == null)
        {
            Debug.LogError("❌ Main Camera is NULL!");
            return;
        }

        // Get Crosshair's world position from CrosshairController
        CrosshairController crosshairController = FindFirstObjectByType<CrosshairController>();
        if (crosshairController == null)
        {
            Debug.LogError("❌ CrosshairController not found!");
            return;
        }

        Vector3 targetPoint = crosshairController.GetCrosshairWorldPosition();

        // Calculate direction from the fire point to the crosshair
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

        // Apply bullet spread (accuracy)
        float spreadAmount = activeBulletData.accuracy;
        shootDirection.x += Random.Range(-spreadAmount, spreadAmount);
        shootDirection.y += Random.Range(-spreadAmount, spreadAmount);

        // Spawn bullet
        GameObject bullet = Instantiate(activeBulletData.bulletPrefab, firePoint.position, Quaternion.LookRotation(shootDirection));

        if (bullet == null)
        {
            Debug.LogError("❌ Bullet Instantiation Failed!");
            return;
        }

        Debug.Log($"✅ Bullet Fired Toward Crosshair! Fire Point: {firePoint.position} → Crosshair: {targetPoint}");

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("❌ Bullet Prefab Missing Rigidbody!");
            return;
        }

        rb.linearVelocity = shootDirection * activeBulletData.speed;
        Debug.Log($"🚀 Bullet Velocity Set: {rb.linearVelocity}");
    }


}
