using UnityEngine;

public class Bullet : MonoBehaviour
{
    private float damage;
    private float speed;
    private float range;
    private Vector3 startPosition;

    public void Initialize(GunData gunData)
    {
        damage = gunData.damage;
        speed = gunData.speed;
        range = gunData.range;
        startPosition = transform.position;
    }

    void Update()
    {
      
            transform.position += transform.forward * speed * Time.deltaTime;
            //Debug.Log($"🚀 Bullet Moving: {gameObject.name} | Position: {transform.position}");
        

    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"💥 Bullet Hit: {other.gameObject.name}");
        Destroy(gameObject);
    }
}
