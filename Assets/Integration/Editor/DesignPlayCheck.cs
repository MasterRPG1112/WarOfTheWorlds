#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Taeyang;
using O = UnityEngine.Object;

namespace Integration.Editor
{
    // Explicit Play Mode regression with isolated save data and accelerated progression.
    [InitializeOnLoad]
    public static class DesignPlayCheck
    {
        static Stack<IEnumerator> steps;
        static string report = "";
        static int errors, searchErrors, code;
        static double deadline;
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static DesignPlayCheck()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (!SessionState.GetBool("Design.Check", false)) return;
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                if (stack.Contains("UnityEditor.Search.SearchDatabase")) searchErrors++;
                else { errors++; report += "ERROR " + message + "\n" + stack + "\n"; }
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool("Design.Check", false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    DesignValidationMode.Prepare();
                    report = ""; errors = 0; searchErrors = 0;
                    steps = new Stack<IEnumerator>(); steps.Push(Check());
                    deadline = EditorApplication.timeSinceStartup + 240;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool("Design.Check", false);
                    File.WriteAllText("../design-play-validation.txt", report + "Runtime errors=" + errors + "; Search exceptions=" + searchErrors + "\n");
                    EditorApplication.Exit(code);
                }
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(IntegrationBuilder.ScenePath);
            SessionState.SetBool("Design.Check", true);
            EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (steps == null || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Design validation timeout");
                if (!steps.Peek().MoveNext())
                {
                    steps.Pop();
                    if (steps.Count == 0) { A(errors == 0, "No game runtime errors"); Finish(0); }
                    return;
                }
                if (steps.Peek().Current is IEnumerator nested) steps.Push(nested);
            }
            catch (Exception exception) { report += "FAIL " + exception + "\n"; Finish(1); }
        }
        static void Finish(int result)
        {
            code = result; steps = null; DesignValidationMode.Restore();
            Time.timeScale = 1; EditorApplication.isPlaying = false;
        }
        static void A(bool value, string name)
        {
            if (!value) throw new Exception(name);
            report += "PASS " + name + "\n";
            File.WriteAllText("../design-play-progress.txt", report);
        }
        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            double wall = EditorApplication.timeSinceStartup + Math.Max(6, seconds * 3);
            while (Time.time < end)
            {
                if (EditorApplication.timeSinceStartup > wall) throw new Exception("Simulation stalled");
                yield return null;
            }
        }
        static void Clear()
        {
            foreach (var item in O.FindObjectsByType<EnemyShip>(FindObjectsSortMode.None)) O.Destroy(item.gameObject);
            foreach (var item in O.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) O.Destroy(item.gameObject);
            foreach (var item in O.FindObjectsByType<IntegratedSkillEffect>(FindObjectsSortMode.None)) O.Destroy(item.gameObject);
            foreach (var item in O.FindObjectsByType<PatchPickup>(FindObjectsSortMode.None)) O.Destroy(item.gameObject);
        }
        static void Quiet(IntegratedGame game)
        {
            game.director.enabled = false;
            foreach (var spawner in O.FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None)) spawner.CancelInvoke();
            game.player.GetComponent<CampaignDrops>().dropChance = 0;
        }
        static EnemyShip Enemy(IntegratedGame game, int kind, Vector2 position, int health = 60)
        {
            var enemy = O.Instantiate(game.director.enemyPrefabs[kind], position, Quaternion.identity);
            enemy.enabled = false; enemy.maxHealth = health; enemy.ResetHealthForTesting();
            return enemy;
        }
        static EnemyProjectile Shot(IntegratedGame game, EnemyShip owner, Vector2 position, Vector2 direction, float speed = 5)
        {
            var shot = O.Instantiate(game.director.enemyPrefabs[0].straightProjectile, position, Quaternion.identity);
            shot.speed = speed; shot.Initialize(owner, direction, null);
            return shot;
        }
        static void State(StageDirector director, StageDirector.Phase phase)
        {
            typeof(StageDirector).GetField("<State>k__BackingField", Private).SetValue(director, phase);
        }
        static void SkipOpening()
        {
            var narrative = O.FindAnyObjectByType<CampaignNarrative>();
            if (narrative != null) narrative.FinishOpening();
        }
        static IEnumerator VerifyBossPatterns(CampaignBoss boss, IntegratedCombat player)
        {
            var names = new HashSet<string>();
            for (int pattern = 0; pattern < 4; pattern++)
            {
                Rect bounds = BattleArena.PlayerBounds(Camera.main, 1.5f);
                player.GetComponent<Rigidbody2D>().position = new Vector2(bounds.center.x, bounds.yMin + 1);
                Physics2D.SyncTransforms();
                typeof(CampaignBoss).GetMethod("BeginWarning", Private).Invoke(boss, null);
                names.Add(boss.PatternName);
                A(boss.IsWarning, "Boss pattern has an explicit warning " + boss.StageNumber + "/" + pattern);
                typeof(CampaignBoss).GetMethod("BeginAttack", Private).Invoke(boss, null);
                typeof(CampaignBoss).GetMethod("FireVolley", Private).Invoke(boss, null);
                var shots = O.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
                if (!(boss.StageNumber == 1 && pattern == 2)) A(shots.Length > 0, "Boss dedicated barrage creates projectiles " + boss.StageNumber + "/" + pattern);
                bool fixedHeadings = true;
                foreach (var shot in shots) fixedHeadings &= typeof(EnemyProjectile).GetField("target", Private).GetValue(shot) == null && shot.rotationSpeed == 0;
                A(fixedHeadings, "Boss barrage has no homing target " + boss.StageNumber + "/" + pattern);
                typeof(CampaignBoss).GetMethod("ClearProjectiles", Private).Invoke(boss, null);
                yield return null;
            }
            A(names.Count == 4, "Boss has four distinct dedicated attack patterns " + boss.StageNumber);
            typeof(CampaignBoss).GetMethod("Recover", Private).Invoke(boss, null);
        }
        static IEnumerator Check()
        {
            yield return null; yield return null;
            var game = IntegratedGame.Active;
            var narrative = game.GetComponent<CampaignNarrative>();
            A(narrative != null && narrative.Opening && !game.Running, "New campaign opens with a paused skippable story");
            SkipOpening(); yield return null;
            var player = game.player;
            Quiet(game);
            A(game.Running && Time.timeScale == 1, "Skipping opening starts the existing campaign");
            A(player.Health == 8 && player.ownedMask == 0, "New campaign has shared HP8 and no free skills");
            A(!game.director.noBossCampaign && !game.director.allowMissingBossTestSkip, "Production campaign requires real bosses");
            var fixedMove = player.movement.GetType().GetMethod("FixedUpdate", Private);
            var body = player.GetComponent<Rigidbody2D>();
            player.movement.ApplyMovementInput(Vector2.right); fixedMove.Invoke(player.movement, null);
            A(body.linearVelocity.magnitude > 0 && body.linearVelocity.magnitude < 5, "Movement begins with fixed-step acceleration");
            for (int i = 0; i < 15; i++) fixedMove.Invoke(player.movement, null);
            A(Mathf.Abs(body.linearVelocity.magnitude - 5) < .01f && Vector2.Dot(player.movement.FacingDirection, Vector2.right) > .99f, "Movement reaches requested speed and direction");
            player.movement.ApplyMovementInput(Vector2.zero);
            for (int i = 0; i < 15; i++) fixedMove.Invoke(player.movement, null);
            A(body.linearVelocity.sqrMagnitude < .001f, "Releasing movement stops smoothly");
            game.TogglePause(); game.OpenShop();
            A(game.ShopAccessible && !game.BetweenStages && game.shop.shopMenu.activeInHierarchy && Time.timeScale == 0,
                "Existing shop opens while paused before any boss clear");
            var cheats = game.GetComponent<CampaignCheats>();
            A(cheats.CheatModeAvailable && !cheats.Allowed && cheats.ToggleDevelopmentMode(), "Development cheats default off and can be enabled explicitly");
            int beforeMoney = game.shop.coin;
            A(cheats.ExecuteCheat(3) && game.shop.coin == beforeMoney + cheats.moneyAmount, "Money cheat works inside the paused shop");
            A(!cheats.ExecuteCheat(2), "Defeat-enemies cheat cannot bypass paused reward rules");
            game.shop.coin = 2000;
            A(game.shop.TryBuyPart(1) && game.shop.IsPartEquipped(1) && player.movement.speed == 7, "Pause-shop purchase immediately equips and applies turbo");
            A(game.shop.TogglePart(1) && game.shop.part2 == 1 && player.movement.speed == 5, "Pause-shop unequip removes turbo without losing ownership");
            A(!cheats.ToggleDevelopmentMode() && !game.RankedRun && IntegrationSaveStore.Data.checkpoint.practice, "Disabling cheats cannot restore ranking eligibility");
            game.shop.part2 = 0; game.shop.equippedPartsMask = 0; game.shop.coin = 0;
            game.CloseShop(); A(game.pausePanel.activeInHierarchy && !game.Running, "Closing pause shop returns to pause controls");
            game.TogglePause(); A(game.Running, "Pause shop returns to gameplay");
            for (int kind = 0; kind < 4; kind++) A(!player.TryUsePatch(kind), "Zero-stock skill blocked " + kind);
            player.Fire(); player.TryBomb(); player.TrySkill(); yield return Wait(.2f);
            A(O.FindObjectsByType<PlayerShot>(FindObjectsSortMode.None).Length == 0 && player.shotsFired == 0, "Missile-lure game has no free player gun");

            var legacy = JsonUtility.FromJson<SaveFile>("{\"version\":1,\"checkpoint\":{\"stage\":2,\"coreHp\":99,\"selectedSkill\":0,\"pull\":2}}");
            typeof(IntegrationSaveStore).GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { legacy });
            A(legacy.version == 4 && legacy.checkpoint.coreHp == 8 && legacy.checkpoint.pull == 2 && legacy.checkpoint.patchSlot0 == -1, "Legacy checkpoint preserves inventory without requiring equipment");
            var owner = Enemy(game, 0, new Vector2(-6, 2));
            var target = Enemy(game, 1, new Vector2(-2, 2), game.director.enemyPrefabs[0].straightProjectile.enemyDamage);
            int money = game.shop.coin;
            Shot(game, owner, owner.transform.position, Vector2.right);
            yield return Wait(1.1f);
            A(owner.CurrentHealth == 60 && target == null, "Lured missile ignores its owner and kills another enemy");
            A(game.Kills == 1 && game.shop.coin > money, "Lured kill rewards score and currency once");
            Clear(); yield return null;
            for (int kind = 0; kind < 5; kind++)
            {
                var enemy = Enemy(game, kind, new Vector2(4, 4)); enemy.stageOnePatterns = true;
                typeof(EnemyShip).GetField("player", Private).SetValue(enemy, player.transform);
                typeof(EnemyShip).GetMethod("FirePattern", Private).Invoke(enemy, null);
                A(O.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length > 0, "Existing enemy pattern creates projectiles " + kind);
                Clear(); yield return null;
            }

            var drops = player.GetComponent<CampaignDrops>();
            float[] weights = drops.weights;
            drops.weights = new float[7]; A(drops.ChooseKind(.5f) == -1, "Empty weighted drop table creates no item");
            drops.weights[6] = 1; A(drops.ChooseKind(0) == 6 && drops.ChooseKind(1) == 6, "Weighted selection includes both normalized boundaries");
            drops.dropChance = 1;
            var dropEnemy = Enemy(game, 0, new Vector2(4, 4), 1);
            dropEnemy.TakeDamage(1, Vector2.up);
            var deathDrops = O.FindObjectsByType<PatchPickup>(FindObjectsSortMode.None);
            A(deathDrops.Length == 1 && deathDrops[0].kind == 6 && Vector2.Distance(deathDrops[0].transform.position, new Vector2(4, 4)) < .01f,
                "Actual enemy defeat creates one weighted item at its death position");
            drops.dropChance = 0; Clear(); yield return null;
            drops.weights = weights;
            for (int kind = 0; kind < 4; kind++)
            {
                int before = player.PatchCount(kind);
                drops.SpawnPickup(kind, player.transform.position + Vector3.right * .2f);
                yield return Wait(.12f);
                A(player.PatchCount(kind) == before + 1, "Approaching death-position pickup grants matching stock " + kind);
                player.GrantPatch(kind, 2);
            }
            A(player.equippedPatches[0] < 0 && player.equippedPatches[1] < 0, "All four skill stocks exist with no slot assignment");
            player.data.Invin = true;
            target = Enemy(game, 1, (Vector2)player.transform.position + Vector2.up * 3);
            var missile = Shot(game, null, (Vector2)player.transform.position + Vector2.up, Vector2.down, 0);
            int damage = missile.enemyDamage, stock = player.PatchCount(2);
            A(player.TryUsePatch(2) && player.PatchCount(2) == stock - 1, "R skill consumes one stock without equipment");
            yield return Wait(.08f); A(missile != null && missile.Reflected, "Reflection changes the existing missile");
            missile.speed = 6; yield return Wait(.6f);
            A(target.CurrentHealth == 60 - damage, "Reflected missile causes actual enemy damage");
            Clear(); yield return null;
            stock = player.PatchCount(0);
            A(player.TryUsePatch(0) && !player.TryUsePatch(0) && player.PatchCount(0) == stock - 1, "B cooldown rejects repeated activation without extra consumption");
            missile = Shot(game, null, (Vector2)player.transform.position + Vector2.right, Vector2.up, 0);
            yield return Wait(.12f); A(missile == null, "Black hole absorbs real missiles");
            Clear(); yield return null;
            A(player.TryUsePatch(1) && player.PatchCooldown(0) > 0 && player.PatchCooldown(1) > 0, "V cooldown is independent of B");
            missile = Shot(game, null, (Vector2)player.transform.position + Vector2.left, Vector2.up, 0);
            yield return Wait(.1f); A(missile.EffectiveSpeedFactor < 1, "Slow field modifies projectile speed");
            foreach (var effect in O.FindObjectsByType<IntegratedSkillEffect>(FindObjectsSortMode.None)) O.Destroy(effect.gameObject);
            yield return null; A(missile.EffectiveSpeedFactor == 1, "Removing a slow field restores projectile speed");
            Clear(); yield return null;
            float cooldown = player.PatchCooldown(1); game.TogglePause();
            double wall = EditorApplication.timeSinceStartup + .25;
            while (EditorApplication.timeSinceStartup < wall) yield return null;
            A(Mathf.Abs(player.PatchCooldown(1) - cooldown) < .01f, "Pause freezes skill cooldowns"); game.TogglePause();
            target = Enemy(game, 0, new Vector2(6, 0)); target.approachPlayer = true; target.moveSpeed = 2; target.enabled = true;
            missile = Shot(game, null, new Vector2(-6, 2), Vector2.up);
            A(player.TryUsePatch(3) && game.EnemiesFrozen && Time.timeScale == 1, "T freezes enemies without pausing player simulation");
            Vector2 enemyPosition = target.transform.position, missilePosition = missile.transform.position;
            yield return Wait(.3f);
            A(Vector2.Distance(enemyPosition, target.transform.position) < .01f && Vector2.Distance(missilePosition, missile.transform.position) < .01f, "Time stop halts enemy and missile physics");
            yield return Wait(3); A(!game.EnemiesFrozen, "Time stop expires"); Clear(); yield return null;

            player.data.Invin = false; player.data.PlayerHp = 3;
            var repair = drops.SpawnPickup(4, player.transform.position + Vector3.right * .2f);
            yield return Wait(.15f); A(player.Health == 5 && repair == null, "Repair drop heals and is removed once");
            drops.SpawnPickup(5, player.transform.position); yield return Wait(.1f);
            player.Hit(null, 1); A(player.Health == 5 && player.InvulnerabilityRemaining > 0, "Invulnerability drop blocks damage for its configured duration");
            yield return Wait(3.1f);
            player.data.HaveShield = 0; drops.SpawnPickup(6, player.transform.position); yield return Wait(.1f);
            player.Hit(null, 1); A(player.Health == 5 && player.ShieldCount == 0, "Shield drop absorbs one hit and consumes one charge");
            yield return Wait(.9f); player.data.PlayerHp = 1; game.shop.part1 = 1; game.shop.equippedPartsMask |= 1;
            player.Hit(null, 1); A(player.Health == 4 && game.shop.part1 == 0, "Emergency part prevents lethal damage exactly once");
            yield return Wait(1.6f); player.Hit(null, 1); A(player.Health == 3 && game.shop.part1 == 0, "Consumed emergency part does not trigger again");
            player.Heal(99);

            var bossObject = new GameObject("Ram contract regression boss");
            bossObject.transform.position = new Vector3(5, 4, 0);
            var bossCollider = bossObject.AddComponent<CircleCollider2D>(); bossCollider.isTrigger = true; bossCollider.radius = .8f;
            var bossBody = bossObject.AddComponent<Rigidbody2D>(); bossBody.bodyType = RigidbodyType2D.Kinematic; bossBody.useFullKinematicContacts = true;
            var adapter = bossObject.AddComponent<IntegratedBossTarget>();
            State(game.director, StageDirector.Phase.AwaitingBoss); game.director.AttachBoss(bossObject);
            int bossHealth = adapter.CurrentHealth;
            Shot(game, null, (Vector2)bossObject.transform.position + Vector2.right * 3, Vector2.left);
            yield return Wait(.8f);
            A(adapter.CurrentHealth == bossHealth, "Ordinary enemy missile cannot damage a boss hull");
            player.data.Invin = true; body.position = bossObject.transform.position; body.linearVelocity = Vector2.zero;
            yield return Wait(.25f);
            A(adapter.CurrentHealth < bossHealth, "Physical player hull contact damages the boss adapter");
            adapter.TakeRamDamage(999);
            A(game.director.State == StageDirector.Phase.Clear, "Boss defeat reaches existing stage-clear interface");
            State(game.director, StageDirector.Phase.Survival);
            O.Destroy(bossObject); Clear(); yield return null;

            // Exercise the real shop/intermission API, then round-trip stage-start snapshots.
            State(game.director, StageDirector.Phase.Clear);
            typeof(IntegratedGame).GetMethod("BeginIntermission", Private).Invoke(game, null);
            A(game.shop.shopMenu.activeInHierarchy, "Intermission activates the existing shop hierarchy");
            game.shop.coin = 2000; int blackStock = player.PatchCount(0);
            A(game.shop.TryBuySkill(0) && game.shop.coin == 1400 && player.PatchCount(0) == blackStock + 1, "Existing skill purchase deducts its price once");
            game.shop.part2 = 0; game.shop.partLicense = 1;
            A(game.shop.TryBuyPart(1) && game.shop.coin == 400 && game.shop.part2 == 1, "Engine purchase records ownership and price");
            A(!game.shop.TryBuyPart(1) && game.shop.coin == 400, "Duplicate part purchase cannot charge currency");
            A(!game.shop.TryBuySkill(3) && game.shop.coin == 400, "Insufficient funds cannot grant stock or spend currency");
            A(game.shop.IsPartEquipped(1) && game.shop.TogglePart(1) && !game.shop.IsPartEquipped(1) && game.shop.part2 == 1,
                "Owned engine can be unequipped without losing ownership or charging money");
            A(game.shop.TogglePart(1) && game.shop.IsPartEquipped(1) && game.shop.coin == 400, "Engine can be equipped again without a purchase");
            game.ContinueAfterShop(); Quiet(game); yield return null;
            A(game.director.stageNumber == 2 && player.movement.speed == 7, "Shop continuation enters stage2 with functional turbo engine");
            for (int stage = 2; stage <= 3; stage++)
            {
                game.director.stageNumber = stage;
                player.data.PlayerHp = 6; player.data.HaveShield = 2;
                game.shop.coin = 4321; game.shop.part1 = game.shop.part2 = game.shop.part3 = 1; game.shop.equippedPartsMask = 5;
                game.shop.pull = 3; game.shop.slow = 4; game.shop.reflex = 5; game.shop.stop = 6;
                game.score.score = 9876; game.SaveCheckpoint(); IntegrationSaveStore.Reload();
                var checkpoint = IntegrationSaveStore.Data.checkpoint;
                A(checkpoint.stage == stage && checkpoint.coreHp == 6 && checkpoint.money == 4321 && checkpoint.shields == 2, "Stage-start snapshot saves current stage and protection " + stage);
                game.shop.coin = 0; game.shop.part2 = 0; game.shop.pull = 0; player.data.PlayerHp = 0;
                game.End(false);
                A(IntegrationSaveStore.HasCheckpoint && IntegrationSaveStore.Data.checkpoint.stage == stage, "Game over retains current stage checkpoint " + stage);
                game.Restart(); yield return null; yield return null;
                game = IntegratedGame.Active; player = game.player; Quiet(game);
                A(game.Running && game.director.stageNumber == stage && player.Health == 6 && game.shop.coin == 4321 && game.shop.part2 == 1 && game.shop.part3 == 1 && player.PatchCount(0) == 3 && player.PatchCount(3) == 6 && player.ShieldCount == 2 && game.score.score == 9876, "Actual retry restores stage, parts, stock, score and money " + stage);
                A(game.shop.EquippedPartsMask == 5 && !game.shop.IsPartEquipped(1) && game.shop.IsPartEquipped(2), "Retry preserves owned versus equipped parts " + stage);
                game.OpenShop();
                A(game.shop.TogglePart(2) && player.RamDamage == 20, "Unequipping owned armor removes its ram bonus " + stage);
                A(game.shop.TogglePart(2) && player.RamDamage == 30, "Equipping armor restores its ram bonus " + stage);
                game.CloseShop(); game.TogglePause();
            }

            // Start a fresh three-stage run. The harness supplies invulnerability and
            // physical player placement at boss hulls; all boss damage still uses contact.
            IntegrationSaveStore.ClearCheckpoint(); game.Restart(); yield return null; yield return null; SkipOpening(); yield return null;
            game = IntegratedGame.Active; player = game.player;
            player.data.Invin = true; game.player.GetComponent<CampaignDrops>().dropChance = 0;
            int stages = 1; bool phaseTwo = false; var seenBosses = new HashSet<int>();
            double end = EditorApplication.timeSinceStartup + 150;
            while (!game.Ended)
            {
                if (EditorApplication.timeSinceStartup > end) throw new Exception("Campaign progression timeout");
                player.data.Invin = true; player.data.PlayerHp = 8;
                if (game.BetweenStages)
                {
                    A(game.shop.shopMenu.activeSelf && Time.timeScale == 0, "Real boss clear opens shop " + game.director.stageNumber);
                    game.ContinueAfterShop(); stages++;
                }
                var boss = O.FindAnyObjectByType<CampaignBoss>();
                if (boss != null && !boss.IsDefeated)
                {
                    if (seenBosses.Add(boss.StageNumber)) yield return VerifyBossPatterns(boss, player);
                    phaseTwo |= boss.StageNumber == 3 && boss.PhaseTwo;
                    player.GetComponent<Rigidbody2D>().position = boss.transform.position;
                }
                Time.timeScale = 20; yield return null;
            }
            A(game.Won && game.director.stageNumber == 3 && stages == 3 && seenBosses.Count == 3, "Three survival stages and real ram-only bosses reach ending");
            A(phaseTwo, "Final boss enters strengthened phase2 before defeat");
            A(O.FindObjectsByType<PlayerShot>(FindObjectsSortMode.None).Length == 0, "Campaign never creates a basic player gun");
            A(game.SubmitVictory("Regression") && !game.SubmitVictory("Duplicate"), "Victory score submits exactly once after ID entry");
            IntegrationSaveStore.Reload();
            A(IntegrationSaveStore.Data.ranks.Count > 0 && IntegrationSaveStore.Data.ranks.Count <= 5 && IntegrationSaveStore.Data.ranks[0].playerName == "Regression", "Victory ranking persists in isolated save and keeps top five");
            report += "LIMIT: accelerated campaign supplies invulnerability and player placement. No human input, visual, audio or dodgeability review is implied.\n";
        }
    }
}
#endif
