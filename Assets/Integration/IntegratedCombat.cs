using UnityEngine;
using Jain;
using Sanghyuk;

namespace Integration
{
    // One shared hull; legacy team references remain available to saved scenes.
    public class IntegratedCombat : MonoBehaviour
    {
        public const int MaxHealth = 8;
        public Player movement;
        public PlayerData data;
        public PlayerHp core;
        public GameObject bulletPrefab;
        public float fireInterval = .18f;
        public int selectedSkill = -1;
        public int bombs = 1;
        public int shotsFired, hitsTaken, skillUses;
        public int[] equippedPatches = { -1, -1 };
        public int ownedMask;
        [Header("Skill balance: Black hole / Slow / Reflect / Stop")]
        public float[] patchCooldowns = { 8, 8, 3, 12 };
        public float[] effectDurations = { 3, 5, 1, 3 };
        public float[] effectRadii = { 2.2f, 2.8f, 1.8f, 0 };
        [Range(.05f, 1)] public float slowFactor = .3f;
        [Header("Parts and protection")]
        [Min(0)] public float baseMoveSpeed = 5;
        [Min(0)] public float turboSpeedBonus = 2;
        [Min(1)] public int baseRamDamage = 20;
        [Min(0)] public int armorRamBonus = 10;
        [Min(1)] public int emergencyHealth = 4;
        [Min(1)] public int maxShields = 3;
        // Set only by the explicitly enabled development controller.
        [System.NonSerialized] public bool CheatInvulnerable;
        readonly float[] nextPatch = new float[4];
        float protectedUntil, invulnerableUntil;
        public int Health => data != null ? Mathf.Clamp(data.PlayerHp, 0, MaxHealth) : 0;
        public int RamDamage => IntegratedGame.Active != null && IntegratedGame.Active.shop != null &&
            IntegratedGame.Active.shop.IsPartEquipped(2) ? baseRamDamage + armorRamBonus : baseRamDamage;
        public int ShieldCount => data != null ? Mathf.Clamp(data.HaveShield, 0, maxShields) : 0;
        public float InvulnerabilityRemaining => Mathf.Max(0, invulnerableUntil - Time.time);
        public bool IsInvulnerable => CheatInvulnerable || Time.time < invulnerableUntil ||
            Time.time < protectedUntil || (data != null && data.Invin);

        void Awake()
        {
            if (movement == null) movement = GetComponent<Player>();
            if (data == null) data = GetComponent<PlayerData>();
            if (core == null) core = GetComponent<PlayerHp>();
            foreach (var hp in GetComponentsInChildren<PlayerHp>(true)) hp.integrated = true;
            // Existing scene references supply the artwork. No scene or UI rebuild is needed.
            if (GetComponent<CampaignDrops>() == null) gameObject.AddComponent<CampaignDrops>();
        }

        void Update()
        {
            MirrorHealth();
            RefreshPatchOwnership();
            var game = IntegratedGame.Active;
            RefreshPartEffects();
            if (game == null || !game.Running) return;
            if (Health <= 0) { game.End(false); return; }
            if (Input.GetKeyDown(KeyCode.B)) TryUsePatch(0);
            if (Input.GetKeyDown(KeyCode.V)) TryUsePatch(1);
            if (Input.GetKeyDown(KeyCode.R)) TryUsePatch(2);
            if (Input.GetKeyDown(KeyCode.T)) TryUsePatch(3);
        }

        public void RefreshPartEffects()
        {
            var game = IntegratedGame.Active;
            if (movement != null) movement.speed = baseMoveSpeed +
                (game != null && game.shop != null && game.shop.IsPartEquipped(1) ? turboSpeedBonus : 0);
        }

        void MirrorHealth()
        {
            if (data == null) return;
            data.PlayerHp = Health;
            if (core != null) core.playerHp = Health;
        }

        // Source-compatible entry points: this game has no basic gun or free skills.
        public void Fire() { }
        public bool TrySkill() => false;
        public bool TryBomb() => false;
        public float SkillCooldown => selectedSkill >= 0 ? PatchCooldown(selectedSkill) : 0;

        public void Hit(Collider2D collider, int amount)
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running || data == null || amount <= 0 ||
                IsInvulnerable) return;
            protectedUntil = Time.time + .8f;
            Vector2 away = collider != null ? (Vector2)(transform.position - collider.bounds.center) : Vector2.zero;
            if (away.sqrMagnitude < .01f) away = movement != null ? -movement.FacingDirection : Vector2.down;
            if (movement != null) movement.AddKnockback(away.normalized * 5);
            if (data.HaveShield > 0) { data.HaveShield--; return; }
            hitsTaken++;
            if (game.shop != null && game.shop.IsPartEquipped(0) && Health <= amount)
            {
                game.shop.ConsumeEmergencyPart();
                data.PlayerHp = Mathf.Clamp(emergencyHealth, 1, MaxHealth);
                protectedUntil = Time.time + 1.5f;
            }
            else data.PlayerHp = Mathf.Max(0, Health - amount);
            MirrorHealth();
            if (Health <= 0) game.End(false);
        }

        public void Heal(int amount)
        {
            if (data == null || amount <= 0) return;
            data.PlayerHp = Mathf.Min(MaxHealth, Health + amount);
            MirrorHealth();
        }

        public void GrantInvulnerability(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + Mathf.Max(0, seconds));
        }

        public void GrantPickup(int kind, int amount = 1, float duration = 3)
        {
            if (kind >= 0 && kind < 4) GrantPatch(kind, amount);
            else if (kind == 4) Heal(amount);
            else if (kind == 5) GrantInvulnerability(duration);
            else if (kind == 6 && data != null) data.HaveShield = Mathf.Clamp(data.HaveShield + Mathf.Max(0, amount), 0, maxShields);
        }

        public bool EquipPatch(int kind, int slot)
        {
            // Legacy button binding only; all four inventories are always independently usable.
            if (kind < 0 || kind > 3 || slot < 0 || slot > 1 || PatchCount(kind) <= 0) return false;
            if (equippedPatches == null || equippedPatches.Length != 2) equippedPatches = new[] { -1, -1 };
            equippedPatches[slot] = kind;
            return true;
        }

        public bool IsEquipped(int kind) => kind >= 0 && kind < 4 && PatchCount(kind) > 0;

        public void GrantPatch(int kind, int amount = 1)
        {
            var shop = IntegratedGame.Active != null ? IntegratedGame.Active.shop : null;
            if (kind < 0 || kind > 3 || amount < 0 || shop == null) return;
            SetCount(kind, PatchCount(kind) + amount);
            if (PatchCount(kind) <= 0) return;
            ownedMask |= 1 << kind;
            shop.SyncLegacyData(false);
        }

        public void RefreshPatchOwnership()
        {
            for (int kind = 0; kind < 4; kind++)
                if (PatchCount(kind) > 0 && (ownedMask & (1 << kind)) == 0) GrantPatch(kind, 0);
        }

        public int PatchCount(int kind)
        {
            var shop = IntegratedGame.Active != null ? IntegratedGame.Active.shop : null;
            if (shop == null) return 0;
            return Mathf.Max(0, kind == 0 ? shop.pull : kind == 1 ? shop.slow : kind == 2 ? shop.reflex : kind == 3 ? shop.stop : 0);
        }

        void SetCount(int kind, int count)
        {
            var shop = IntegratedGame.Active.shop;
            if (kind == 0) shop.pull = count;
            else if (kind == 1) shop.slow = count;
            else if (kind == 2) shop.reflex = count;
            else if (kind == 3) shop.stop = count;
        }

        public float PatchCooldown(int kind) => kind < 0 || kind > 3 ? 0 : Mathf.Max(0, nextPatch[kind] - Time.time);
        public bool TryConsumable(int kind) => TryUsePatch(kind);

        static float BalanceValue(float[] values, int kind, float fallback)
        {
            return values != null && kind >= 0 && kind < values.Length ? Mathf.Max(0, values[kind]) : fallback;
        }

        public bool TryUsePatch(int kind)
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running || kind < 0 || kind > 3 ||
                PatchCount(kind) <= 0 || PatchCooldown(kind) > 0) return false;
            float duration = BalanceValue(effectDurations, kind, kind == 1 ? 5 : 3);
            if (kind == 3)
            {
                if (!game.ActivateTimeStop(duration)) return false;
            }
            else
            {
                if (movement == null) return false;
                GameObject prefab = kind == 0 ? movement.BHole : kind == 1 ? movement.GravityUnit : movement.ParryingUnit;
                var go = prefab != null ? Instantiate(prefab, transform.position, Quaternion.identity) :
                    new GameObject("Skill " + kind);
                go.transform.position = transform.position;
                // Keep the team's graphics and disable legacy independent collision/timer logic.
                foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                foreach (var collider in go.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
                foreach (var body in go.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
                var effect = go.AddComponent<IntegratedSkillEffect>();
                effect.owner = this; effect.kind = kind;
                effect.duration = Mathf.Max(.05f, duration);
                effect.radius = Mathf.Max(.1f, BalanceValue(effectRadii, kind, 2.2f));
                effect.slowFactor = slowFactor;
            }
            SetCount(kind, PatchCount(kind) - 1);
            game.shop.SyncLegacyData(false);
            nextPatch[kind] = Time.time + BalanceValue(patchCooldowns, kind, 8);
            skillUses++;
            return true;
        }
    }
}
