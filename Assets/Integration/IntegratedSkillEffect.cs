using UnityEngine;
using Taeyang;
namespace Integration
{
    public class IntegratedSkillEffect : MonoBehaviour
    {
        readonly System.Collections.Generic.List<EnemyProjectile> slowed = new System.Collections.Generic.List<EnemyProjectile>();
        public IntegratedCombat owner;
        public int kind;
        float age;
        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            if (kind != 0) transform.position = owner.transform.position;
            float radius = kind == 2 ? 1.3f : 2.2f;
            foreach (var bullet in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                if (Vector2.Distance(bullet.transform.position, transform.position) > radius) continue;
                if (kind == 1) { bullet.externalSpeedFactor = .3f; if (!slowed.Contains(bullet)) slowed.Add(bullet); }
                else
                {
                    Destroy(bullet.gameObject);
                    if (kind == 2) owner.Fire();
                }
            }
            if (age > (kind == 0 ? 3 : kind == 1 ? 5 : .125f)) Destroy(gameObject);
        }
        void OnDestroy() { foreach (var bullet in slowed) if (bullet != null) bullet.externalSpeedFactor = 1; }
    }
}
