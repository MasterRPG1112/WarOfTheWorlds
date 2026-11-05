using System.Collections.Generic;
using UnityEngine;
using Taeyang;

namespace Integration
{
    public class IntegratedSkillEffect : MonoBehaviour
    {
        readonly HashSet<EnemyProjectile> slowed = new HashSet<EnemyProjectile>();
        readonly List<EnemyProjectile> leaving = new List<EnemyProjectile>();
        public IntegratedCombat owner;
        public int kind;
        [Min(.05f)] public float duration = 3;
        [Min(.1f)] public float radius = 2.2f;
        [Range(.05f, 1)] public float slowFactor = .3f;
        float age;

        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            var game = IntegratedGame.Active;
            if (game != null && !game.Running) return;
            age += Time.deltaTime;
            if (age >= duration) { ReleaseSlow(); Destroy(gameObject); return; }
            if (kind != 0) transform.position = owner.transform.position;
            if (kind == 1)
            {
                leaving.Clear();
                foreach (var bullet in slowed)
                    if (bullet == null || (bullet.transform.position - transform.position).sqrMagnitude > radius * radius)
                        leaving.Add(bullet);
                foreach (var bullet in leaving)
                {
                    if (bullet != null) bullet.RemoveSlow(this);
                    slowed.Remove(bullet);
                }
            }
            foreach (var bullet in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                if ((bullet.transform.position - transform.position).sqrMagnitude > radius * radius) continue;
                if (kind == 1) { bullet.SetSlow(this, slowFactor); slowed.Add(bullet); }
                else if (kind == 0) bullet.Absorb();
                else if (!bullet.Reflected)
                {
                    EnemyShip nearest = null;
                    float distance = float.PositiveInfinity;
                    foreach (var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))
                    {
                        if (enemy.IsDefeated || enemy.GetComponentInParent<IntegratedBossTarget>() != null) continue;
                        float candidate = (enemy.transform.position - bullet.transform.position).sqrMagnitude;
                        if (candidate < distance) { nearest = enemy; distance = candidate; }
                    }
                    Vector2 aim = nearest != null ? (Vector2)(nearest.transform.position - bullet.transform.position) :
                        (Vector2)(bullet.transform.position - owner.transform.position);
                    if (aim.sqrMagnitude < .01f) aim = owner.movement != null ? owner.movement.FacingDirection : Vector2.up;
                    bullet.Reflect(aim);
                }
            }
        }

        void ReleaseSlow()
        {
            foreach (var bullet in slowed) if (bullet != null) bullet.RemoveSlow(this);
            slowed.Clear();
        }
        void OnDisable() => ReleaseSlow();
        void OnDestroy() => ReleaseSlow();
    }
}
