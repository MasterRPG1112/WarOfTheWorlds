using UnityEngine;
namespace Integration
{
    public class PatchPickup : MonoBehaviour
    {
        // 0..3: B/V/R/T stock, 4: repair, 5: invulnerability, 6: shield.
        [Range(0,6)] public int kind;
        [Min(1)] public int amount = 1;
        [Min(.1f)] public float lifetime = 18;
        [Min(.1f)] public float pickupRadius = .75f;
        [Min(0)] public float attractionRadius = 1.7f;
        [Min(0)] public float attractionSpeed = 3;
        [Min(0)] public float effectDuration = 3;
        [Min(0)] public float driftSpeed;
        float age;
        bool collected;
        public bool Collected => collected;
        void Update()
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running || collected) return;
            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }
            transform.position += Vector3.down * (driftSpeed * Time.deltaTime);
            var player = game.player;
            if (player == null) return;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance <= pickupRadius) Collect(player);
            else if (distance <= attractionRadius)
                transform.position = Vector3.MoveTowards(transform.position, player.transform.position, attractionSpeed * Time.deltaTime);
        }
        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<IntegratedCombat>();
            Collect(player);
        }
        public bool Collect(IntegratedCombat player)
        {
            var game = IntegratedGame.Active;
            if (collected || player == null || game == null || !game.Running || kind < 0 || kind > 6) return false;
            collected = true;
            player.GrantPickup(kind, Mathf.Max(1, amount), effectDuration);
            Destroy(gameObject);
            return true;
        }
    }
}
