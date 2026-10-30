using System;
using UnityEngine;

namespace Taeyang
{
    // Enemy ships deliberately share a single component so team members can
    // rebalance all five archetypes from their prefabs without copying code.
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
    public class EnemyShip : MonoBehaviour
    {
        public enum EnemyKind { Scout, Fighter, Heavy, Interceptor, Bomber }
        public EnemyKind kind;
        [Min(1)] public int maxHealth = 20;
        [Min(0.1f)] public float moveSpeed = 2f;
        [Min(0.1f)] public float fireInterval = 2.4f;
        [Range(0f, 2f)] public float driftWidth = 0.45f;
        public int scoreValue = 100;
        public EnemyProjectile homingProjectile;
        public EnemyProjectile straightProjectile;
        public EnemyProjectile bombProjectile;
        public Transform firePoint;
        // Opt-in on Stage 1 variants only; original EnemyTest prefabs retain their behavior.
        public bool stageOnePatterns;
        [Range(1,3)] public int stageDifficulty = 1;

        public static event Action<EnemyShip, int> DestroyedByPlayer;
        public int CurrentHealth { get; private set; }
        public bool IsDefeated { get; private set; }

        private Rigidbody2D body;
        private SpriteRenderer visuals;
        private Transform player;
        private float fireTimer;
        private float age;
        private float spawnX;
        private float settledY;
        private float frequency;
        private float flashTimer;
        private Color baseColor;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            visuals = GetComponent<SpriteRenderer>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            CurrentHealth = maxHealth;
            spawnX = transform.position.x;
            settledY = UnityEngine.Random.Range(1.2f, 3.3f);
            frequency = UnityEngine.Random.Range(0.85f, 1.75f);
            fireTimer = UnityEngine.Random.Range(0.9f, 2.5f);
            baseColor = visuals.color;
        }

        private void Update()
        {
            if (Integration.IntegratedGame.Active != null && Integration.IntegratedGame.Active.EnemiesFrozen) return;
            age += Time.deltaTime;
            if (player == null)
            {
                GameObject candidate = GameObject.FindGameObjectWithTag("Player");
                if (candidate != null) player = candidate.transform;
            }
            if (visuals != null)
            {
                flashTimer -= Time.deltaTime;
                visuals.color = flashTimer > 0f ? Color.white : baseColor;
            }

            if (age < 1f || player == null) return;
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                FirePattern();
                fireTimer = fireInterval * UnityEngine.Random.Range(0.85f, 1.15f);
            }
            // Enemies retreat after a while so an unfinished demo never fills up forever.
            if (age > 26f) Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (Integration.IntegratedGame.Active != null && Integration.IntegratedGame.Active.EnemiesFrozen) return;
            float y = body.position.y;
            float newY = y > settledY ? Mathf.MoveTowards(y, settledY, moveSpeed * Time.fixedDeltaTime) : y;
            float wave = Mathf.Sin(age * frequency) * driftWidth;
            float x = spawnX + wave;
            if (kind == EnemyKind.Interceptor)
                x += Mathf.Sin(age * 3.5f) * 0.45f;
            if (kind == EnemyKind.Bomber && player != null)
                x = Mathf.MoveTowards(body.position.x, player.position.x, moveSpeed * 0.25f * Time.fixedDeltaTime);
            body.MovePosition(new Vector2(x, newY));
        }

        private void FirePattern()
        {
            if (player == null) return;
            Vector2 origin = firePoint != null ? firePoint.position : transform.position + Vector3.down * 0.35f;
            Vector2 aim = ((Vector2)player.position - origin).normalized;
            if (aim.sqrMagnitude < 0.01f) aim = Vector2.down;
            if (stageOnePatterns)
            {
                if (kind == EnemyKind.Scout)
                { Shoot(homingProjectile, origin, aim, true); }
                else if (kind == EnemyKind.Bomber)
                {
                    if (bombProjectile != null)
                    {
                        Vector2 snapshot = player.position;
                        foreach (float bend in new[] { -2.4f, 2.4f })
                            Instantiate(bombProjectile, origin, Quaternion.identity)
                                .InitializeCurve(this, snapshot, bend, 1.6f);
                    }
                }
                else if (kind == EnemyKind.Interceptor)
                {
                    Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                    float speed = straightProjectile != null ? straightProjectile.speed : 7f;
                    Vector2 predicted = StageOneMath.Predict(origin, player.position,
                        rb != null ? rb.linearVelocity : Vector2.zero, speed, 1.2f);
                    Shoot(straightProjectile, origin, predicted - origin, false);
                }
                else
                {
                    if(stageDifficulty >= 2 && kind == EnemyKind.Heavy) Shoot(homingProjectile, origin, aim, true);
                    int spread = kind == EnemyKind.Heavy ? 2 : kind == EnemyKind.Fighter ? 1 : 0;
                    for (int i = -spread; i <= spread; i++)
                        Shoot(straightProjectile, origin, Rotate(aim, i * 16f), false);
                }
                return;
            }

            switch (kind)
            {
                case EnemyKind.Scout:
                    Shoot(homingProjectile, origin, aim, true);
                    break;
                case EnemyKind.Fighter:
                    for (int i = -1; i <= 1; i++)
                        Shoot(homingProjectile, origin, Rotate(aim, i * 13f), true);
                    break;
                case EnemyKind.Heavy:
                    for (int i = -2; i <= 2; i++)
                        Shoot(straightProjectile, origin, Rotate(aim, i * 17f), false);
                    Shoot(homingProjectile, origin, aim, true);
                    break;
                case EnemyKind.Interceptor:
                    Shoot(straightProjectile, origin, aim, false, 1.9f);
                    break;
                case EnemyKind.Bomber:
                    Rigidbody2D targetBody = player.GetComponent<Rigidbody2D>();
                    Vector2 velocity = targetBody != null ? targetBody.linearVelocity : Vector2.zero;
                    float travelTime = Vector2.Distance(origin, player.position) / 4f;
                    Vector2 predicted = (Vector2)player.position + velocity * Mathf.Clamp(travelTime, 0f, 1.4f);
                    Vector2 predictiveAim = (predicted - origin).normalized;
                    Shoot(bombProjectile, origin, predictiveAim, false);
                    break;
            }
        }

        private void Shoot(EnemyProjectile prefab, Vector2 origin, Vector2 direction, bool homing, float speedMultiplier = 1f)
        {
            if (prefab == null) return;
            EnemyProjectile bullet = Instantiate(prefab, origin, Quaternion.identity);
            bullet.Initialize(this, direction, homing ? player : null, speedMultiplier);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            return Quaternion.Euler(0, 0, degrees) * v;
        }

        public void TakeDamage(int damage, Vector2 fromDirection)
        {
            if (IsDefeated) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, damage));
            flashTimer = 0.12f;
            if (CurrentHealth > 0) return;
            IsDefeated = true;
            DestroyedByPlayer?.Invoke(this, scoreValue);
            SimpleImpact.Spawn(transform.position, new Color(1f, 0.55f, 0.28f), 1.25f);
            Destroy(gameObject);
        }

        public void ResetHealthForTesting() => CurrentHealth = maxHealth;
    }
}
