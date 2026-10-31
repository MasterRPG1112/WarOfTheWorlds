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
        readonly float[] nextPatch = new float[4];
        static readonly float[] cooldowns = { 8, 8, 2, 12 };
        float protectedUntil;
        public int Health => data != null ? Mathf.Clamp(data.PlayerHp, 0, MaxHealth) : 0;
        public int RamDamage => IntegratedGame.Active != null && IntegratedGame.Active.shop != null &&
            IntegratedGame.Active.shop.part3 > 0 ? 30 : 20;

        void Awake()
        {
            if (movement == null) movement = GetComponent<Player>();
            if (data == null) data = GetComponent<PlayerData>();
            if (core == null) core = GetComponent<PlayerHp>();
            foreach (var hp in GetComponentsInChildren<PlayerHp>(true)) hp.integrated = true;
        }

        void Update()
        {
            MirrorHealth();
            RefreshPatchOwnership();
            var game = IntegratedGame.Active;
            if (game == null || !game.Running) return;
            if (movement != null) movement.speed = 5 + (game.shop != null && game.shop.part2 > 0 ? 2 : 0);
            if (Health <= 0) { game.End(false); return; }
            if (Input.GetKeyDown(KeyCode.Q)) UseSlot(0);
            if (Input.GetKeyDown(KeyCode.E)) UseSlot(1);
            if (Input.GetKeyDown(KeyCode.B)) TryUsePatch(0);
            if (Input.GetKeyDown(KeyCode.V)) TryUsePatch(1);
            if (Input.GetKeyDown(KeyCode.R)) TryUsePatch(2);
            if (Input.GetKeyDown(KeyCode.T)) TryUsePatch(3);
        }

        void UseSlot(int slot)
        {
            if (equippedPatches != null && slot < equippedPatches.Length) TryUsePatch(equippedPatches[slot]);
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
                Time.time < protectedUntil || data.Invin) return;
            protectedUntil = Time.time + .8f;
            Vector2 away = collider != null ? (Vector2)(transform.position - collider.bounds.center) : Vector2.zero;
            if (away.sqrMagnitude < .01f) away = movement != null ? -movement.FacingDirection : Vector2.down;
            if (movement != null) movement.AddKnockback(away.normalized * 5);
            if (data.HaveShield > 0) { data.HaveShield--; return; }
            hitsTaken++;
            if (game.shop != null && game.shop.part1 > 0 && Health <= amount)
            {
                game.shop.part1 = 0;
                data.PlayerHp = 4;
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

        public bool EquipPatch(int kind, int slot)
        {
            if (kind < 0 || kind > 3 || slot < 0 || slot > 1 || (ownedMask & (1 << kind)) == 0) return false;
            if (equippedPatches == null || equippedPatches.Length != 2) equippedPatches = new[] { -1, -1 };
            if (equippedPatches[1 - slot] == kind) return false;
            equippedPatches[slot] = kind;
            return true;
        }

        public bool IsEquipped(int kind) => kind >= 0 && kind < 4 && equippedPatches != null &&
            equippedPatches.Length == 2 && (equippedPatches[0] == kind || equippedPatches[1] == kind);

        public void GrantPatch(int kind, int amount = 1)
        {
            var shop = IntegratedGame.Active != null ? IntegratedGame.Active.shop : null;
            if (kind < 0 || kind > 3 || amount < 0 || shop == null) return;
            SetCount(kind, PatchCount(kind) + amount);
            if (PatchCount(kind) <= 0) return;
            ownedMask |= 1 << kind;
            if (equippedPatches == null || equippedPatches.Length != 2) equippedPatches = new[] { -1, -1 };
            if (!IsEquipped(kind))
                for (int i = 0; i < 2; i++) if (equippedPatches[i] < 0) { EquipPatch(kind, i); break; }
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

        public bool TryUsePatch(int kind)
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running || kind < 0 || kind > 3 || !IsEquipped(kind) ||
                (ownedMask & (1 << kind)) == 0 || PatchCount(kind) <= 0 || PatchCooldown(kind) > 0) return false;
            if (kind == 3)
            {
                if (!game.ActivateTimeStop()) return false;
            }
            else
            {
                if (movement == null) return false;
                GameObject prefab = kind == 0 ? movement.BHole : kind == 1 ? movement.GravityUnit : movement.ParryingUnit;
                if (prefab == null) return false;
                var go = Instantiate(prefab, transform.position, Quaternion.identity);
                // Keep the team's graphics and disable legacy independent collision/timer logic.
                foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                foreach (var collider in go.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
                foreach (var body in go.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
                var effect = go.AddComponent<IntegratedSkillEffect>();
                effect.owner = this; effect.kind = kind;
            }
            SetCount(kind, PatchCount(kind) - 1);
            nextPatch[kind] = Time.time + cooldowns[kind];
            skillUses++;
            return true;
        }
    }
}
