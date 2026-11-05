using UnityEngine;
using Taeyang;

namespace Integration
{
    // Reuses each stage's enemy art and projectile assets, without an EnemyShip
    // component: bosses never retreat and are immune to all missile damage.
    public class CampaignBoss : IntegratedBossTarget
    {
        public int stage;
        public EnemyProjectile missile;
        SpriteRenderer art;
        float age, nextAttack = 1.5f, flashUntil;
        Color tint;
        public int Attacks { get; private set; }
        // A red pulse warns of contact danger. The other three seconds in each
        // cycle let a careful unarmed player ram without unavoidable attrition.
        public override bool ContactHazard => Mathf.Repeat(age,4) < 1;

        public static GameObject Create(StageDirector director)
        {
            var go = new GameObject("Stage " + director.stageNumber + " Boss");
            go.SetActive(false);
            go.transform.position = director.bossPosition;
            go.transform.localScale = Vector3.one * 2.2f;
            var source = director.enemyPrefabs[director.stageNumber == 1 ? 1 : director.stageNumber == 2 ? 2 : 4];
            var visual = go.AddComponent<SpriteRenderer>();
            visual.sprite = source.GetComponent<SpriteRenderer>().sprite;
            visual.sortingOrder = 12;
            var collider = go.AddComponent<CircleCollider2D>(); collider.radius = .65f; collider.isTrigger = true;
            var body = go.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.useFullKinematicContacts = true;
            var boss = go.AddComponent<CampaignBoss>();
            boss.stage = director.stageNumber;
            boss.director = director;
            boss.maxHealth = 160 + (boss.stage - 1) * 120;
            boss.missile = source.homingProjectile != null ? source.homingProjectile : source.straightProjectile;
            go.SetActive(true);
            return go;
        }
        protected override void Awake()
        {
            base.Awake(); art = GetComponent<SpriteRenderer>();
            tint = stage == 1 ? new Color(1,.65f,.4f) : stage == 2 ? new Color(.8f,.45f,1) : new Color(.4f,.9f,1);
        }
        void Update()
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running || IsDefeated) return;
            age += Time.deltaTime;
            art.color = Time.time < flashUntil ? Color.white : ContactHazard ? new Color(1,.2f,.2f) : tint;
            Rect view = BattleArena.ViewBounds();
            transform.position = new Vector3(Mathf.Sin(age * (.35f + stage * .12f)) * view.width * .28f,
                view.yMax - 3 + Mathf.Sin(age * .8f) * .6f, 0);
            if (age < nextAttack) return;
            nextAttack = age + (stage == 1 ? 2 : stage == 2 ? 1.5f : 1.1f);
            Attack(game.player.transform);
        }
        public void Attack(Transform player)
        {
            if (missile == null || player == null) return;
            Attacks++;
            Vector2 aim = (player.position - transform.position).normalized;
            if (stage >= 2 && Attacks % 2 == 0)
            {
                int count = stage == 2 ? 10 : 14;
                float spin = age * (stage == 2 ? 15 : 45);
                for (int i=0;i<count;i++)
                {
                    Vector2 direction = Quaternion.Euler(0,0,spin+i*360f/count) * Vector2.up;
                    var ring = Instantiate(missile,transform.position+(Vector3)(direction*1.5f),Quaternion.identity);
                    ring.Initialize(null,direction,null,.7f+stage*.15f);
                }
                return;
            }
            if (stage == 3)
            {
                var body = player.GetComponent<Rigidbody2D>();
                if(body!=null)aim=(StageOneMath.Predict(transform.position,player.position,body.linearVelocity,missile.speed,1.2f)-(Vector2)transform.position).normalized;
            }
            int half = stage == 1 ? 1 : stage == 2 ? 2 : 3;
            for (int i = -half; i <= half; i++)
            {
                Vector2 direction = Quaternion.Euler(0,0,i * (stage == 3 ? 30 : 18)) * aim;
                var shot = Instantiate(missile, transform.position + (Vector3)(direction * 1.5f), Quaternion.identity);
                shot.Initialize(null, direction, i == 0 ? player : null, .8f + stage * .15f);
            }
        }
        protected override void OnDamaged() { flashUntil = Time.time + .15f; }
        protected override void OnDefeated()
        {
            SimpleImpact.Spawn(transform.position, tint, 3);
            foreach (var shot in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) shot.Absorb();
        }
    }
}
