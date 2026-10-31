using System.Collections.Generic;
using UnityEngine;

namespace Taeyang
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
    public class EnemyProjectile : MonoBehaviour
    {
        [Min(.5f)] public float speed = 5f;
        [Min(0)] public float rotationSpeed = 160f;
        [Min(.2f)] public float lifetime = 8f;
        public int enemyDamage = 20;
        public Color impactColor = new Color(.5f, .9f, 1f, 1f);
        [System.NonSerialized] public float externalSpeedFactor = 1;
        public bool Reflected { get; private set; }
        public EnemyShip Owner => owner;
        public Vector2 Direction => direction;

        EnemyShip owner;
        Transform target;
        Vector2 direction = Vector2.down;
        Rigidbody2D body;
        Collider2D hitbox;
        float speedScale = 1, remaining;
        bool resolved, curved;
        Vector2 curveStart, curveControl, curveEnd;
        float curveAge, curveDuration;
        readonly Dictionary<Object, float> slows = new Dictionary<Object, float>();
        readonly RaycastHit2D[] castHits = new RaycastHit2D[64];

        public static Vector2 Bezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            t = Mathf.Clamp01(t);
            return (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
        }

        public void InitializeCurve(EnemyShip source, Vector2 end, float bend, float duration)
        {
            Initialize(source, end - (Vector2)transform.position, null);
            curved = true;
            curveStart = transform.position;
            curveEnd = end;
            Vector2 delta = end - curveStart;
            curveControl = (curveStart + end) * .5f + new Vector2(-delta.y, delta.x).normalized * bend;
            curveDuration = Mathf.Max(.1f, duration);
            remaining = Mathf.Max(lifetime, curveDuration + .1f);
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<Collider2D>();
            hitbox.isTrigger = true;
            body.gravityScale = 0;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            remaining = lifetime;
        }

        public void Initialize(EnemyShip source, Vector2 initialDirection, Transform homingTarget, float multiplier = 1)
        {
            owner = source;
            direction = initialDirection.sqrMagnitude < .01f ? Vector2.down : initialDirection.normalized;
            target = homingTarget;
            speedScale = Mathf.Max(.1f, multiplier);
            curved = resolved = Reflected = false;
            curveAge = 0;
            remaining = lifetime;
            RotateToDirection();
        }

        public void Reflect(Vector2 aim)
        {
            if (resolved || Reflected) return;
            Reflected = true;
            curved = false;
            target = null;
            direction = aim.sqrMagnitude > .001f ? aim.normalized : -direction;
            remaining = Mathf.Max(remaining, 2);
            RotateToDirection();
        }

        public void SetSlow(Object source, float factor)
        {
            if (!ReferenceEquals(source, null)) slows[source] = Mathf.Clamp01(factor);
        }
        public void RemoveSlow(Object source)
        {
            if (!ReferenceEquals(source, null)) slows.Remove(source);
        }
        public float EffectiveSpeedFactor
        {
            get
            {
                float factor = Mathf.Clamp01(externalSpeedFactor);
                foreach (var slow in slows) if (slow.Key != null) factor = Mathf.Min(factor, slow.Value);
                return factor;
            }
        }

        bool Frozen => Integration.IntegratedGame.Active != null && Integration.IntegratedGame.Active.EnemiesFrozen;
        void Update()
        {
            if (resolved || Frozen) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) Absorb();
        }

        void FixedUpdate()
        {
            if (resolved || Frozen) { body.linearVelocity = Vector2.zero; return; }
            Vector2 next;
            bool endOfCurve = false;
            if (curved)
            {
                curveAge += Time.fixedDeltaTime * EffectiveSpeedFactor;
                float t = Mathf.Clamp01(curveAge / curveDuration);
                next = Bezier(curveStart, curveControl, curveEnd, t);
                Vector2 delta = next - body.position;
                if (delta.sqrMagnitude > .000001f) direction = delta.normalized;
                endOfCurve = t >= 1;
            }
            else
            {
                if (target != null)
                {
                    Vector2 desired = (Vector2)target.position - body.position;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    float aim = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                    float turn = Mathf.MoveTowardsAngle(angle, aim, rotationSpeed * Time.fixedDeltaTime) * Mathf.Deg2Rad;
                    direction = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn));
                }
                next = body.position + direction * (speed * speedScale * EffectiveSpeedFactor * Time.fixedDeltaTime);
            }
            RotateToDirection();
            SweepTo(next);
            if (!resolved) body.MovePosition(next);
            if (endOfCurve && !resolved) Absorb();
        }

        void SweepTo(Vector2 destination)
        {
            Vector2 step = destination - body.position;
            if (step.sqrMagnitude < .000001f) return;
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
            int count = hitbox.Cast(step.normalized, filter, castHits, step.magnitude);
            Collider2D closest = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (castHits[i].distance >= nearest || !CanHit(castHits[i].collider)) continue;
                nearest = castHits[i].distance;
                closest = castHits[i].collider;
            }
            if (closest != null) Resolve(closest);
        }

        bool CanHit(Collider2D other)
        {
            if (other == null || other.transform.IsChildOf(transform) ||
                other.GetComponentInParent<EnemyProjectile>() != null ||
                other.GetComponentInParent<Integration.IntegratedBossTarget>() != null) return false;
            if (!Reflected && owner != null && other.transform.IsChildOf(owner.transform)) return false;
            var enemy = other.GetComponentInParent<EnemyShip>();
            if (enemy != null) return (Reflected || enemy != owner) && !enemy.IsDefeated;
            if (Reflected) return false;
            return other.GetComponentInParent<Integration.IntegratedCombat>() != null || other.CompareTag("Player");
        }

        void RotateToDirection() => transform.rotation = Quaternion.Euler(0, 0,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        void OnTriggerEnter2D(Collider2D other) => Resolve(other);
        void OnTriggerStay2D(Collider2D other) => Resolve(other);

        void Resolve(Collider2D other)
        {
            if (resolved || !CanHit(other)) return;
            // This flag is shared by the sweep and trigger paths; one missile resolves once.
            resolved = true;
            var enemy = other.GetComponentInParent<EnemyShip>();
            if (enemy != null) enemy.TakeDamage(enemyDamage, direction);
            else
            {
                var combat = other.GetComponentInParent<Integration.IntegratedCombat>();
                if (combat != null) combat.Hit(hitbox, 1);
                else
                {
                    var demo = other.GetComponentInParent<DemoPlayer>();
                    if (demo != null) demo.TakeDamage(1);
                }
            }
            SimpleImpact.Spawn(transform.position, impactColor, enemy != null ? .5f : .3f);
            hitbox.enabled = false;
            Destroy(gameObject);
        }

        public void Absorb()
        {
            if (resolved) return;
            resolved = true;
            if (hitbox != null) hitbox.enabled = false;
            Destroy(gameObject);
        }
    }
}
