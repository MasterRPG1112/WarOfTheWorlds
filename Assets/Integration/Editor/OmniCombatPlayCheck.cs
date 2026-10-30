#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Taeyang;
using UObject = UnityEngine.Object;

namespace Integration.Editor
{
    // Drives the same movement/missile/spawn code used by gameplay. No OS-input claims.
    [InitializeOnLoad]
    public static class OmniCombatPlayCheck
    {
        const string Flag = "Integration.OmniCheck";
        static IEnumerator checks;
        static string report = "";
        static int errors, searchErrors, exitCode;
        static double deadline;
        static bool isolated;

        static OmniCombatPlayCheck()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Flag, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    report = ""; errors = 0; searchErrors = 0; exitCode = 0;
                    deadline = EditorApplication.timeSinceStartup + 180;
                    IntegrationSaveStore.ResetForTests(); isolated = true;
                    checks = Check();
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    if (checks != null) { report += "FAIL: Play Mode ended unexpectedly.\n"; exitCode = 1; checks = null; }
                    Restore(); SessionState.SetBool(Flag, false);
                    File.WriteAllText("../omni-combat-play-validation.txt", report +
                        "Runtime errors=" + errors + "; Editor Search exceptions=" + searchErrors + "\n");
                    EditorApplication.Exit(exitCode);
                }
            };
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (!SessionState.GetBool(Flag, false) ||
                    (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
                if (stack.Contains("UnityEditor.Search.SearchDatabase")) searchErrors++;
                else { errors++; report += "ERROR: " + message + "\n" + stack + "\n"; }
            };
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(IntegrationBuilder.ScenePath);
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (checks == null || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Omnidirectional combat check timeout");
                if (checks.MoveNext()) return;
                Assert(errors == 0, "Zero game runtime errors in omnidirectional combat");
                exitCode = 0; Finish();
            }
            catch (Exception exception) { report += "FAIL: " + exception + "\n"; exitCode = 1; Finish(); }
        }
        static void Restore() { if (isolated) { IntegrationSaveStore.RestoreAfterTests(); isolated = false; } }
        static void Finish() { checks = null; Restore(); Time.timeScale = 1; EditorApplication.isPlaying = false; }
        static void Assert(bool value, string label)
        {
            if (!value) throw new Exception(label);
            report += "PASS: " + label + "\n";
            File.WriteAllText("../omni-combat-play-progress.txt", report);
            Debug.Log("OMNI_COMBAT_CHECK " + label);
        }
        static T[] All<T>() where T : UObject => UObject.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        static void ClearCombat()
        {
            foreach (var enemy in All<EnemyShip>()) UObject.Destroy(enemy.gameObject);
            foreach (var shot in All<PlayerShot>()) UObject.Destroy(shot.gameObject);
            foreach (var shot in All<EnemyProjectile>()) UObject.Destroy(shot.gameObject);
            foreach (var obstacle in All<StageObstacle>()) UObject.Destroy(obstacle.gameObject);
        }
        static EnemyShip Enemy(StageDirector director, int kind, Vector2 position, int health = 120)
        {
            var enemy = UObject.Instantiate(director.enemyPrefabs[kind], position, Quaternion.identity);
            enemy.approachPlayer = false; enemy.moveSpeed = 0; enemy.driftWidth = 0; enemy.fireInterval = 100;
            enemy.maxHealth = health; enemy.ResetHealthForTesting();
            return enemy;
        }
        static PlayerShot Missile(IntegratedCombat player, Vector2 position, Vector2 heading)
        {
            var go = UObject.Instantiate(player.bulletPrefab, position, Quaternion.identity);
            go.tag = "Untagged";
            var body = go.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            foreach (var collider in go.GetComponents<Collider2D>()) collider.isTrigger = true;
            var shot = go.AddComponent<PlayerShot>(); shot.damage = 12; shot.Initialize(heading);
            return shot;
        }
        static bool InSector(Vector2 position, Rect view, int sector)
        {
            Vector2 outward = BattleArena.Direction(sector);
            return (Mathf.Abs(outward.x) < .01f || (outward.x > 0 ? position.x > view.xMax : position.x < view.xMin)) &&
                   (Mathf.Abs(outward.y) < .01f || (outward.y > 0 ? position.y > view.yMax : position.y < view.yMin));
        }

        static IEnumerator Check()
        {
            yield return null;
            var game = UObject.FindFirstObjectByType<IntegratedGame>();
            Assert(game != null && game.director.enemyPrefabs.Length == 5, "Integrated scene retains five enemy prefabs");
            var player = game.player; var movement = player.movement; var body = player.GetComponent<Rigidbody2D>();
            foreach (var spawner in All<ItemSpawner>()) { spawner.CancelInvoke(); spawner.enabled = false; }
            game.SelectSkill(0); game.director.enabled = false; movement.enabled = false; player.enabled = false;
            player.data.Invin = true; player.data.PlayerHp = 8; player.core.playerHp = 8;
            Time.timeScale = 1; body.position = Vector2.zero; body.linearVelocity = Vector2.zero;
            yield return null;
            var director = game.director;
            Rect view = BattleArena.ViewBounds();
            Assert(Camera.main.orthographicSize >= 8 && view.height >= 16 && view.width > 20,
                "Expanded camera exposes a larger combat arena");
            Assert(director.perimeterSpawns && director.noBossCampaign, "Integrated campaign enables perimeter spawning and excludes bosses");

            // Shared movement method updates both heading and rigidbody; no injected OS key events.
            movement.speed = 5;
            for (int direction = 0; direction < 8; direction++)
            {
                Vector2 expected = BattleArena.Direction(direction);
                body.position = Vector2.zero;
                movement.ApplyMovementInput(expected);
                Assert(Vector2.Dot(movement.FacingDirection, expected) > .999f &&
                       Vector2.Dot((Vector2)movement.transform.up, expected) > .999f,
                    "Player visual and facing agree with direction " + direction);
                Assert(Mathf.Abs(body.linearVelocity.magnitude - movement.speed) < .01f,
                    "Cardinal/diagonal movement has normalized speed " + direction);
                float angle = body.rotation;
                movement.ApplyMovementInput(Vector2.zero);
                Assert(Vector2.Dot(movement.FacingDirection, expected) > .999f &&
                       Mathf.Abs(Mathf.DeltaAngle(body.rotation, angle)) < .01f && body.linearVelocity.sqrMagnitude < .0001f,
                    "Stopping preserves the last facing direction " + direction);
                body.position = new Vector2(direction < 4 ? view.xMax + 3 : view.xMin - 3,
                    direction % 2 == 0 ? view.yMax + 3 : view.yMin - 3);
                movement.ApplyMovementInput(expected);
                Rect safe = BattleArena.PlayerBounds();
                Assert(body.position.x >= safe.xMin && body.position.x <= safe.xMax &&
                       body.position.y >= safe.yMin && body.position.y <= safe.yMax,
                    "Rotated player stays within expanded movement bounds " + direction);
                movement.ApplyMovementInput(Vector2.zero);
            }
            body.position = Vector2.zero; movement.ApplyMovementInput(Vector2.up);
            double until = EditorApplication.timeSinceStartup + .25;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(body.position.y > .5f, "Shared movement input produces actual physics movement");
            movement.ApplyMovementInput(Vector2.zero); body.position = Vector2.zero;

            // Inspect actual formations immediately at their spawn callback, before physics moves them.
            Assert(typeof(StageDirector).GetMethod("SpawnFormation", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "Campaign formation entry point is available for deterministic verification");
            MethodInfo spawn = typeof(StageDirector).GetMethod("SpawnFormation", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < 8; index++)
            {
                ClearCombat(); yield return null;
                int expected = director.SpawnTicks % 8; int observed = 0; EnemyShip ship = null;
                Action<EnemyShip, int> onSpawn = (enemy, sector) =>
                {
                    if (sector != expected || view.Contains(enemy.transform.position) ||
                        !InSector(enemy.transform.position, view, sector) || !enemy.approachPlayer)
                        throw new Exception("Invalid off-screen formation sector " + expected);
                    observed++; ship = enemy;
                };
                director.EnemySpawned += onSpawn;
                try { spawn.Invoke(director, null); }
                finally { director.EnemySpawned -= onSpawn; }
                Assert(observed > 0 && director.LastSpawnDirection == expected,
                    "Actual formation starts beyond viewport in sector " + expected);
                float distance = Vector2.Distance(ship.transform.position, player.transform.position);
                float approachStarted = Time.time;
                until = EditorApplication.timeSinceStartup + 5;
                while (ship != null && Time.time - approachStarted < .8f && EditorApplication.timeSinceStartup < until)
                    yield return null;
                float approachElapsed = Time.time - approachStarted;
                float finalDistance = ship != null ? Vector2.Distance(ship.transform.position, player.transform.position) : float.NaN;
                string approachMetrics = "sector=" + expected + ", distance=" + distance.ToString("F3") +
                    " -> " + finalDistance.ToString("F3") + ", simSeconds=" + approachElapsed.ToString("F3") +
                    ", moveSpeed=" + (ship != null ? ship.moveSpeed.ToString("F3") : "destroyed") +
                    ", approach=" + (ship != null && ship.approachPlayer) + ", timeScale=" + Time.timeScale.ToString("F2");
                report += "OBSERVED: enemy approach " + approachMetrics + "\n";
                Assert(ship != null && approachElapsed >= .8f && finalDistance < distance - .2f,
                    "Spawned enemy approaches player from sector " + expected + " (" + approachMetrics + ")");
            }
            ClearCombat(); yield return null;

            // A player against the top edge must not leave a Heavy parked outside.
            body.position = new Vector2(view.center.x, BattleArena.PlayerBounds(Camera.main, 1.6f).yMax);
            var heavyPrefab = director.enemyPrefabs.Single(enemy => enemy.kind == EnemyShip.EnemyKind.Heavy);
            var edgeHeavy = UObject.Instantiate(heavyPrefab, BattleArena.SpawnPosition(0), Quaternion.identity);
            edgeHeavy.approachPlayer = true; edgeHeavy.stageOnePatterns = true; edgeHeavy.moveSpeed = 5;
            float edgeStarted = Time.time; until = EditorApplication.timeSinceStartup + 5;
            while (Time.time - edgeStarted < 1.5f && EditorApplication.timeSinceStartup < until) yield return null;
            Assert(Time.time - edgeStarted >= 1.5f && edgeHeavy != null &&
                   BattleArena.PlayerBounds(Camera.main, .75f).Contains(edgeHeavy.transform.position),
                "Heavy enters the visible arena instead of holding range outside an edge-bound player");
            body.position = Vector2.zero; movement.ApplyMovementInput(Vector2.zero);
            ClearCombat(); yield return null;

            // One guided missile takes the nearest target, bends, and deals damage through physics.
            var near = Enemy(director, 0, new Vector2(-5, 0));
            var far = Enemy(director, 0, new Vector2(20, 0));
            var missile = Missile(player, Vector2.zero, Vector2.up);
            Assert(missile.CurrentTarget == near, "Missile initially acquires the nearest live active enemy");
            float highestY = 0; bool turned = false;
            until = EditorApplication.timeSinceStartup + 4;
            while (missile != null && near.CurrentHealth == 120 && EditorApplication.timeSinceStartup < until)
            {
                highestY = Mathf.Max(highestY, missile.transform.position.y);
                turned |= Vector2.Dot(missile.Heading, Vector2.up) < .9f;
                yield return null;
            }
            Assert(turned && highestY > .2f, "Missile follows a visibly curved path toward a lateral target");
            Assert(near.CurrentHealth == 108 && missile == null, "Real missile collision applies exactly one 12-point hit");
            Assert(far.CurrentHealth == 120, "Missile damage leaves the farther non-target enemy unchanged");
            ClearCombat(); yield return null;

            near = Enemy(director, 0, new Vector2(-5, 0)); far = Enemy(director, 0, new Vector2(20, 0));
            missile = Missile(player, Vector2.zero, Vector2.up);
            Assert(missile.CurrentTarget == near, "Retarget scenario starts with the nearest target");
            UObject.Destroy(near.gameObject); yield return null;
            until = EditorApplication.timeSinceStartup + .4;
            while (missile != null && missile.CurrentTarget != far && EditorApplication.timeSinceStartup < until) yield return null;
            Assert(missile != null && missile.CurrentTarget == far, "Destroyed target is replaced by another active enemy");
            far.gameObject.SetActive(false);
            until = EditorApplication.timeSinceStartup + .4;
            while (missile != null && missile.CurrentTarget != null && EditorApplication.timeSinceStartup < until) yield return null;
            Assert(missile != null && missile.CurrentTarget == null, "Inactive target is released without a stale reference");
            var replacement = Enemy(director, 0, new Vector2(0, 20));
            until = EditorApplication.timeSinceStartup + .4;
            while (missile != null && missile.CurrentTarget != replacement && EditorApplication.timeSinceStartup < until) yield return null;
            Assert(missile != null && missile.CurrentTarget == replacement, "An unlocked missile acquires a new enemy that appears later");
            UObject.Destroy(replacement.gameObject); yield return null;
            until = EditorApplication.timeSinceStartup + .4;
            while (missile != null && missile.CurrentTarget != null && EditorApplication.timeSinceStartup < until) yield return null;
            Assert(missile != null && missile.CurrentTarget == null, "Final target loss switches the missile to coasting");
            Vector2 coastHeading = missile.Heading, coastStart = missile.transform.position;
            until = EditorApplication.timeSinceStartup + .3;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(missile != null && Vector2.Dot(missile.Heading, coastHeading) > .999f &&
                   Vector2.Distance(missile.transform.position, coastStart) > 1,
                "No-target missile preserves heading and continues moving");
            until = EditorApplication.timeSinceStartup + 7;
            while (missile != null && EditorApplication.timeSinceStartup < until) yield return null;
            Assert(missile == null, "No-target missile expires after its finite lifetime");
            ClearCombat(); yield return null;

            missile = Missile(player, Vector2.zero, Vector2.up);
            until = EditorApplication.timeSinceStartup + .1;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            game.TogglePause(); Vector2 pausedPosition = missile.transform.position;
            until = EditorApplication.timeSinceStartup + .25;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(missile != null && Vector2.Distance(missile.transform.position, pausedPosition) < .001f,
                "Pausing freezes guided missile physics");
            game.TogglePause(); ClearCombat(); yield return null;

            // The real auto-fire adapter, target scan, sweep/trigger and reward event run together.
            movement.ApplyMovementInput(Vector2.up); movement.ApplyMovementInput(Vector2.zero); body.position = Vector2.zero;
            player.fireInterval = .18f; player.enabled = true;
            int previousKills = game.Kills, previousScore = game.score.score, previousMoney = game.shop.coin, expectedScore = 0;
            for (int kind = 0; kind < 5; kind++)
            {
                foreach (var shot in All<PlayerShot>()) UObject.Destroy(shot.gameObject);
                yield return null;
                var enemy = Enemy(director, kind, new Vector2(3, 2), director.enemyPrefabs[kind].maxHealth);
                expectedScore += enemy.scoreValue;
                until = EditorApplication.timeSinceStartup + 8;
                while (enemy != null && EditorApplication.timeSinceStartup < until) yield return null;
                Assert(enemy == null, "Automatic guided attack physically defeats archetype " + kind);
            }
            player.enabled = false;
            Assert(game.Kills == previousKills + 5 && game.score.score == previousScore + expectedScore &&
                   game.shop.coin == previousMoney + expectedScore,
                "Guided kills award each enemy score and currency exactly once");
            Assert(player.shotsFired > 0, "Integrated combat adapter actually launches the guided attacks");
            report += "LIMIT: test invokes the runtime movement-input method rather than OS keyboard injection; player invulnerability isolates combat verification.\n";
            report += "LIMIT: GPU rendering, human handling/balance and known Windows native shutdown issue are evaluated separately.\n";
        }
    }
}
#endif
