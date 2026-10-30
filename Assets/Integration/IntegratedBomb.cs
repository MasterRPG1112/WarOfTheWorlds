using UnityEngine;
using Taeyang;
namespace Integration
{
    public class IntegratedBomb : MonoBehaviour
    {
        void Start() { Destroy(gameObject,4); }
        void OnTriggerEnter2D(Collider2D other)
        {
            var bullet=other.GetComponent<EnemyProjectile>();
            if(bullet!=null) Destroy(bullet.gameObject);
            var enemy=other.GetComponent<EnemyShip>();
            if(enemy!=null) enemy.TakeDamage(60,Vector2.up);
        }
    }
}
