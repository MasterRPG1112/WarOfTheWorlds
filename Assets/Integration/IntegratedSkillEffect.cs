using System.Collections.Generic;
using UnityEngine;
using Taeyang;

namespace Integration
{
    public class IntegratedSkillEffect : MonoBehaviour
    {
        readonly HashSet<EnemyProjectile> slowed = new HashSet<EnemyProjectile>();
        public IntegratedCombat owner;
        public int kind;
        float age;

        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            var game = IntegratedGame.Active;
            if (game != null && !game.Running) return;
            age += Time.deltaTime;
            if (age >= (kind == 0 ? 3 : kind == 1 ? 5 : .125f)) { ReleaseSlow(); Destroy(gameObject); return; }
            if (kind != 0) transform.position = owner.transform.position;
            float radius = kind == 2 ? 1.3f : 2.2f;
            foreach (var bullet in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                if ((bullet.transform.position - transform.position).sqrMagnitude > radius * radius) continue;
                if (kind == 1) { bullet.SetSlow(this, .3f); slowed.Add(bullet); }
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
