using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    // Weapon System
    private Transform[] firePoints;
    private GunData activeGunData;
    private bool canShoot = true;
    private bool isReloading = false;
    public int currentAmmo;
    private CrosshairController crosshairController;

    // Accuracy System
    private float currentAccuracy;
    private float accuracyRecoveryTimer;
    private Queue<GameObject> bulletPool = new Queue<GameObject>();

    public void InitializeWeapon(string weaponName, List<Transform> firePointsList, GunData gunData)
    {
        firePoints = firePointsList.ToArray();
        activeGunData = gunData;
        currentAmmo = activeGunData.magazineSize;
        crosshairController = FindFirstObjectByType<CrosshairController>();
        currentAccuracy = activeGunData.baseAccuracy;

        InitializeBulletPool();
        UpdateCrosshairAccuracy();

        Debug.Log($"🔫 {weaponName} initialized | Accuracy: {currentAccuracy:P0}");
    }

    void Update()
    {
        HandleShootingInput();
        HandleReloadInput();
        RecoverAccuracy();
    }

    void HandleShootingInput()
    {
        if (Input.GetButton("Fire1") && canShoot && !isReloading)
        {
            if (currentAmmo > 0)
            {
                StartCoroutine(FireWeapon());
            }
            else
            {
                Debug.Log("⚠️ Out of ammo! Press R to reload");
            }
        }
    }

    void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < activeGunData.magazineSize)
        {
            StartCoroutine(Reload());
        }
    }

    IEnumerator FireWeapon()
    {
        canShoot = false;

        foreach (var firePoint in firePoints)
        {
            if (currentAmmo <= 0) break;

            ApplyRecoil();
            FireBullet(firePoint);
            currentAmmo--;
        }

        yield return new WaitForSeconds(activeGunData.fireRate);
        canShoot = true;
    }

    void ApplyRecoil()
    {
        currentAccuracy = Mathf.Clamp(
            currentAccuracy - activeGunData.accuracyLossPerShot,
            activeGunData.baseAccuracy - activeGunData.maxAccuracyLoss,
            1f
        );
        UpdateCrosshairAccuracy();
    }

    void RecoverAccuracy()
    {
        if (canShoot && !isReloading)
        {
            currentAccuracy = Mathf.Lerp(
                currentAccuracy,
                activeGunData.baseAccuracy,
                Time.deltaTime * activeGunData.accuracyRecoveryRate
            );
            UpdateCrosshairAccuracy();
        }
    }

    void UpdateCrosshairAccuracy()
    {
        if (crosshairController != null)
        {
            crosshairController.UpdateAccuracy(currentAccuracy);
        }
    }

    void FireBullet(Transform firePoint)
    {
        if (crosshairController == null) return;

        Vector3 targetPoint = crosshairController.GetCrosshairWorldPosition();
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

        GameObject bullet = GetPooledBullet();
        bullet.transform.SetPositionAndRotation(firePoint.position, Quaternion.LookRotation(shootDirection));
        bullet.SetActive(true);

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = shootDirection * activeGunData.speed;
        }

        StartCoroutine(ReturnToPool(bullet, activeGunData.range / activeGunData.speed));
    }

    IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log($"🔄 Reloading {activeGunData.gunName}...");
        yield return new WaitForSeconds(activeGunData.reloadTime);

        currentAmmo = activeGunData.magazineSize;
        isReloading = false;
        Debug.Log("✅ Reload complete");
    }

    void InitializeBulletPool()
    {
        bulletPool.Clear();
        for (int i = 0; i < activeGunData.magazineSize * 2; i++)
        {
            GameObject bullet = Instantiate(activeGunData.bulletPrefab);
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }
    }

    GameObject GetPooledBullet()
    {
        if (bulletPool.Count > 0) return bulletPool.Dequeue();

        // Emergency bullet if pool is empty
        GameObject newBullet = Instantiate(activeGunData.bulletPrefab);
        newBullet.SetActive(false);
        return newBullet;
    }

    IEnumerator ReturnToPool(GameObject bullet, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bullet.activeSelf)
        {
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }
    }
}