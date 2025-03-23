using UnityEngine;
using System.Collections;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapons")]
    public GameObject machineGun1x;
    public GameObject machineGun2x;
    public GameObject machineGun4x;
    public GameObject cannon1x;

    [Header("Fire Points")]
    public Transform firePoint1x;
    public Transform[] firePoints2x;
    public Transform[] firePoints4x;
    public Transform firePointCannon;

    [Header("Bullet Data")]
    public BulletData machineGun1xBullet;
    public BulletData machineGun2xBullet;
    public BulletData machineGun4xBullet;
    public BulletData cannonBullet;

    private GameObject activeWeapon;
    private Transform[] activeFirePoints;
    private BulletData activeBulletData;
    private bool canShoot = true;
    private bool isReloading = false;
    private int currentAmmo;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        DisableAllWeapons(); // Start with all weapons disabled
    }

    void Update()
    {
        // Weapon switching
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectWeapon(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectWeapon(4);

        // Fire bullets when left click is pressed
        if (Input.GetButton("Fire1") && canShoot && !isReloading)
        {
            if (currentAmmo > 0)
            {
                StartCoroutine(FireWeapon());
            }
            else
            {
                StartCoroutine(Reload());
            }
        }
    }

    private void SelectWeapon(int weaponType)
    {
        DisableAllWeapons();
        isReloading = false;

        switch (weaponType)
        {
            case 1:
                if (machineGun1x != null)
                {
                    activeWeapon = machineGun1x;
                    activeFirePoints = new Transform[] { firePoint1x };
                    activeBulletData = machineGun1xBullet;
                    machineGun1x.SetActive(true);
                    Debug.Log("✅ Machine Gun (1x) Activated");
                }
                break;
            case 2:
                if (machineGun2x != null)
                {
                    activeWeapon = machineGun2x;
                    activeFirePoints = firePoints2x;
                    activeBulletData = machineGun2xBullet;
                    machineGun2x.SetActive(true);
                    Debug.Log("✅ Machine Gun (2x) Activated");
                }
                break;
            case 3:
                if (machineGun4x != null)
                {
                    activeWeapon = machineGun4x;
                    activeFirePoints = firePoints4x;
                    activeBulletData = machineGun4xBullet;
                    machineGun4x.SetActive(true);
                    Debug.Log("✅ Machine Gun (4x) Activated");
                }
                break;
            case 4:
                if (cannon1x != null)
                {
                    activeWeapon = cannon1x;
                    activeFirePoints = new Transform[] { firePointCannon };
                    activeBulletData = cannonBullet;
                    cannon1x.SetActive(true);
                    Debug.Log("✅ Cannon (1x) Activated");
                }
                break;
            default:
                Debug.LogError("❌ Invalid Weapon Type!");
                return;
        }

        if (activeWeapon == null || activeFirePoints == null || activeBulletData == null)
        {
            Debug.LogError("❌ Selected weapon is not available!");
            return;
        }

        currentAmmo = activeBulletData.magazineSize; // Refill ammo when switching
    }

    private IEnumerator FireWeapon()
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

        currentAmmo--; // Reduce ammo count

        yield return new WaitForSeconds(activeBulletData.fireRate);
        canShoot = true;
    }

    private void FireBullet(Transform firePoint)
    {
        CrosshairController crosshairController = FindFirstObjectByType<CrosshairController>();
        if (crosshairController == null)
        {
            Debug.LogError("❌ CrosshairController not found!");
            return;
        }

        Vector3 targetPoint = crosshairController.GetCrosshairWorldPosition();
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

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

        rb.linearVelocity = shootDirection * activeBulletData.speed;
        Debug.Log($"🚀 Bullet Velocity Set: {rb.linearVelocity}");

        Destroy(bullet, 2f);
    }

    private IEnumerator Reload()
    {
        if (isReloading) yield break;
        isReloading = true;

        Debug.Log($"🔄 Reloading... ({activeBulletData.reloadTime}s)");
        yield return new WaitForSeconds(activeBulletData.reloadTime);

        currentAmmo = activeBulletData.magazineSize;
        isReloading = false;
        Debug.Log("✅ Reload Complete!");
    }

    private void DisableAllWeapons()
    {
        if (machineGun1x != null) machineGun1x.SetActive(false);
        if (machineGun2x != null) machineGun2x.SetActive(false);
        if (machineGun4x != null) machineGun4x.SetActive(false);
        if (cannon1x != null) cannon1x.SetActive(false);
    }
}
