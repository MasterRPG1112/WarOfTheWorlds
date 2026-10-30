using UnityEngine;
using Jain;
using Sanghyuk;
using Taeyang;

namespace Integration
{
    // Adapter for the team's movement, segmented HP, item data and skill prefabs.
    public class IntegratedCombat : MonoBehaviour
    {
        public Player movement;
        public PlayerData data;
        public PlayerHp core;
        public GameObject bulletPrefab;
        public float fireInterval = .18f;
        public int selectedSkill = -1;
        public int bombs = 1;
        public int shotsFired, hitsTaken, skillUses;
        float nextShot, nextSkill, protectedUntil;
        void Update()
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running) return;
            bool wingLost = false;
            foreach (var hp in GetComponentsInChildren<PlayerHp>()) if (hp != core && hp.playerHp <= 0) wingLost = true;
            movement.speed = (wingLost ? 3 : 5) + (game.shop.part2 > 0 ? 2 : 0);
            // Items write PlayerData; segmented HP/UI use PlayerHp.
            core.playerHp = Mathf.Clamp(data.PlayerHp, 0, 8);
            if (core.playerHp <= 0) { game.End(false); return; }
            if (Time.time >= nextShot) Fire();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift)) TrySkill();
            if (Input.GetKeyDown(KeyCode.B)) TryConsumable(0);
            if (Input.GetKeyDown(KeyCode.V)) TryConsumable(1);
            if (Input.GetKeyDown(KeyCode.R)) TryConsumable(2);
            if (Input.GetKeyDown(KeyCode.T)) game.TryTimeStop();
            if (Input.GetKeyDown(KeyCode.X)) TryBomb();
        }
        public void Fire()
        {
            if (bulletPrefab == null) return;
            nextShot = Time.time + fireInterval;
            var go = Instantiate(bulletPrefab, transform.position + Vector3.up * .8f, Quaternion.identity);
            go.tag = "Untagged";
            var body = go.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
            foreach (var c in go.GetComponents<Collider2D>()) c.isTrigger = true;
            go.AddComponent<PlayerShot>().damage = IntegratedGame.Active.shop.part3 > 0 ? 20 : 12; shotsFired++;
        }
        public void Hit(Collider2D collider, int amount)
        {
            if (IntegratedGame.Active == null || !IntegratedGame.Active.Running || Time.time < protectedUntil || data.Invin) return;
            if (data.HaveShield > 0) { data.HaveShield = 0; protectedUntil = Time.time + .8f; return; }
            if (IntegratedGame.Active.shop.part1 > 0 && core.playerHp <= amount) { IntegratedGame.Active.shop.part1 = 0; core.playerHp = 4; data.PlayerHp = 4; protectedUntil = Time.time + 2; return; }
            var hp = collider.GetComponent<PlayerHp>();
            if (hp == null) hp = core;
            hp.playerHp = Mathf.Max(0, hp.playerHp - amount);
            if (hp == core) data.PlayerHp = Mathf.CeilToInt(hp.playerHp);
            else if (hp.playerHp == 0) { collider.enabled = false; movement.speed = 3; }
            hitsTaken++; protectedUntil = Time.time + .8f;
            if (core.playerHp <= 0) IntegratedGame.Active.End(false);
        }
        public bool TrySkill()
        {
            if (IntegratedGame.Active == null || !IntegratedGame.Active.Running || selectedSkill < 0 || Time.time < nextSkill) return false;
            if (!CastSkill(selectedSkill)) return false;
            nextSkill = Time.time + (selectedSkill == 2 ? 2 : 8); return true;
        }
        bool CastSkill(int kind)
        {
            GameObject prefab = kind == 0 ? movement.BHole : kind == 1 ? movement.GravityUnit : movement.ParryingUnit;
            if (prefab == null) return false;
            var go = Instantiate(prefab, transform.position, Quaternion.identity);
            var effect = go.AddComponent<IntegratedSkillEffect>(); effect.owner = this; effect.kind = kind;
            // Original prefabs use solid collisions, while Taeyang bullets use triggers.
            foreach (var c in go.GetComponents<Collider2D>()) c.isTrigger = true;
            skillUses++; return true;
        }
        public bool TryConsumable(int kind)
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running || kind < 0 || kind > 2) return false;
            var shop = game.shop;
            int count = kind == 0 ? shop.pull : kind == 1 ? shop.slow : shop.reflex;
            if (count <= 0 || !CastSkill(kind)) return false;
            if (kind == 0) shop.pull--; else if (kind == 1) shop.slow--; else shop.reflex--;
            return true;
        }
        public bool TryBomb()
        {
            if (bombs <= 0 || IntegratedGame.Active == null || !IntegratedGame.Active.Running || movement.Origin == null) return false;
            var go = Instantiate(movement.Origin, new Vector3(0,-6,0),Quaternion.identity);
            foreach (var c in go.GetComponents<Collider2D>()) c.isTrigger = true;
            go.AddComponent<IntegratedBomb>(); bombs--; return true;
        }
        public float SkillCooldown => Mathf.Max(0, nextSkill - Time.time);
    }
}
