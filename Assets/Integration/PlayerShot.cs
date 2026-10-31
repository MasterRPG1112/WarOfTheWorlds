using UnityEngine;
using Taeyang;
namespace Integration
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerShot : MonoBehaviour
    {
        float age;
        bool resolved;
        public int damage = 12;
        [Min(.1f)] public float speed = 12;
        [Min(.1f)] public float lifetime = 6;
        [Min(.1f)] public float searchRadius = 35;
        [Min(1)] public float turnDegreesPerSecond = 480;
        [Min(.01f)] public float targetSearchInterval = .15f;
        [Min(0)] public float launchArcOffset = 1.4f;
        [Min(.01f)] public float launchArcDuration = .45f;

        public EnemyShip CurrentTarget { get; private set; }
        public Vector2 Heading { get; private set; } = Vector2.up;
        public float Age => age;
        public bool Resolved => resolved;

        Rigidbody2D body;
        Collider2D hitbox;
        ContactFilter2D hitFilter;
        RaycastHit2D[] sweepHits = new RaycastHit2D[32];
        Vector2 launchHeading = Vector2.up;
        float nextSearch;
        float arcSign = 1;
        static int launchSequence;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            hitbox = GetComponent<Collider2D>();
            if (hitbox == null) hitbox = gameObject.AddComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false, useDepth = false };
            Heading = transform.up;
            launchHeading = Heading;
            UpdateRotation();
        }

        // Launch along the ship's last facing; steering is bounded, never a position snap.
        public void Initialize(Vector2 launchDirection)
        {
            Heading = launchDirection.sqrMagnitude > .0001f ? launchDirection.normalized : Vector2.up;
            launchHeading = Heading;
            age = 0;
            resolved = false;
            CurrentTarget = null;
            nextSearch = 0;
            arcSign = (++launchSequence & 1) == 0 ? 1 : -1;
            AcquireNearestTarget();
            UpdateRotation();
        }

        void FixedUpdate()
        {
            if (resolved) return;
            age += Time.fixedDeltaTime;
            if (age >= lifetime) { resolved = true; Destroy(gameObject); return; }

            if (!IsValidTarget(CurrentTarget))
            {
                CurrentTarget = null;
                // Lost/no lock uses bounded periodic retries while the missile coasts.
                if (Time.time >= nextSearch) AcquireNearestTarget();
            }
            if (CurrentTarget != null)
            {
                Vector2 targetPosition = CurrentTarget.transform.position;
                // A temporary side-offset bends even a nearly straight-ahead launch, then fades
                // completely so the missile converges on the real target rather than orbiting it.
                if (age < launchArcDuration)
                {
                    float bend = Mathf.Sin(Mathf.PI * age / launchArcDuration) * launchArcOffset * arcSign;
                    targetPosition += new Vector2(-launchHeading.y, launchHeading.x) * bend;
                }
                Vector2 desired = targetPosition - body.position;
                if (desired.sqrMagnitude > .0001f)
                {
                    float currentAngle = Mathf.Atan2(Heading.y, Heading.x) * Mathf.Rad2Deg;
                    float targetAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                    float angle = Mathf.MoveTowardsAngle(currentAngle, targetAngle,
                        turnDegreesPerSecond * Time.fixedDeltaTime) * Mathf.Deg2Rad;
                    Heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }

            float travelSpeed = speed;
            if (CurrentTarget != null)
            {
                // Tighten the turning radius near the target; otherwise a fast missile can
                // circle a close target forever despite continuously steering toward it.
                float targetDistance = Vector2.Distance(body.position, CurrentTarget.transform.position);
                travelSpeed = Mathf.Min(speed, Mathf.Max(2f,
                    targetDistance * turnDegreesPerSecond * Mathf.Deg2Rad * .65f));
            }
            float distance = travelSpeed * Time.fixedDeltaTime;
            // Sweep the actual prefab shape over the full step. Trigger callbacks alone can miss
            // a thin/fast target between fixed frames. Ignore non-enemy colliders in the sweep.
            int count = hitbox.Cast(Heading, hitFilter, sweepHits, distance);
            while (count == sweepHits.Length && sweepHits.Length < 1024)
            {
                System.Array.Resize(ref sweepHits, sweepHits.Length * 2);
                count = hitbox.Cast(Heading, hitFilter, sweepHits, distance);
            }
            EnemyShip first = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                EnemyShip enemy = sweepHits[i].collider != null
                    ? sweepHits[i].collider.GetComponentInParent<EnemyShip>() : null;
                if (!IsDamageable(enemy) || sweepHits[i].distance >= nearest) continue;
                nearest = sweepHits[i].distance; first = enemy;
            }
            if (first != null) { ResolveHit(first); return; }
            body.MovePosition(body.position + Heading * distance);
            UpdateRotation();
        }

        bool IsValidTarget(EnemyShip enemy)
        {
            return IsDamageable(enemy) && enemy.isActiveAndEnabled &&
                ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude <= searchRadius * searchRadius;
        }

        static bool IsDamageable(EnemyShip enemy)
        {
            return enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDefeated && enemy.CurrentHealth > 0;
        }

        void AcquireNearestTarget()
        {
            nextSearch = Time.time + targetSearchInterval;
            float nearest = searchRadius * searchRadius;
            CurrentTarget = null;
            foreach (var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))
            {
                if (!IsValidTarget(enemy)) continue;
                float distance = ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; CurrentTarget = enemy;
            }
        }

        void UpdateRotation()
        {
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Heading.y, Heading.x) * Mathf.Rad2Deg - 90);
        }

        void ResolveHit(EnemyShip enemy)
        {
            if (resolved || !IsDamageable(enemy)) return;
            resolved = true;
            enemy.TakeDamage(damage, Heading);
            Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved) return;
            ResolveHit(other.GetComponentInParent<EnemyShip>());
        }
    }
}
