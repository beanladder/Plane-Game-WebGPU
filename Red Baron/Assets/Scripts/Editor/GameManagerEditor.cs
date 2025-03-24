#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    private int selectedPlaneIndex;
    private int selectedWeaponIndex;
    private string[] weaponNames;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GameManager gm = (GameManager)target;

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Plane Selection", EditorStyles.boldLabel);
            selectedPlaneIndex = EditorGUILayout.Popup("Plane", selectedPlaneIndex, GetPlaneNames(gm.planePrefabs));

            if (gm.planePrefabs[selectedPlaneIndex] != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Weapon Selection", EditorStyles.boldLabel);
                weaponNames = GetWeaponNames(gm.planePrefabs[selectedPlaneIndex]);
                selectedWeaponIndex = EditorGUILayout.Popup("Weapon", selectedWeaponIndex, weaponNames);

                if (GUILayout.Button("Spawn Plane with Selected Weapon"))
                {
                    gm.SpawnPlane(selectedPlaneIndex, weaponNames[selectedWeaponIndex]);
                }
            }
        }
    }

    private string[] GetPlaneNames(GameObject[] planes)
    {
        return planes.Select(p => p != null ? p.name : "Missing Prefab").ToArray();
    }

    private string[] GetWeaponNames(GameObject planePrefab)
    {
        if (planePrefab == null) return new string[0];

        List<string> names = new List<string>();
        foreach (Transform child in planePrefab.transform)
        {
            if (child.CompareTag("Weapon"))
            {
                names.Add(child.name);
            }
        }
        return names.ToArray();
    }
}
#endif