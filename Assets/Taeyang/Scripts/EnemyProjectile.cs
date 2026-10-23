using UnityEngine;

namespace Taeyang
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
    public class EnemyProjectile : MonoBehaviour
    {
        [Min(0.5f)] public float speed = 5f;
        [Min(0f)] public float rotationSpeed = 160f;
        [Min(0.2f)] public float lifetime = 8f;
        public int enemyDamage = 20;
        public Color impactColor = new Color(0.5f, 0.9f, 1f, 1f);

        private EnemyShip owner;
        private Transform target;
        private Vector2 direction = Vector2.down;
        private Rigidbody2D body;
        private float speedScale = 1f;
        private float remaining;
        private bool resolved;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            remaining = lifetime;
        }

        public void Initialize(EnemyShip source, Vector2 initialDirection, Transform homingTarget, float multiplier = 1f)
        {
            owner = source;
            direction = initialDirection.sqrMagnitude < 0.01f ? Vector2.down : initialDirection.normalized;
            target = homingTarget;
            speedScale = Mathf.Max(0.1f, multiplier);
            RotateToDirection();
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (target != null)
            {
                Vector2 desired = ((Vector2)target.position - body.position).normalized;
                float maxRadians = rotationSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
                float angle = Mathf.Atan2(direction.y, direction.x);
                float aimAngle = Mathf.Atan2(desired.y, desired.x);
                float newAngle = Mathf.MoveTowardsAngle(angle * Mathf.Rad2Deg, aimAngle * Mathf.Rad2Deg,
                    maxRadians * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                direction = new Vector2(Mathf.Cos(newAngle), Mathf.Sin(newAngle));
            }
            body.MovePosition(body.position + direction * (speed * speedScale * Time.fixedDeltaTime));
            RotateToDirection();
        }

        private void RotateToDirection()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved) return;
            EnemyShip enemy = other.GetComponent<EnemyShip>();
            if (enemy != null && enemy != owner)
            {
                resolved = true;
                enemy.TakeDamage(enemyDamage, direction);
                SimpleImpact.Spawn(transform.position, impactColor, 0.5f);
                Destroy(gameObject);
                return;
            }
            if (other.CompareTag("Player"))
            {
                resolved = true;
                DemoPlayer demo = other.GetComponent<DemoPlayer>();
                if (demo != null) demo.TakeDamage(1);
                // Existing team Player.cs independently handles objects tagged "Bullet".
                SimpleImpact.Spawn(transform.position, impactColor, 0.3f);
                Destroy(gameObject);
            }
        }
    }
}
