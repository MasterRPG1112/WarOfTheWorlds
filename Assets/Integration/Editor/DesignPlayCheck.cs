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
using O=UnityEngine.Object;
namespace Integration.Editor
{
    [InitializeOnLoad]
    public static class DesignPlayCheck
    {
        static Stack<IEnumerator> steps;static string report="";static int errors,searchErrors,code;static double deadline;
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static DesignPlayCheck()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(m,s,t)=>{if(!SessionState.GetBool("Design.Check",false))return;if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert){if(s.Contains("UnityEditor.Search.SearchDatabase"))searchErrors++;else{errors++;report+="ERROR "+m+"\n"+s+"\n";}}};
            EditorApplication.playModeStateChanged+=s=>
            {
                if(!SessionState.GetBool("Design.Check",false))return;
                if(s==PlayModeStateChange.EnteredPlayMode){DesignValidationMode.Prepare();steps=new Stack<IEnumerator>();steps.Push(Check());deadline=EditorApplication.timeSinceStartup+240;}
                if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Design.Check",false);File.WriteAllText("../design-play-validation.txt",report+"Runtime errors="+errors+"; Search exceptions="+searchErrors+"\n");EditorApplication.Exit(code);}
            };
        }
        public static void Run(){EditorSceneManager.OpenScene(IntegrationBuilder.ScenePath);SessionState.SetBool("Design.Check",true);EditorApplication.isPlaying=true;}
        static void Tick()
        {
            if(steps==null||!EditorApplication.isPlaying)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Design validation timeout");
                if(!steps.Peek().MoveNext()){steps.Pop();if(steps.Count==0){A(errors==0,"No game runtime errors");Finish(0);}return;}
                if(steps.Peek().Current is IEnumerator nested)steps.Push(nested);
            }
            catch(Exception e){report+="FAIL "+e+"\n";Finish(1);}
        }
        static void Finish(int result){code=result;steps=null;DesignValidationMode.Restore();Time.timeScale=1;EditorApplication.isPlaying=false;}
        static void A(bool yes,string name){if(!yes)throw new Exception(name);report+="PASS "+name+"\n";File.WriteAllText("../design-play-progress.txt",report);}
        static IEnumerator Wait(float seconds){float end=Time.time+seconds;double wall=EditorApplication.timeSinceStartup+Math.Max(6,seconds*3);while(Time.time<end){if(EditorApplication.timeSinceStartup>wall)throw new Exception("Simulation stalled");yield return null;}}
        static void Clear(){foreach(var e in O.FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))O.Destroy(e.gameObject);foreach(var e in O.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))O.Destroy(e.gameObject);foreach(var e in O.FindObjectsByType<IntegratedSkillEffect>(FindObjectsSortMode.None))O.Destroy(e.gameObject);}
        static void Quiet(IntegratedGame g){g.director.enabled=false;foreach(var s in O.FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None))s.CancelInvoke();}
        static EnemyShip Enemy(IntegratedGame g,int kind,Vector2 position,int hp=60){var e=O.Instantiate(g.director.enemyPrefabs[kind],position,Quaternion.identity);e.enabled=false;e.maxHealth=hp;e.ResetHealthForTesting();return e;}
        static EnemyProjectile Shot(IntegratedGame g,EnemyShip owner,Vector2 position,Vector2 direction,float speed=5){var b=O.Instantiate(g.director.enemyPrefabs[0].straightProjectile,position,Quaternion.identity);b.speed=speed;b.Initialize(owner,direction,null);return b;}
        static void State(StageDirector d,StageDirector.Phase phase){typeof(StageDirector).GetField("<State>k__BackingField",Private).SetValue(d,phase);}
        static IEnumerator Check()
        {
            yield return null;yield return null;
            var g=IntegratedGame.Active;var p=g.player;Quiet(g);
            A(g.Running&&g.selectionPanel==null&&Time.timeScale==1,"Immediate battle start without skill selection");
            A(p.Health==8&&p.ownedMask==0&&p.equippedPatches[0]<0&&p.equippedPatches[1]<0,"Shared HP8 and no initial skill ownership");
            var fixedMove=p.movement.GetType().GetMethod("FixedUpdate",Private);var playerBody=p.GetComponent<Rigidbody2D>();
            p.movement.ApplyMovementInput(Vector2.right);fixedMove.Invoke(p.movement,null);
            A(playerBody.linearVelocity.magnitude>0&&playerBody.linearVelocity.magnitude<5&&Mathf.Abs(playerBody.rotation)<90,"Fixed-step acceleration and turn begin smoothly without snapping");
            for(int n=0;n<15;n++)fixedMove.Invoke(p.movement,null);
            A(Mathf.Abs(playerBody.linearVelocity.magnitude-5)<.01f&&Vector2.Dot(p.movement.FacingDirection,Vector2.right)>.99f,"Movement converges rapidly to the requested direction and speed");
            p.movement.ApplyMovementInput(Vector2.zero);fixedMove.Invoke(p.movement,null);A(playerBody.linearVelocity.magnitude>0&&playerBody.linearVelocity.magnitude<5,"Release applies smooth deceleration");
            for(int n=0;n<15;n++)fixedMove.Invoke(p.movement,null);A(playerBody.linearVelocity.sqrMagnitude<.001f&&Vector2.Dot(p.movement.FacingDirection,Vector2.right)>.99f,"Rest preserves the last direction");
            p.Fire();p.TryBomb();p.TrySkill();yield return Wait(.5f);
            A(O.FindObjectsByType<PlayerShot>(FindObjectsSortMode.None).Length==0&&p.shotsFired==0,"No automatic/basic/guided player gun");
            for(int k=0;k<4;k++)A(!p.TryUsePatch(k),"Unowned patch blocked "+k);
            var legacy=JsonUtility.FromJson<SaveFile>("{\"version\":1,\"checkpoint\":{\"stage\":2,\"coreHp\":99,\"leftHp\":0,\"rightHp\":0,\"selectedSkill\":0,\"pull\":2}}");
            typeof(IntegrationSaveStore).GetMethod("Validate",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{legacy});
            A(legacy.version==2&&legacy.checkpoint.coreHp==8&&legacy.checkpoint.patchSlot0==-1&&legacy.checkpoint.ownedMask==1,"Legacy save migrates to one HP pool without free equipped skill");
            var a=Enemy(g,0,new Vector2(-6,2));var b=Enemy(g,1,new Vector2(-2,2),g.director.enemyPrefabs[0].straightProjectile.enemyDamage);int money=g.shop.coin;
            var missile=Shot(g,a,a.transform.position,Vector2.right);yield return Wait(1.1f);
            A(a.CurrentHealth==60&&b==null,"Missile ignores its owner and physically kills another enemy");
            A(g.Kills==1&&g.shop.coin>money,"Lured kill gives score and currency once");Clear();yield return null;
            for(int k=0;k<5;k++)
            {
                var e=Enemy(g,k,new Vector2(4,4));e.stageOnePatterns=true;
                typeof(EnemyShip).GetField("player",Private).SetValue(e,p.transform);
                typeof(EnemyShip).GetMethod("FirePattern",Private).Invoke(e,null);
                A(O.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length>0,"Enemy attack pattern creates real projectiles "+k);
                Clear();yield return null;
            }
            p.data.Invin=false;p.data.PlayerHp=8;
            var contact=Enemy(g,0,(Vector2)p.transform.position+Vector2.up*.6f);contact.approachPlayer=true;contact.moveSpeed=0;contact.enabled=true;
            yield return Wait(.25f);A(p.Health==7,"Enemy body contact damages the shared HP once");
            A(Vector2.Distance(contact.transform.position,p.transform.position)>.65f,"Contact knockback separates enemy and player");Clear();yield return Wait(1);
            var pickup=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MC/Prefeb/Heal.prefab");O.Instantiate(pickup,p.transform.position,Quaternion.identity);yield return Wait(.2f);p.Heal(99);A(p.Health==8,"Heart pickup and maximum healing clamp");
            p.data.HaveShield=2;p.Hit(null,1);A(p.Health==8&&p.data.HaveShield==1,"Shield absorbs one hit and consumes one charge");yield return Wait(.9f);
            p.data.HaveShield=0;p.data.InvinTimerStart();p.Hit(null,1);A(p.Health==8,"Timed invulnerability blocks damage");yield return Wait(3.2f);A(!p.data.Invin,"Timed invulnerability expires");
            for(int k=0;k<3;k++){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Integration/Prefabs/Patch_"+k+".prefab");O.Instantiate(prefab,p.transform.position,Quaternion.identity);yield return Wait(.12f);}
            A(p.PatchCount(0)>0&&p.PatchCount(1)>0&&p.PatchCount(2)>0,"Physical patch pickups grant matching stock");
            A(p.IsEquipped(0)&&p.IsEquipped(1)&&!p.IsEquipped(2)&&!p.TryUsePatch(2),"Only two patches equip and unmounted use is blocked");
            A(p.TryUsePatch(0)&&!p.TryUsePatch(0),"Black hole consumes stock only on successful cooldown start");
            missile=Shot(g,null,(Vector2)p.transform.position+Vector2.right,Vector2.up,0);yield return Wait(.15f);A(missile==null,"Black hole absorbs real enemy missiles");
            A(p.TryUsePatch(1)&&p.PatchCooldown(0)>0&&p.PatchCooldown(1)>0,"Patch cooldowns are independent");
            missile=Shot(g,null,(Vector2)p.transform.position+Vector2.left,Vector2.up,0);yield return Wait(.1f);A(missile.EffectiveSpeedFactor<1,"Slow field modifies real projectile speed");
            foreach(var effect in O.FindObjectsByType<IntegratedSkillEffect>(FindObjectsSortMode.None))O.Destroy(effect.gameObject);yield return null;A(missile.EffectiveSpeedFactor==1,"Ending slow effect restores original speed");Clear();yield return null;
            p.EquipPatch(2,1);p.data.Invin=true;b=Enemy(g,1,(Vector2)p.transform.position+Vector2.up*3);
            missile=Shot(g,null,(Vector2)p.transform.position+Vector2.up,Vector2.down,0);int reflectedDamage=missile.enemyDamage;A(p.TryUsePatch(2),"Owned equipped mirror activates");yield return Wait(.06f);A(missile!=null&&missile.Reflected,"Mirror reflects the existing missile");missile.speed=6;yield return Wait(.6f);A(b.CurrentHealth==60-reflectedDamage,"Reflected missile causes actual enemy damage without player bullets");Clear();yield return null;
            float cooldown=p.PatchCooldown(2);g.TogglePause();double wall=EditorApplication.timeSinceStartup+.25;while(EditorApplication.timeSinceStartup<wall)yield return null;A(Mathf.Abs(p.PatchCooldown(2)-cooldown)<.01f,"Pause freezes patch cooldowns");g.TogglePause();
            p.GrantPatch(3);p.EquipPatch(3,0);b=Enemy(g,0,new Vector2(6,0));b.approachPlayer=true;b.moveSpeed=2;b.enabled=true;missile=Shot(g,null,new Vector2(-6,2),Vector2.up);
            A(p.TryUsePatch(3)&&g.EnemiesFrozen&&Time.timeScale==1,"Owned time stop freezes enemies without global player pause");Vector2 bp=b.transform.position,mp=missile.transform.position;yield return Wait(.3f);A(Vector2.Distance(bp,b.transform.position)<.01f&&Vector2.Distance(mp,missile.transform.position)<.01f,"Time stop halts enemy and missile physics");yield return Wait(3);A(!g.EnemiesFrozen,"Time stop expires normally");Clear();yield return null;
            g.shop.coin=2000;g.shop.PullBtn();p.RefreshPatchOwnership();A(g.shop.coin==1400&&p.PatchCount(0)>0,"Existing shop purchase price and usable patch stock");
            g.shop.SpeedBtn();yield return null;A(p.movement.speed==7,"Purchased engine upgrade affects actual movement speed");
            g.SaveCheckpoint();IntegrationSaveStore.Reload();A(IntegrationSaveStore.Data.version==2&&IntegrationSaveStore.Data.checkpoint.coreHp<=8&&IntegrationSaveStore.Data.checkpoint.patchSlot0==3,"Version2 save retains shared HP and patch slots");
            // Real 300-second sections; no direct enemy kill or wave skip during this loop.
            g.director.enabled=true;int stages=1;double end=EditorApplication.timeSinceStartup+130;
            while(!g.Ended)
            {
                if(EditorApplication.timeSinceStartup>end)throw new Exception("Campaign progression timeout");
                p.data.Invin=true;p.data.PlayerHp=8;
                if(g.BetweenStages){A(g.shop.shopMenu.activeSelf&&Time.timeScale==0,"Stage clear opens shop intermission "+g.director.stageNumber);g.ContinueAfterShop();stages++;}
                Time.timeScale=20;yield return null;
            }
            A(g.Won&&g.director.stageNumber==3&&stages==3,"Three 300-second stages finish through explicit missing-boss test path");
            A(O.FindObjectsByType<PlayerShot>(FindObjectsSortMode.None).Length==0,"No player basic shots throughout campaign");
            g.Restart();yield return null;yield return null;g=IntegratedGame.Active;p=g.player;Quiet(g);A(g.Running&&p.Health==8&&p.ownedMask==0,"Restart resets shared health and stock without selection");
            var boss=new GameObject("Synthetic boss adapter test");boss.transform.position=p.transform.position+Vector3.up*.5f;boss.AddComponent<CircleCollider2D>().isTrigger=true;boss.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;var adapter=boss.AddComponent<IntegratedBossTarget>();
            State(g.director,StageDirector.Phase.AwaitingBoss);g.director.AttachBoss(boss);p.data.Invin=true;yield return Wait(.2f);A(adapter.CurrentHealth<adapter.MaxHealth,"Real player body contact damages boss adapter");
            int bossHp=adapter.CurrentHealth;missile=Shot(g,null,(Vector2)boss.transform.position+Vector2.right,Vector2.left);yield return Wait(.3f);A(adapter.CurrentHealth==bossHp,"Ordinary enemy missiles cannot damage boss");
            adapter.TakeRamDamage(999);A(g.director.State==StageDirector.Phase.Clear,"Boss adapter defeat calls stage completion interface");O.Destroy(boss);Clear();
            report+="LIMIT: synthetic boss adapter only; actual teammate boss, human controls/balance and seven absent design photos are not validated. Accelerated campaign uses invulnerability.\n";
        }
    }
}
#endif
