using UnityEngine;

public class PlaneHealth : MonoBehaviour
{
    [SerializeField] private PlaneStats planeStats;
    public float currentHealth;
    private bool isRepairing = false;
    private float repairTimer = 0f;

    private void Start()
    {
        if (planeStats == null)
        {
            Debug.LogError("PlaneStats not assigned to PlaneHealth!");
            return;
        }

        currentHealth = planeStats.maxHealth;
    }

    private void Update()
    {
        if (isRepairing)
        {
            HandleRepair();
            return;
        }

        if (Input.GetKeyDown(KeyCode.R) && currentHealth < planeStats.maxHealth)
        {
            StartRepair();
        }
    }

    public void TakeDamage(float damageAmount)
    {
        currentHealth = Mathf.Max(0, currentHealth - damageAmount);
        if (currentHealth <= 0)
        {
            Debug.Log($"{planeStats.planeName} has been destroyed!");
        }
    }

    private void StartRepair()
    {
        isRepairing = true;
        repairTimer = 0f;
        Debug.Log($"Starting repair for {planeStats.planeName}");
    }

    private void HandleRepair()
    {
        repairTimer += Time.deltaTime;
        if (repairTimer >= planeStats.repairTime)
        {
            currentHealth = Mathf.Min(planeStats.maxHealth, currentHealth + planeStats.repairAmount);
            isRepairing = false;
            Debug.Log($"Repair complete. Health: {currentHealth}/{planeStats.maxHealth}");
        }
    }
}