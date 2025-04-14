using UnityEngine;

[CreateAssetMenu(fileName = "New Plane Stats", menuName = "Aircraft/Plane Stats")]
public class PlaneStats : ScriptableObject
{
    [Header("Plane Identity")]
    public string planeName;
    public string planeDescription;

    [Header("Health Settings")]
    public float maxHealth = 200f;
    public float repairAmount = 50f;
    public float repairTime = 10f;

    [Header("Movement Settings")]
    public bool invertRollAxis = true;
    public bool invertPitchAxis = true;
    public float airNormalSpeed = 30f;
    public float airBoostSpeed = 80f;
    public float accelerationRate = 1f;
    public float decelerationRate = 0.5f;
    public float mouseSensitivity = 2f;
    public float maxRollAngle = 360f;
    public float rollSpeed = 2f;
    public float pitchSpeed = 2f;
    public float yawSpeed = 1f;

    [Header("Flight Physics")]
    [Tooltip("How much pitch angle affects forward speed")]
    public float pitchSpeedInfluence = 0.3f;
    [Tooltip("Speed bonus when diving (nose down)")]
    public float diveSpeedBoost = 0.4f;
    [Tooltip("Speed penalty when climbing (nose up)")]
    public float climbSpeedPenalty = 0.5f;
    [Tooltip("Strength of gravity's influence")]
    public float gravitationalForce = 9.8f;

    [Header("Inertia Settings")]
    public float rotationalDamping = 0.97f;
    public float rollDamping = 0.98f;

    [Header("Sensitivity Schemes")]
    [Range(0, 2)]
    public float pitchSensitivityMultiplier = 1f;
    [Range(0, 2)]
    public float rollSensitivityMultiplier = 1f;
    public bool useExponentialPitchResponse = false;
    public bool useProgressiveRollResponse = false;
    public float exponentialPitchFactor = 1.5f;
    public float progressiveRollThreshold = 0.3f;
    public float progressiveRollMultiplier = 1.5f;

    [Header("Input Smoothing")]
    public float inputSmoothTime = 0.1f;
    public float keyboardSmoothTime = 0.15f;

    [Header("Camera Settings")]
    public bool lockCameraToHorizon = true;
    public float defaultFov = 50f;
    public float maxFov = 60f;
    public float fovSmoothSpeed = 2f;
    public float cameraBlendTime = 1.2f;

    [Header("Audio & Visual Effects")]
    public AudioClip windRushSound;
    public AudioClip engineSputterSound;
}