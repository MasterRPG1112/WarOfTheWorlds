using System.Collections.Generic;
using UnityEngine;
using Taeyang;

namespace Integration
{
    // Uses the existing missile/skill collision contract; only hull rams change boss HP.
    public class CampaignBoss : IntegratedBossTarget
    {
        [Range(1, 3)] public int stageNumber = 1;
        [Min(.8f)] public float warningDuration = 1.15f;
        [Min(1.2f)] public float recoveryDuration = 2.6f;
        [Min(.5f)] public float bulletSpeed = 3.1f;
        [Range(.1f, .7f)] public float secondPhaseHealth = .45f;
        [Min(1)] public float dashSpeed = 8;
        public EnemyProjectile projectilePrefab;
        public bool PhaseTwo { get; private set; }
        public int StageNumber => stageNumber;
        public bool IsWarning => state == AttackState.Warning;
        public string PatternName => Names[Mathf.Clamp(stageNumber, 1, 3) - 1, Mathf.Clamp(pattern, 0, 3)];
        static readonly string[,] Names = {
            { "원형 탄막", "부채꼴 탄막", "돌진", "다중 원형 탄막" },
            { "유도 미사일", "회전 탄막", "예측 집중 포격", "다방향 미사일" },
            { "나선 탄막", "확산 탄막", "예측 포격", "성운 차단 탄막" }
        };
        enum AttackState { Recovery, Warning, Attack, Death }
        AttackState state;
        Rigidbody2D body;
        SpriteRenderer[] visuals;
        SpriteRenderer deathArtwork;
        float deathArtworkScale;
        Color tint;
        float stateTime, shotTimer, clock, flash, deathBurst;
        int pattern = -1, volley;
        Vector2 aimPoint, dashEnd, home;
        float gapAngle;
        GameObject warnings;
        readonly List<EnemyProjectile> projectiles = new List<EnemyProjectile>();

        public static CampaignBoss Create(StageDirector owner)
        {
            var go = new GameObject("Stage " + owner.stageNumber + " campaign boss");
            go.SetActive(false);
            Rect bounds = BattleArena.PlayerBounds(Camera.main, 2.3f);
            go.transform.position = new Vector3(bounds.center.x, Mathf.Min(bounds.yMax, bounds.center.y + 3), 0);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;
            rb.useFullKinematicContacts = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var hull = go.AddComponent<BoxCollider2D>();
            hull.isTrigger = true;
            hull.size = new Vector2(owner.stageNumber == 2 ? 3.2f : 2.8f, 2.1f);
            var boss = go.AddComponent<CampaignBoss>();
            boss.stageNumber = owner.stageNumber;
            boss.maxHealth = owner.stageNumber == 1 ? 100 : owner.stageNumber == 2 ? 140 : 180;
            boss.warningDuration = owner.stageNumber == 1 ? 1.35f : 1.1f;
            boss.recoveryDuration = owner.stageNumber == 1 ? 3.1f : 2.5f;
            boss.bulletSpeed = owner.stageNumber == 1 ? 2.8f : owner.stageNumber == 2 ? 3.1f : 3.3f;
            boss.director = owner;
            boss.deactivateOnDefeat = false;
            EnemyShip source = owner.enemyPrefabs[2];
            boss.projectilePrefab = source.straightProjectile != null ? source.straightProjectile : source.homingProjectile;
            Sprite sprite = Resources.Load<Sprite>("CampaignArt/boss-stage" + owner.stageNumber);
            var sourceVisual = source.GetComponent<SpriteRenderer>();
            var visual = new GameObject("Boss hull artwork").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(go.transform, false);
            visual.sprite = sprite != null ? sprite : sourceVisual.sprite;
            visual.sharedMaterial = sourceVisual.sharedMaterial;
            visual.sortingOrder = 12;
            if (visual.sprite != null)
            {
                Vector2 size = visual.sprite.bounds.size;
                visual.transform.localScale = new Vector3(3.4f / Mathf.Max(.01f, size.x), 2.8f / Mathf.Max(.01f, size.y), 1);
            }
            visual.transform.localRotation = Quaternion.Euler(0, 0, 180);
            go.SetActive(true);
            return boss;
        }

        protected override void Awake()
        {
            base.Awake();
            body = GetComponent<Rigidbody2D>();
            visuals = GetComponentsInChildren<SpriteRenderer>();
            tint = stageNumber == 1 ? Color.white : stageNumber == 2 ? new Color(.85f, .92f, 1) : new Color(1, .8f, 1);
            home = transform.position;
            state = AttackState.Recovery;
            stateTime = 3;
        }

        protected override bool ContactDamageEnabled => state == AttackState.Attack &&
            (IntegratedGame.Active == null || !IntegratedGame.Active.EnemiesFrozen);

        void Update()
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running || game.EnemiesFrozen) return;
            float dt = Time.deltaTime;
            flash = Mathf.Max(0, flash - dt);
            foreach (var visual in visuals)
                if (visual != null) visual.color = flash > 0 ? new Color(1, .3f, .2f) : tint;
            clock += dt;
            stateTime -= dt;
            if (state == AttackState.Death)
            {
                float remaining = Mathf.Clamp01(stateTime / 1.5f);
                if (deathArtwork != null)
                {
                    deathArtwork.transform.localScale = Vector3.one * deathArtworkScale * Mathf.Lerp(.45f, 1.4f, 1 - remaining);
                    deathArtwork.color = new Color(1, 1, 1, remaining);
                }
                foreach (var visual in visuals)
                    if (visual != null) visual.color = new Color(tint.r, tint.g, tint.b, remaining);
                deathBurst -= dt;
                if (deathBurst <= 0)
                {
                    SimpleImpact.Spawn(transform.position + (Vector3)Random.insideUnitCircle * 1.3f, new Color(1, .5f, .18f), 1.5f);
                    deathBurst = .15f;
                }
                if (stateTime <= 0) { CompleteDefeat(); Destroy(gameObject); }
                return;
            }
            if (game.player == null) return;
            if (state == AttackState.Recovery)
            {
                if (stateTime <= 0) BeginWarning();
            }
            else if (state == AttackState.Warning)
            {
                if (stateTime <= 0) BeginAttack();
            }
            else
            {
                shotTimer -= dt;
                if (shotTimer <= 0) FireVolley();
                if (stateTime <= 0) Recover();
            }
        }

        void FixedUpdate()
        {
            var game = IntegratedGame.Active;
            if (body == null) return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
            if (IsDefeated || game == null || !game.Running || game.EnemiesFrozen) return;
            if (state == AttackState.Recovery)
            {
                Vector2 destination = home + new Vector2(Mathf.Sin(clock * .7f) * 1.4f, Mathf.Sin(clock * .5f) * .35f);
                body.MovePosition(Vector2.MoveTowards(body.position, destination, 2.3f * Time.fixedDeltaTime));
            }
            else if (state == AttackState.Attack && stageNumber == 1 && pattern == 2)
                body.MovePosition(Vector2.MoveTowards(body.position, dashEnd, dashSpeed * Time.fixedDeltaTime));
        }

        void BeginWarning()
        {
            pattern = (pattern + 1) % 4;
            var player = IntegratedGame.Active.player;
            Vector2 velocity = player.GetComponent<Rigidbody2D>() != null ? player.GetComponent<Rigidbody2D>().linearVelocity : Vector2.zero;
            bool predictive = (stageNumber == 2 && pattern == 2) || (stageNumber == 3 && pattern == 2);
            Rect bounds = BattleArena.PlayerBounds(Camera.main, 1.2f);
            aimPoint = (Vector2)player.transform.position + (predictive ? velocity * .65f : Vector2.zero);
            aimPoint = new Vector2(Mathf.Clamp(aimPoint.x, bounds.xMin, bounds.xMax), Mathf.Clamp(aimPoint.y, bounds.yMin, bounds.yMax));
            gapAngle = Angle(aimPoint - body.position);
            Vector2 dash = aimPoint - body.position;
            dashEnd = body.position + dash.normalized * Mathf.Min(7, dash.magnitude + 1);
            dashEnd = new Vector2(Mathf.Clamp(dashEnd.x, bounds.xMin + 1, bounds.xMax - 1), Mathf.Clamp(dashEnd.y, bounds.yMin + 1, bounds.yMax - 1));
            state = AttackState.Warning;
            stateTime = Mathf.Max(.9f, warningDuration - (PhaseTwo ? .15f : 0));
            warnings = new GameObject("Attack warning " + PatternName);
            Color color = new Color(1, .3f, .12f, .72f);
            if (stageNumber == 1 && pattern == 2) BossTelegraph.Line(warnings.transform, body.position, dashEnd, .35f, color);
            else if ((stageNumber == 2 || stageNumber == 3) && pattern == 2)
            {
                BossTelegraph.Circle(warnings.transform, aimPoint, 1, color);
                BossTelegraph.Line(warnings.transform, body.position, aimPoint, .1f, color);
            }
            else if (stageNumber == 2 && pattern == 3)
            {
                for (int i = 0; i < 4; i++) BossTelegraph.Line(warnings.transform, Edge(i), aimPoint, .14f, color);
            }
            else if ((stageNumber == 1 && pattern == 1) || (stageNumber == 2 && pattern == 0))
            {
                for (int i = -1; i <= 1; i++) BossTelegraph.Line(warnings.transform, body.position, body.position + Direction(gapAngle + i * 20) * 7, .1f, color);
            }
            else
            {
                BossTelegraph.Circle(warnings.transform, body.position, 2, color);
                bool ringPattern = (stageNumber == 1 && (pattern == 0 || pattern == 3)) ||
                    (stageNumber == 3 && (pattern == 1 || pattern == 3));
                if (ringPattern)
                    // Blue corridor shows the open wedge maintained by every ring.
                    BossTelegraph.Line(warnings.transform, body.position + Direction(gapAngle) * 1.6f,
                        body.position + Direction(gapAngle) * 7, .25f, new Color(.25f, .9f, 1, .55f));
            }
        }

        void BeginAttack()
        {
            ClearWarning();
            state = AttackState.Attack;
            stateTime = stageNumber == 1 ? (pattern == 2 ? .8f : 1.8f) : 2.7f;
            shotTimer = 0;
            volley = 0;
            if (stageNumber == 3 && pattern == 3)
            {
                var vision = FindAnyObjectByType<StageVision>();
                if (vision != null) vision.Boost(4);
            }
        }

        void FireVolley()
        {
            shotTimer = 10;
            float speed = bulletSpeed + (PhaseTwo ? .35f : 0);
            if (stageNumber == 1)
            {
                if (pattern == 0 && volley == 0) Ring(18, 0, speed);
                if (pattern == 1 && volley < 2) { Fan(body.position, gapAngle, 5, 16, speed); shotTimer = .7f; }
                if (pattern == 3 && volley < 3) { Ring(22, volley * 11, speed * (.8f + volley * .1f)); shotTimer = .6f; }
            }
            else if (stageNumber == 2)
            {
                if (pattern == 0 && volley < 3)
                {
                    Shoot(body.position + Vector2.left * 1.1f, gapAngle - 18, speed, true);
                    Shoot(body.position + Vector2.right * 1.1f, gapAngle + 18, speed, true);
                    shotTimer = .65f;
                }
                else if (pattern == 1 && volley < 15)
                {
                    Shoot(body.position, volley * 23, speed);
                    Shoot(body.position, volley * 23 + 180, speed);
                    shotTimer = .17f;
                }
                else if (pattern == 2 && volley < 3) { Fan(body.position, gapAngle, 3, 9, speed + .8f); shotTimer = .6f; }
                else if (pattern == 3 && volley < 3)
                {
                    // Sequential edge pairs leave half the perimeter clear on each volley.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 origin = Edge((volley + i * 2) % 4);
                        Shoot(origin, Angle(aimPoint - origin), speed * .85f, true);
                    }
                    shotTimer = .75f;
                }
            }
            else
            {
                if (pattern == 0 && volley < (PhaseTwo ? 18 : 14))
                {
                    int arms = PhaseTwo ? 3 : 2;
                    for (int i = 0; i < arms; i++) Shoot(body.position, volley * 24 + i * 360f / arms, speed);
                    shotTimer = PhaseTwo ? .135f : .18f;
                }
                else if (pattern == 1 && volley < 3) { Ring(PhaseTwo ? 30 : 24, volley * 13, speed); shotTimer = .7f; }
                else if (pattern == 2 && volley < (PhaseTwo ? 3 : 2)) { Fan(body.position, gapAngle, PhaseTwo ? 5 : 3, 11, speed + .6f); shotTimer = .7f; }
                else if (pattern == 3 && volley < 4) { Ring(PhaseTwo ? 26 : 20, volley * 14, speed * .75f); shotTimer = .65f; }
            }
            volley++;
        }

        void Ring(int count, float offset, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count + offset;
                // A fixed 60 degree escape corridor survives every expanding ring.
                if (Mathf.Abs(Mathf.DeltaAngle(angle, gapAngle)) < 30) continue;
                Shoot(body.position, angle, speed);
            }
        }

        void Fan(Vector2 origin, float angle, int count, float spread, float speed)
        {
            for (int i = 0; i < count; i++) Shoot(origin, angle + (i - (count - 1) * .5f) * spread, speed);
        }

        void Shoot(Vector2 origin, float angle, float speed, bool homing = false)
        {
            if (projectilePrefab == null) return;
            var player = IntegratedGame.Active != null ? IntegratedGame.Active.player : null;
            if (player == null) return;
            // Edge missiles and hull-origin bullets never materialize on a player's
            // actual hit surfaces. Keep at least .2 seconds of extra travel space.
            float safeDistance = 1.2f + speed * .2f;
            foreach (var hitbox in player.GetComponentsInChildren<Collider2D>())
                if (hitbox.enabled && (hitbox.ClosestPoint(origin) - origin).sqrMagnitude < safeDistance * safeDistance) return;
            EnemyProjectile bullet = Instantiate(projectilePrefab, origin, Quaternion.identity);
            bullet.speed = speed;
            bullet.rotationSpeed = homing ? 34 : 0;
            bullet.lifetime = homing ? 5 : 6;
            bullet.Initialize(null, Direction(angle), homing ? player.transform : null);
            var visual = bullet.GetComponent<SpriteRenderer>();
            Sprite art = Resources.Load<Sprite>(homing ? "CampaignArt/boss-missile" : "CampaignArt/boss-bullet");
            if (visual != null && art != null)
            {
                visual.sprite = art;
                // Preserve the artwork aspect ratio; only this boss clone gets a
                // matching small hit core so a thin bolt never has an invisible wide hitbox.
                visual.enabled = false;
                var child = new GameObject("Boss projectile artwork").AddComponent<SpriteRenderer>();
                child.transform.SetParent(bullet.transform, false);
                child.sprite = art;
                child.sharedMaterial = visual.sharedMaterial;
                child.sortingOrder = visual.sortingOrder;
                float worldScale = Mathf.Max(.01f, Mathf.Abs(bullet.transform.lossyScale.y));
                child.transform.localScale = Vector3.one * ((homing ? .9f : .8f) / (Mathf.Max(.01f, art.bounds.size.y) * worldScale));
                var circle = bullet.GetComponent<CircleCollider2D>();
                if (circle != null) circle.radius = (homing ? .17f : .06f) /
                    Mathf.Max(.01f, Mathf.Max(Mathf.Abs(bullet.transform.lossyScale.x), Mathf.Abs(bullet.transform.lossyScale.y)));
            }
            projectiles.Add(bullet);
            if (projectiles.Count > 160) projectiles.RemoveAll(p => p == null);
        }

        Vector2 Edge(int side)
        {
            Rect view = BattleArena.PlayerBounds(Camera.main, .9f);
            return side == 0 ? new Vector2(view.xMin, view.center.y) : side == 1 ? new Vector2(view.center.x, view.yMax) :
                side == 2 ? new Vector2(view.xMax, view.center.y) : new Vector2(view.center.x, view.yMin);
        }

        static float Angle(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        void Recover() { state = AttackState.Recovery; stateTime = Mathf.Max(1.6f, recoveryDuration - (PhaseTwo ? .7f : 0)); }
        void ClearWarning() { if (warnings != null) Destroy(warnings); warnings = null; }
        void ClearProjectiles() { foreach (var p in projectiles) if (p != null) p.Absorb(); projectiles.Clear(); }

        protected override void OnRamHit(int amount)
        {
            flash = .17f;
            SimpleImpact.Spawn(transform.position, new Color(.5f, .9f, 1), .7f);
            if (stageNumber != 3 || PhaseTwo || CurrentHealth <= 0 || CurrentHealth > MaxHealth * secondPhaseHealth) return;
            PhaseTwo = true;
            tint = new Color(1, .5f, .65f);
            ClearProjectiles();
            ClearWarning();
            state = AttackState.Recovery;
            stateTime = 3;
            SimpleImpact.Spawn(transform.position, new Color(.9f, .25f, 1), 4);
        }

        protected override void HandleDefeat()
        {
            ClearProjectiles();
            ClearWarning();
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            Sprite explosion = Resources.Load<Sprite>("CampaignArt/explosion");
            if (explosion != null)
            {
                deathArtwork = new GameObject("Boss destruction explosion").AddComponent<SpriteRenderer>();
                deathArtwork.transform.SetParent(transform, false);
                deathArtwork.sprite = explosion;
                deathArtwork.sortingOrder = 190;
                if (visuals.Length > 0) deathArtwork.sharedMaterial = visuals[0].sharedMaterial;
                deathArtworkScale = 4.2f / Mathf.Max(.01f, explosion.bounds.size.x);
                deathArtwork.transform.localScale = Vector3.one * deathArtworkScale * .45f;
            }
            state = AttackState.Death;
            stateTime = 1.5f;
            deathBurst = 0;
        }
        void OnDestroy() { ClearWarning(); ClearProjectiles(); }
    }
}
