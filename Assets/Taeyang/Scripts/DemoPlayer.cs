using UnityEngine;

namespace Taeyang
{
    // Standalone TEST scene only. Does not replace the existing Sanghyuk Player.cs.
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class DemoPlayer : MonoBehaviour
    {
        public float movementSpeed = 5.5f;
        public int health = 5;
        private Rigidbody2D body;
        private Vector2 input;
        private float invulnerable;
        private SpriteRenderer sprite;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.bodyType = RigidbodyType2D.Kinematic;
            sprite = GetComponent<SpriteRenderer>();
        }
        private void Update()
        {
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            if (Input.GetKeyDown(KeyCode.Space))
                foreach (EnemyProjectile bullet in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                    Destroy(bullet.gameObject);
            invulnerable -= Time.deltaTime;
            if (sprite != null) sprite.color = invulnerable > 0 ? new Color(0.5f, 0.7f, 1f) : Color.white;
        }
        private void FixedUpdate()
        {
            Vector2 destination = body.position + input * movementSpeed * Time.fixedDeltaTime;
            destination.x = Mathf.Clamp(destination.x, -7.6f, 7.6f);
            destination.y = Mathf.Clamp(destination.y, -4.3f, 4.3f);
            body.MovePosition(destination);
        }
        public void TakeDamage(int damage)
        {
            if (invulnerable > 0 || health <= 0) return;
            health = Mathf.Max(0, health - damage);
            invulnerable = 0.8f;
            if (health <= 0) { health = 5; transform.position = new Vector3(0, -3.5f, 0); }
        }
    }
}
