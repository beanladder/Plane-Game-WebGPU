using UnityEngine;

public class Bullet : MonoBehaviour
{
    private float damage;
    private float range;
    private Vector3 startPosition;

    public void Initialize(BulletData bulletData)
    {
        damage = bulletData.damage;
        range = bulletData.range;
        startPosition = transform.position;
    }

    void Update()
    {
        // Destroy bullet if it exceeds range
        if (Vector3.Distance(startPosition, transform.position) > range)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Handle damage logic here
        Destroy(gameObject);
    }
}
