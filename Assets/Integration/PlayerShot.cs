using UnityEngine;
using Taeyang;
namespace Integration
{
    public class PlayerShot : MonoBehaviour
    {
        float age;
        bool resolved;
        public int damage = 12;
        void FixedUpdate()
        {
            GetComponent<Rigidbody2D>().MovePosition((Vector2)transform.position + Vector2.up * 12 * Time.fixedDeltaTime);
            age += Time.fixedDeltaTime;
            if (age > 2) Destroy(gameObject);
        }
        void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved) return;
            var enemy = other.GetComponent<EnemyShip>();
            if (enemy == null) return;
            resolved = true; enemy.TakeDamage(damage, Vector2.up); Destroy(gameObject);
        }
    }
}
