using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    public List<WeaponData> availableWeapons;
    public Camera mainCamera;
    private WeaponData currentWeapon;
    private Transform[] firePoints;
    private int fireIndex = 0;
    private bool canShoot = true;

    void Start()
    {
        if (availableWeapons.Count > 0)
            EquipWeapon(0);
    }

    void Update()
    {
        if (Input.GetButton("Fire1") && canShoot)
        {
            StartCoroutine(FireWeapon());
        }
    }

    public void EquipWeapon(int index)
    {
        if (index < 0 || index >= availableWeapons.Count)
            return;

        currentWeapon = availableWeapons[index];
        firePoints = currentWeapon.firePoints;
        Debug.Log("Equipped: " + currentWeapon.weaponName);
    }

    IEnumerator FireWeapon()
    {
        if (currentWeapon == null || firePoints == null || firePoints.Length == 0)
            yield break;

        canShoot = false;
        FireBullet();
        yield return new WaitForSeconds(currentWeapon.fireRate);
        canShoot = true;
    }

    void FireBullet()
    {
        if (currentWeapon == null || firePoints.Length == 0) return;

        Transform firePoint = firePoints[fireIndex];

        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Vector3 targetPoint = ray.GetPoint(1000);
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

        GameObject bullet = Instantiate(currentWeapon.bulletPrefab, firePoint.position, Quaternion.LookRotation(shootDirection));
        bullet.GetComponent<Rigidbody>().linearVelocity = shootDirection * currentWeapon.bullet.speed;
        //bullet.GetComponent<Bullet>().Initialize(currentWeapon.bullet);

        if (currentWeapon.alternateFire)
            fireIndex = (fireIndex + 1) % firePoints.Length;
    }
}
