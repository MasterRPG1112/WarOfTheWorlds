#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Taeyang;
namespace Integration.Editor
{
    [InitializeOnLoad]
    public static class IntegrationPlayCheck
    {
        static IEnumerator checks;
        static string report;
        static int errors, searchErrors, code;
        static double deadline;
        static IntegrationPlayCheck()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool("Integration.Check",false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) { IntegrationSaveStore.ResetForTests(); report=""; errors=0; searchErrors=0; deadline=EditorApplication.timeSinceStartup+240; checks=Check(); }
                if (state == PlayModeStateChange.EnteredEditMode && checks == null)
                { SessionState.SetBool("Integration.Check",false); File.WriteAllText("../integration-play-validation.txt",report+"Runtime errors="+errors+"; Editor Search exceptions="+searchErrors+"\n"); EditorApplication.Exit(code); }
            };
            Application.logMessageReceived += (message,stack,type) =>
            {
                if(!SessionState.GetBool("Integration.Check",false) || (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)) return;
                if(stack.Contains("UnityEditor.Search.SearchDatabase")) searchErrors++;
                else { errors++; report += "ERROR: "+message+"\n"+stack+"\n"; }
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(IntegrationBuilder.ScenePath);
            SessionState.SetBool("Integration.Check",true); EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(checks==null || !EditorApplication.isPlaying) return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Play check timeout");
                if(checks.MoveNext()) return;
                Assert(errors==0,"Zero game runtime errors"); code=0; Finish();
            }
            catch(Exception e) { report+="FAIL: "+e+"\n"; code=1; Finish(); }
        }
        static void Finish() { checks=null; IntegrationSaveStore.RestoreAfterTests(); Time.timeScale=1; EditorApplication.isPlaying=false; }
        static void Assert(bool yes,string label) { if(!yes) throw new Exception(label); report+="PASS: "+label+"\n"; File.WriteAllText("../integration-play-progress.txt",report); Debug.Log("INTEGRATION_CHECK "+label); }
        static IEnumerator Check()
        {
            yield return null;
            var g=UnityEngine.Object.FindFirstObjectByType<IntegratedGame>(); var p=g.player;
            var spawners=UnityEngine.Object.FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None);
            foreach(var s in spawners) { s.CancelInvoke(); s.enabled=false; }
            Assert(Time.timeScale==0 && !g.Running,"Initial skill choice pauses gameplay");
            g.selectionPanel.GetComponentsInChildren<Button>()[0].onClick.Invoke(); yield return null;
            Assert(g.Running && p.selectedSkill==0,"Serialized skill button starts game");
            var d=g.director; d.enabled=false;
            // Real physics contacts, never SendMessage or direct damage for these assertions.
            int hp=p.data.PlayerHp;
            var shot=UnityEngine.Object.Instantiate(d.enemyPrefabs[0].straightProjectile,p.transform.position+Vector3.up*.7f,Quaternion.identity);
            shot.Initialize(null,Vector2.down,null);
            float until=Time.time+1.2f; while(Time.time<until) yield return null;
            Assert(p.data.PlayerHp==hp-1,"Enemy projectile contact reduces real core HP once");
            Assert(g.hpUI.Any(ui=>ui.HpBarInt.text==(hp-1).ToString()),"HP UI reflects real damage");
            p.data.Invin=true;
            int oldScore=g.score.score;
            for(int i=0;i<5;i++)
            {
                foreach(var bullet in UnityEngine.Object.FindObjectsByType<PlayerShot>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(bullet.gameObject);
                var enemy=UnityEngine.Object.Instantiate(d.enemyPrefabs[i],p.transform.position+Vector3.up*2,Quaternion.identity);
                enemy.moveSpeed=0; enemy.driftWidth=0; enemy.fireInterval=100;
                until=Time.time+6; while(enemy!=null && Time.time<until) yield return null;
                Assert(enemy==null,"Auto-fire physics destroys enemy type "+i);
            }
            Assert(g.Kills==5 && g.score.score>oldScore && g.shop.coin==g.score.score,"Enemy kills award score and shop currency once");
            // Shop methods are invoked through their original purchase API.
            g.shop.coin=2000; g.shop.PullBtn();
            Assert(g.shop.pull==1 && g.shop.coin==1400,"Original shop purchase deducts price and grants inventory");
            Assert(p.TryConsumable(0) && g.shop.pull==0 && !p.TryConsumable(0),"Purchased consumable is usable exactly once");
            g.shop.stop=1;
            Assert(g.TryTimeStop() && g.EnemiesFrozen && g.shop.stop==0,"Purchased time stop activates and consumes inventory");
            until=Time.time+3.1f; while(Time.time<until) yield return null;
            Assert(!g.EnemiesFrozen,"Time stop expires");
            Assert(p.TryBomb() && p.bombs==0 && !p.TryBomb(),"Original Origin prefab connects as one bomb per stage");
            // Each skill prefab must instantiate and affect an actual projectile.
            for(int kind=0;kind<3;kind++)
            {
                g.SelectSkill(kind); d.enabled=false; until=Time.time+8.1f; while(Time.time<until) yield return null;
                var bullet=UnityEngine.Object.Instantiate(d.enemyPrefabs[0].straightProjectile,p.transform.position+Vector3.right*.8f,Quaternion.identity);
                bullet.Initialize(null,Vector2.up,null);
                Assert(p.TrySkill(),"Skill activation "+kind); yield return null; yield return null;
                Assert(kind==1 ? bullet!=null && bullet.externalSpeedFactor<1 : bullet==null,"Skill changes projectile behavior "+kind);
            }
            // Item collection through actual trigger callbacks.
            p.data.PlayerHp=4; p.core.playerHp=4; p.data.Invin=false;
            foreach(var name in new[]{"Heal","Shield","Invincibility"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MC/Prefeb/"+name+".prefab");
                UnityEngine.Object.Instantiate(prefab,p.transform.position,Quaternion.identity);
                until=Time.time+.3f; while(Time.time<until) yield return null;
            }
            Assert(p.data.PlayerHp==5 && p.data.HaveShield==1 && p.data.Invin,"Heal/shield/invincibility item triggers reach shared player data");
            // Pause freezes the actual stage timer.
            d.enabled=true; g.TogglePause(); float elapsed=d.Elapsed;
            double wait=EditorApplication.timeSinceStartup+.3; while(EditorApplication.timeSinceStartup<wait) yield return null;
            Assert(d.Elapsed==elapsed && !g.Running,"Pause freezes stage simulation"); g.TogglePause();
            foreach(var s in spawners) { s.enabled=true; s.InvokeRepeating("SpawnItem",2,s.spawnInterval); }
            int lastStage=1, maxEnemies=0, maxBullets=0;
            // Accelerated campaign, invulnerability replenished solely by the test harness.
            // Enemies are NOT auto-killed here; normal auto-fire and existing retreat rules run.
            while(!g.Ended)
            {
                p.data.Invin=true; p.data.PlayerHp=8; p.core.playerHp=8;
                if(!g.Running) g.SelectSkill(0);
                Time.timeScale=20;
                maxEnemies=Mathf.Max(maxEnemies,UnityEngine.Object.FindObjectsByType<EnemyShip>(FindObjectsSortMode.None).Length);
                maxBullets=Mathf.Max(maxBullets,UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length);
                if(d.stageNumber!=lastStage) { report+="PASS: advanced from stage "+lastStage+" without boss\n"; lastStage=d.stageNumber; }
                yield return null;
            }
            Assert(g.Won && d.stageNumber==3 && d.Elapsed==300,"All three 300-second stages reach campaign result without boss");
            Assert(maxEnemies>0 && maxBullets>0,"Campaign spawned enemies and moving projectiles");
            report+="Observed max enemies="+maxEnemies+", max enemy bullets="+maxBullets+", shots="+p.shotsFired+", kills="+g.Kills+"\n";
            g.resultPanel.GetComponentInChildren<Button>().onClick.Invoke(); yield return null; yield return null;
            g=UnityEngine.Object.FindFirstObjectByType<IntegratedGame>(); p=g.player;
            Assert(!g.Ended && !g.Running && g.director.stageNumber==1 && p.data.PlayerHp==8,"Serialized restart button resets campaign, HP and skill selection");
            g.SelectSkill(0); g.director.enabled=false;
            foreach(var s in UnityEngine.Object.FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None)) s.CancelInvoke();
            p.data.PlayerHp=1; p.core.playerHp=1; p.data.Invin=false; p.data.HaveShield=0;
            shot=UnityEngine.Object.Instantiate(g.director.enemyPrefabs[0].straightProjectile,p.transform.position+Vector3.up*.7f,Quaternion.identity);
            shot.Initialize(null,Vector2.down,null);
            until=Time.time+1.5f; while(!g.Ended && Time.time<until) yield return null;
            Assert(g.Ended && !g.Won && Time.timeScale==0 && g.resultPanel.activeSelf,"Lethal projectile opens game-over result and stops gameplay");
            report+="LIMIT: accelerated automated run uses invulnerability; no human play/balance review.\n";
        }
    }
}
#endif
