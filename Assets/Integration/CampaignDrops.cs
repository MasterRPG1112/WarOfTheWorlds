using UnityEngine;
using Taeyang;

namespace Integration
{
    // Added to the existing player automatically; all balance values remain Inspector-editable.
    public class CampaignDrops : MonoBehaviour
    {
        [Range(0, 1)] public float dropChance = .35f;
        [Tooltip("B / V / R / T / Repair / Invulnerability / Shield")]
        public float[] weights = { 20, 20, 18, 12, 16, 6, 8 };
        public GameObject[] pickupPrefabs = new GameObject[7];
        [Min(.1f)] public float pickupLifetime = 18;
        [Min(.1f)] public float pickupRadius = .75f;
        [Min(0)] public float attractionRadius = 1.7f;
        [Min(0)] public float attractionSpeed = 3;
        [Min(0)] public float invulnerabilityDuration = 3;
        [Min(1)] public int healAmount = 2;
        [Min(1)] public int stockPerPickup = 1;
        [Tooltip("Scales pickup artwork once; collection and attraction distances stay in world units.")]
        [Range(.1f, 2f)] public float visualSizeMultiplier = .7f;
        [Tooltip("Maximum combined sprite width/height in world units; oversized team icons shrink uniformly.")]
        [Min(.1f)] public float maxVisualWorldSize = .65f;

        void OnEnable() => EnemyShip.DestroyedByPlayer += OnEnemyDestroyed;
        void OnDisable() => EnemyShip.DestroyedByPlayer -= OnEnemyDestroyed;

        void Start()
        {
            if (IntegratedGame.Active == null) return;
            // Reuse the team's serialized item art, replacing periodic sky spawns with kill drops.
            foreach (var spawner in FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None))
            {
                GameObject prefab = spawner.itemPrefab;
                if (prefab != null)
                {
                    var patch = prefab.GetComponent<PatchPickup>();
                    int kind = patch != null ? patch.kind : prefab.GetComponent<HealItem>() != null ? 4 :
                        prefab.GetComponent<Invincibility>() != null ? 5 : prefab.GetComponent<Shield>() != null ? 6 : -1;
                    if (kind >= 0 && kind < 7 && pickupPrefabs != null && pickupPrefabs.Length > kind && pickupPrefabs[kind] == null)
                        pickupPrefabs[kind] = prefab;
                }
                spawner.CancelInvoke();
                spawner.enabled = false;
            }
        }

        void OnEnemyDestroyed(EnemyShip enemy, int score)
        {
            var game = IntegratedGame.Active;
            if (enemy == null || game == null || !game.Running || Random.value >= dropChance) return;
            int kind = ChooseKind(Random.value);
            if (kind >= 0) SpawnPickup(kind, enemy.transform.position);
        }

        // A normalized sample makes the same weighted table usable in deterministic checks.
        public int ChooseKind(float sample)
        {
            float total = 0;
            for (int i = 0; i < 7; i++) total += Weight(i);
            if (total <= 0) return -1;
            float target = Mathf.Clamp01(sample) * total;
            int last = -1;
            for (int i = 0; i < 7; i++)
            {
                float weight = Weight(i);
                if (weight <= 0) continue;
                last = i;
                target -= weight;
                if (target < 0) return i;
            }
            return last;
        }

        float Weight(int index) => weights != null && index < weights.Length ? Mathf.Max(0, weights[index]) : 0;

        public PatchPickup SpawnPickup(int kind, Vector3 position)
        {
            if (kind < 0 || kind > 6) return null;
            var prefab = pickupPrefabs != null && kind < pickupPrefabs.Length ? pickupPrefabs[kind] : null;
            var item = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : new GameObject("Drop " + kind);
            item.transform.position = position;
            item.name = "Enemy drop " + kind;
            // The pickup component exclusively owns collection and lifetime. Keep all graphics intact.
            foreach (var script in item.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(script is PatchPickup)) script.enabled = false;
            foreach (var collider in item.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in item.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            var pickup = item.GetComponent<PatchPickup>();
            if (pickup == null) pickup = item.AddComponent<PatchPickup>();
            pickup.enabled = true;
            pickup.kind = kind;
            pickup.amount = kind == 4 ? Mathf.Max(1, healAmount) : Mathf.Max(1, stockPerPickup);
            pickup.lifetime = Mathf.Max(.1f, pickupLifetime);
            pickup.pickupRadius = Mathf.Max(.1f, pickupRadius);
            pickup.attractionRadius = Mathf.Max(pickup.pickupRadius, attractionRadius);
            pickup.attractionSpeed = Mathf.Max(0, attractionSpeed);
            pickup.effectDuration = Mathf.Max(0, invulnerabilityDuration);
            pickup.driftSpeed = 0;
            if (prefab == null)
            {
                var visual = item.AddComponent<SpriteRenderer>();
                string[] icons = { "drop-blackhole", "drop-slow", "drop-reflect", "drop-time", "drop-heal", "drop-invulnerable", "drop-shield" };
                visual.sprite = Resources.Load<Sprite>("CampaignArt/" + icons[kind]);
                if (visual.sprite == null)
                {
                    visual.sprite = StageBackdrop.MakeSprite(6);
                    visual.color = new[] { new Color(.6f, .3f, 1), Color.cyan, Color.yellow, Color.blue,
                        Color.green, Color.white, new Color(.3f, .7f, 1) }[kind];
                }
                visual.sortingOrder = 8;
                float size = visual.sprite != null ? Mathf.Max(visual.sprite.bounds.size.x, visual.sprite.bounds.size.y) : 1;
                item.transform.localScale = Vector3.one * (.65f / Mathf.Max(.01f, size));
            }
            // Both cached team prefabs and generated fallback icons use one root
            // multiplier, preserving their artwork aspect and child proportions.
            item.transform.localScale *= Mathf.Clamp(visualSizeMultiplier, .1f, 2f);
            Bounds visualBounds = default(Bounds);
            bool hasVisualBounds = false;
            foreach (var visual in item.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (visual.sprite == null) continue;
                if (!hasVisualBounds) { visualBounds = visual.bounds; hasVisualBounds = true; }
                else visualBounds.Encapsulate(visual.bounds);
            }
            float largestSide = Mathf.Max(visualBounds.size.x, visualBounds.size.y);
            float sizeLimit = Mathf.Max(.1f, maxVisualWorldSize);
            if (hasVisualBounds && largestSide > sizeLimit)
                item.transform.localScale *= sizeLimit / largestSide;
            return pickup;
        }

        public void ClearPickups()
        {
            foreach (var pickup in FindObjectsByType<PatchPickup>(FindObjectsSortMode.None)) Destroy(pickup.gameObject);
        }
    }
}
