using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;

public class GameManager : MonoBehaviour
{
    [Header("Plane Prefabs")]
    public GameObject[] planePrefabs = new GameObject[5];

    [Header("Gun Data")]
    public GunData[] gunDataList = new GunData[4];

    [Header("References")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private CinemachineCamera mapCamera;

    private Dictionary<string, List<Transform>> weaponFirePoints = new Dictionary<string, List<Transform>>();
    private GameObject selectedPlane;
    private string selectedWeaponName;
    private GunData selectedGunData;

    

    public void SpawnPlane(int planeIndex, string weaponName)
    {
        if (planeIndex < 0 || planeIndex >= planePrefabs.Length || planePrefabs[planeIndex] == null)
        {
            Debug.LogError("Invalid plane index");
            return;
        }

        // Destroy existing plane
        if (selectedPlane != null) Destroy(selectedPlane);

        // Instantiate new plane
        selectedPlane = Instantiate(planePrefabs[planeIndex], spawnPoint.position, spawnPoint.rotation);
        Debug.Log($"Spawned {selectedPlane.name}");
        if (mapCamera != null)
            mapCamera.gameObject.SetActive(false);

        // Extract weapons and firepoints
        ExtractWeaponsAndFirePoints(selectedPlane);

        // Enable selected weapon
        SelectWeapon(weaponName);

        // Initialize WeaponManager
        WeaponManager weaponManager = selectedPlane.GetComponent<WeaponManager>();
        if (weaponManager != null)
        {
            weaponManager.InitializeWeapon(selectedWeaponName, weaponFirePoints[weaponName], selectedGunData);
        }

        // Disable map camera
        if (mapCamera != null)
            mapCamera.gameObject.SetActive(false);
    }

    private void ExtractWeaponsAndFirePoints(GameObject plane)
    {
        weaponFirePoints.Clear();

        // Get all weapons (direct children with Weapon tag)
        foreach (Transform weapon in plane.transform)
        {
            if (weapon.CompareTag("Weapon"))
            {
                List<Transform> firePoints = new List<Transform>();

                // Get all firepoints in grandchildren
                foreach (Transform wingGun in weapon)
                {
                    foreach (Transform child in wingGun)
                    {
                        if (child.CompareTag("FirePoint"))
                        {
                            firePoints.Add(child);
                            Debug.Log($"Found fire point: {child.name} in {weapon.name}");
                        }
                    }
                }

                if (firePoints.Count > 0)
                {
                    weaponFirePoints.Add(weapon.name, firePoints);
                    Debug.Log($"Registered weapon: {weapon.name} with {firePoints.Count} fire points");
                }

                // Initially disable all weapons
                weapon.gameObject.SetActive(false);
            }
        }
    }

    private void SelectWeapon(string weaponName)
    {
        if (weaponFirePoints.ContainsKey(weaponName))
        {
            // Enable selected weapon
            Transform weapon = selectedPlane.transform.Find(weaponName);
            if (weapon != null)
            {
                weapon.gameObject.SetActive(true);
                selectedWeaponName = weaponName;
                selectedGunData = gunDataList.FirstOrDefault(g => g.gunName == weaponName);
                Debug.Log($"Selected weapon: {weaponName}");
            }
        }
        else
        {
            Debug.LogError($"Weapon {weaponName} not found!");
        }
    }
}