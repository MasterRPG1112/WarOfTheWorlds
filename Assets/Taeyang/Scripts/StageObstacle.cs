using UnityEngine;
namespace Taeyang
{
    public class StageObstacle : MonoBehaviour
    {
        [Min(.2f)] public float movementSpeed = 1.15f;
        [Min(2)] public float lifetime = 25;
        [Min(.1f)] public float radius = .55f;
        Vector2 velocity;
        float age, originX, spin;
        int stage;
        float bottomLimit = -6.5f;
        Rigidbody2D body;
        Collider2D hull;
        float nextContact;
        public void Initialize(int value)
        {
            // Stage 1 is deliberately free of meteor hazards, even when called externally.
            if (value < 2) { Destroy(gameObject); return; }
            stage = value; originX = transform.position.x;
            var visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sprite = Resources.Load<Sprite>(value == 2 ? "CampaignArt/meteor-a" : "CampaignArt/meteor-b");
            if (visual.sprite == null) visual.sprite = StageBackdrop.MakeSprite(value == 2 ? 6 : 9);
            visual.color = value == 2 ? Color.white : new Color(.82f, .7f, 1);
            visual.sortingOrder = 2;
            Vector2 size = visual.sprite.bounds.size;
            // Keep a compact obstacle with ample routes around it.
            transform.localScale = Vector3.one * (1.45f / Mathf.Max(size.x, size.y));
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = radius / Mathf.Max(.01f, transform.localScale.x);
            collider.isTrigger = false;
            hull = collider;
            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            velocity = new Vector2(Random.Range(-.25f, .25f), -movementSpeed);
            spin = Random.Range(-30, 30);
            var game = Integration.IntegratedGame.Active;
            if (game != null)
            {
                EnsurePlayerHull(game.player);
                bottomLimit = Integration.BattleArena.ViewBounds().yMin - 2;
                lifetime = Mathf.Max(lifetime, (transform.position.y - bottomLimit) / movementSpeed + 1);
            }
        }
        static void EnsurePlayerHull(Integration.IntegratedCombat player)
        {
            if (player == null || player.transform.Find("Meteor movement hull") != null) return;
            // Existing team hitboxes remain triggers; this small extra hull permits solid rock contacts.
            var go = new GameObject("Meteor movement hull");
            go.transform.SetParent(player.transform, false);
            var collider = go.AddComponent<CircleCollider2D>();
            float scale = Mathf.Max(.01f, Mathf.Abs(player.transform.lossyScale.x));
            collider.radius = .5f / scale;
            collider.isTrigger = false;
        }
        void FixedUpdate()
        {
            if (body == null) return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
            var game = Integration.IntegratedGame.Active;
            if (game != null && (!game.Running || game.EnemiesFrozen)) return;
            age += Time.fixedDeltaTime;
            float x = stage == 3 ? originX + Mathf.Sin(age * .65f) * .7f : body.position.x + velocity.x * Time.fixedDeltaTime;
            body.MovePosition(new Vector2(x, body.position.y + velocity.y * Time.fixedDeltaTime));
            body.MoveRotation(body.rotation + spin * Time.fixedDeltaTime);
            if (age > lifetime || body.position.y < bottomLimit) Destroy(gameObject);
        }
        void OnCollisionEnter2D(Collision2D collision) => Contact(collision.collider);
        void OnCollisionStay2D(Collision2D collision) => Contact(collision.collider);
        void OnTriggerEnter2D(Collider2D other)
        {
            // Meteor geometry is the obstacle. Missiles can be intercepted by its surface.
            var bullet = other.GetComponentInParent<EnemyProjectile>();
            if (bullet != null) { bullet.Absorb(); return; }
            // Legacy demo player retains the previous damage contract.
            if (Integration.IntegratedGame.Active == null)
            {
                var player = other.GetComponentInParent<DemoPlayer>();
                if (player != null) player.TakeDamage(1);
            }
        }
        void Contact(Collider2D other)
        {
            var game = Integration.IntegratedGame.Active;
            if (game == null || !game.Running || game.EnemiesFrozen || Time.time < nextContact) return;
            var combat = other.GetComponentInParent<Integration.IntegratedCombat>();
            if (combat == null) return;
            nextContact = Time.time + 1.2f;
            combat.Hit(hull, 1);
            SimpleImpact.Spawn(other.ClosestPoint(transform.position), Color.white, .35f);
        }
    }
}
