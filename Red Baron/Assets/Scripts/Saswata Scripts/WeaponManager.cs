using UnityEngine;
using System.Collections;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapon")]
    public GameObject machineGun2x; // Only Machine Gun (2x) available

    [Header("Fire Points")]
    public Transform[] firePoints2x; // Fire points for Machine Gun (2x)

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
        SelectWeapon(2); // Default to Machine Gun (2x)
    }

    void Update()
    {
        // Fire bullets when left click is pressed
        if (Input.GetButton("Fire1") && canShoot)
        {
            StartCoroutine(FireWeapon());
        }
    }

    public void SelectWeapon(int weaponType)
    {
        if (machineGun2x != null)
            machineGun2x.SetActive(false);

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
        // Get accurate crosshair world position
        CrosshairController crosshairController = FindFirstObjectByType<CrosshairController>();
        if (crosshairController == null)
        {
            Debug.LogError("❌ CrosshairController not found!");
            return;
        }

        Vector3 targetPoint = crosshairController.GetCrosshairWorldPosition();
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

        // Instantiate bullet at fire point
        GameObject bullet = Instantiate(activeBulletData.bulletPrefab, firePoint.position, Quaternion.LookRotation(shootDirection));

        if (bullet == null)
        {
            Debug.LogError("❌ Bullet Instantiation Failed!");
            return;
        }

        Debug.Log($"✅ Bullet Fired from {firePoint.position} to {targetPoint}");

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("❌ Bullet Prefab Missing Rigidbody!");
            return;
        }

        // Apply velocity
        rb.linearVelocity = shootDirection * activeBulletData.speed;
        Debug.Log($"🚀 Bullet Velocity Set: {rb.linearVelocity}");

        // Destroy bullet after a set time (e.g., 5 seconds)
        Destroy(bullet, 2f);
    }



}
