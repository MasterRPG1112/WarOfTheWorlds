using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Taeyang;
using O=UnityEngine.Object;
namespace Integration
{
    // Opt-in only. Runs the same physical/scene checks in Editor and a Windows player.
    public class CampaignValidation : MonoBehaviour
    {
        public string folder;
        public Action<int> completed;
        public bool capture;
        int errors, passes;
        string report="";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static bool isolated;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--assignment-smoke")<0&&Array.IndexOf(args,"Integration.Editor.AssignmentPlayCheck.Run")<0)return;
            IntegrationSaveStore.ResetForTests();isolated=true;OpeningSequence.SuppressForTests=true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--assignment-smoke");if(i<0||i+1>=args.Length)return;
            var probe=new GameObject("Assignment validation only").AddComponent<CampaignValidation>();probe.folder=args[i+1];probe.capture=true;
        }
        void Awake(){DontDestroyOnLoad(gameObject);Application.logMessageReceived+=Log;}
        void OnDestroy(){Application.logMessageReceived-=Log;}
        void Log(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            if(stack.Contains("UnityEditor.Search.SearchDatabase")){report+="EDITOR SEARCH EXCEPTION "+message+"\n";return;}
            errors++;report+="ERROR "+message+"\n"+stack+"\n";
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);var stack=new Stack<IEnumerator>();stack.Push(Check());bool failed=false;
            double deadline=Time.realtimeSinceStartupAsDouble+180;
            while(stack.Count>0)
            {
                object next=null;
                try
                {
                    if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("Validation timeout");
                    if(!stack.Peek().MoveNext()){stack.Pop();continue;}
                    next=stack.Peek().Current;if(next is IEnumerator nested){stack.Push(nested);continue;}
                }
                catch(Exception e){report+="FAIL "+e+"\n";failed=true;break;}
                yield return next;
            }
            report+="Assertions passed="+passes+"; runtime errors="+errors+"\n";
            report+="Automated checks use direct method calls, stage-time injection, and temporary invulnerability. Human keyboard, subjective visuals/balance/audio are not certified.\n";
            File.WriteAllText(Path.Combine(folder,"assignment-validation.txt"),report);
            if(isolated){IntegrationSaveStore.RestoreAfterTests();isolated=false;}OpeningSequence.SuppressForTests=false;Time.timeScale=1;
            int code=failed||errors>0?1:0;
            if(completed!=null)completed(code);else Application.Quit(code);
        }
        void A(bool condition,string name){if(!condition)throw new Exception(name);passes++;report+="PASS "+name+"\n";File.WriteAllText(Path.Combine(folder,"progress.txt"),report);}
        static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        static void Field(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        static void Phase(StageDirector d,StageDirector.Phase phase)=>Field(d,"<State>k__BackingField",phase);
        static void Clear()
        {
            foreach(var e in O.FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))O.Destroy(e.gameObject);
            foreach(var e in O.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))O.Destroy(e.gameObject);
            foreach(var e in O.FindObjectsByType<CampaignDrop>(FindObjectsSortMode.None))O.Destroy(e.gameObject);
            foreach(var e in O.FindObjectsByType<StageObstacle>(FindObjectsSortMode.None))O.Destroy(e.gameObject);
        }
        static EnemyShip Enemy(IntegratedGame g,Vector2 position,int hp=20)
        {
            var e=O.Instantiate(g.director.enemyPrefabs[0],position,Quaternion.identity);e.enabled=false;e.maxHealth=hp;e.ResetHealthForTesting();return e;
        }
        void Capture(string name,int width,int height)
        {
            if(!capture)return;
            A(FrameCapture.TryCapture(Path.Combine(folder,name+".png"),out string error,width,height),"GPU capture "+name+" "+error);
        }
        IEnumerator Check()
        {
            yield return null;yield return null;
            // The menu opening is independently exercised below after combat isolation.
            var menu=O.FindAnyObjectByType<IntegratedMenu>();
            if(menu!=null){menu.NewGame();yield return null;yield return null;}
            var g=IntegratedGame.Active;var p=g.player;var d=g.director;
            A(g.Running&&p.Health==8&&p.equippedParts==0,"New run: HP8, no parts, no skill selection");
            d.enabled=false;Clear();yield return null;
            for(int wave=1;wave<=5;wave++)for(int tick=0;tick<10;tick++)A(StageDirector.PickKind(1,wave,tick,0)!=2&&StageDirector.PickKind(1,wave,tick,0)!=4,"Stage 1 three-kind pool "+wave+"/"+tick);
            Invoke(d,"SpawnObstacle");A(O.FindObjectsByType<StageObstacle>(FindObjectsSortMode.None).Length==0,"Stage 1 meteor factory refuses spawn");
            foreach(var sp in O.FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None))A(!sp.integratedArena||!sp.IsInvoking(),"No timer-based integrated item/meteor spawner "+sp.name);
            p.Fire();p.TrySkill();yield return new WaitForSeconds(.2f);
            A(p.shotsFired==0&&O.FindObjectsByType<PlayerShot>(FindObjectsSortMode.None).Length==0,"No player direct attack");
            p.transform.position=new Vector3(0,-5,0);
            var a=Enemy(g,new Vector2(-5,1));var b=Enemy(g,new Vector2(-1,1),d.enemyPrefabs[0].straightProjectile.enemyDamage);
            var shot=O.Instantiate(d.enemyPrefabs[0].straightProjectile,a.transform.position,Quaternion.identity);shot.speed=8;shot.Initialize(a,Vector2.right,null);
            int kills=g.Kills;yield return new WaitForSeconds(.8f);
            A(a!=null&&a.CurrentHealth==20&&b==null&&g.Kills==kills+1,"Physical lured missile kills another enemy, ignores owner, rewards once (source="+(a==null?-1:a.CurrentHealth)+", target="+(b==null?-1:b.CurrentHealth)+", kills="+g.Kills+")");
            var drop=O.FindAnyObjectByType<CampaignDrop>();A(drop!=null&&Vector2.Distance(drop.transform.position,new Vector2(-1,1))<.05f,"Drop exists at exact enemy death location");
            int stock=p.PatchCount(drop.kind);p.transform.position=drop.transform.position;yield return new WaitForSeconds(.2f);
            A(drop==null&&p.PatchCount(0)==stock+1,"Physical player trigger collects skill drop exactly once");
            Clear();yield return null;p.transform.position=new Vector3(0,-5,0);
            for(int k=0;k<5;k++)p.GrantPatch(k,2);
            p.equippedPatches=new[]{-1,-1};var target=Enemy(g,new Vector2(4,3));
            for(int k=0;k<5;k++){
                if(k==4){shot=O.Instantiate(d.enemyPrefabs[0].straightProjectile,new Vector3(4,0),Quaternion.identity);shot.speed=8;shot.Initialize(target,Vector2.up,null);}
                int before=p.PatchCount(k);A(p.TryUsePatch(k)&&p.PatchCount(k)==before-1&&p.PatchCooldown(k)>0,"Skill "+k+" works without slots, consumes stock and starts cooldown");A(!p.TryUsePatch(k),"Skill "+k+" cooldown prevents repeat");}
            A(!g.EnemiesFrozen&&g.MissilesFrozen,"Time stop applies to missiles, not enemy ships");
            Field(g,"frozenUntil",0f);yield return new WaitForSeconds(.6f);
            A(target.CurrentHealth<20,"Force guidance physically redirects an owner missile back into its owner");
            int beforeBomb=target.CurrentHealth;
            p.bombs=1;shot=O.Instantiate(d.enemyPrefabs[0].straightProjectile,new Vector3(5,5),Quaternion.identity);shot.Initialize(target,Vector2.down,null);
            A(p.TryBomb()&&p.bombs==0&&target.CurrentHealth==beforeBomb,"Bomb clears missiles without damaging enemies");yield return null;A(shot==null,"Bomb actually removes projectile");
            p.data.PlayerHp=3;var heal=CampaignDrop.Spawn(p.transform.position,5);A(heal.Collect(p)&&p.Health==5&&!heal.Collect(p),"Recovery collectible heals once");
            var shield=CampaignDrop.Spawn(p.transform.position,6);shield.Collect(p);A(p.data.HaveShield>0,"Defense collectible adds a shield");p.data.HaveShield=0;
            // Owned parts are distinct from equipment. All three effects execute in real combat methods.
            A(!g.TryBuyPart(0),"Part purchase blocked during battle");
            Phase(d,StageDirector.Phase.Clear);Invoke(g,"BeginIntermission");g.shop.coin=3000;
            A(g.TryBuyPart(0)&&g.TryBuyPart(1)&&g.TryBuyPart(2)&&g.shop.coin==0&&p.equippedParts==7,"Shop buys and equips all three parts at actual prices");
            A(!g.TryBuyPart(0),"Duplicate or unaffordable purchase is rejected");
            A(p.TogglePart(1)&&(p.equippedParts&2)==0&&p.TogglePart(1),"Shop permits explicit unequip and re-equip");
            g.ContinueAfterShop();d.enabled=false;yield return null;A(d.stageNumber==2&&p.equippedParts==7,"Stage transition keeps equipment");
            A(p.movement.speed==7&&p.RamDamage==30,"Engine raises speed; reinforced hull raises boss ram damage");
            p.data.PlayerHp=1;p.Hit(null,1);A(p.Health==4&&g.shop.part1==0&&(p.equippedParts&1)==0,"Emergency part consumes on lethal hit and revives at HP4");
            A(!p.TogglePart(1),"Part changes blocked during combat");
            Invoke(d,"SpawnObstacle");A(O.FindObjectsByType<StageObstacle>(FindObjectsSortMode.None).Length==1,"Stage 2 meteor spawns");Clear();yield return null;
            var tools=g.GetComponent<CampaignTools>();
            A(!tools.Bind(0,KeyCode.A)&&!tools.Bind(0,tools.keys[1])&&tools.Bind(0,KeyCode.F12),"Cheat binding restricts F1-F12 and rejects duplicates");
            tools.Execute(0);A(tools.showColliders,"Debug cheat toggles real collider overlay");tools.Execute(0);
            tools.Execute(1);p.data.PlayerHp=8;yield return new WaitForSeconds(1);p.Hit(null,3);A(p.Health==8&&p.cheatInvulnerable,"Invulnerability cheat blocks actual damage");tools.Execute(1);p.Hit(null,1);A(p.Health==7,"Invulnerability toggle restores damage");
            int money=g.shop.coin;tools.Execute(3);A(g.shop.coin==money+1000,"Money cheat grants configured amount");
            target=Enemy(g,new Vector2(4,4));tools.Execute(2);yield return null;A(target==null,"Kill cheat destroys active enemies through defeat pipeline");Clear();yield return null;
            Capture("hud-1920x1080",1920,1080);Capture("hud-1280x720",1280,720);Capture("hud-1024x768",1024,768);Capture("hud-2560x1080",2560,1080);
            // Death reload: current stage, money, stock and equipment survive; HP and stage timer reset.
            g.shop.coin=3210;g.shop.part2=1;p.equippedParts=2;p.guideStock=3;p.data.PlayerHp=0;g.End(false);
            A(IntegrationSaveStore.HasCheckpoint,"Game over keeps continuation checkpoint");g.Restart();yield return null;yield return null;yield return null;
            g=IntegratedGame.Active;p=g.player;d=g.director;d.enabled=false;
            A(d.stageNumber==2&&d.Elapsed<1&&p.Health==8&&g.shop.coin==3210&&p.equippedParts==2&&p.guideStock==3,"Restart loads same stage, full HP, money, parts and skill stock");
            // Exercise each stage's real boss spawn, attack, immunity, contact damage and clear pipeline.
            for(int stage=1;stage<=3;stage++)
            {
                Clear();yield return null;d.stageNumber=stage;Field(d,"<Elapsed>k__BackingField",300f);Phase(d,StageDirector.Phase.Clearing);Invoke(d,"Update");
                var boss=O.FindAnyObjectByType<CampaignBoss>();A(boss!=null&&boss.stage==stage&&d.State==StageDirector.Phase.Boss,"Stage "+stage+" attaches real boss without test skip");
                int hp=boss.CurrentHealth;boss.Attack(p.transform);boss.Attack(p.transform);yield return null;A(boss.Attacks>0&&O.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length>0,"Boss "+stage+" fires stage attack pattern");
                shot=O.Instantiate(d.enemyPrefabs[0].straightProjectile,boss.transform.position,Quaternion.identity);shot.Initialize(null,Vector2.up,null);yield return new WaitForSeconds(.1f);
                A(boss.CurrentHealth==hp,"Boss "+stage+" immune to missiles");
                p.cheatInvulnerable=true;p.transform.position=boss.transform.position;yield return new WaitForSeconds(.2f);A(boss.CurrentHealth<hp,"Boss "+stage+" receives physical player ram damage");
                p.transform.position=new Vector3(0,-5,0);yield return null;yield return null;Capture("boss-stage-"+stage,1280,720);
                boss.TakeRamDamage(int.MaxValue);yield return null;yield return null;
                A(d.State==StageDirector.Phase.Clear&&boss.IsDefeated,"Boss "+stage+" death clears stage exactly through real pipeline");
                if(stage<3){A(g.BetweenStages,"Clear opens between-stage shop "+stage);Capture("shop-stage-"+stage,1280,720);g.ContinueAfterShop();d.enabled=false;yield return null;}
            }
            A(g.Ended&&g.Won&&!IntegrationSaveStore.HasCheckpoint,"Final boss ends campaign and clears checkpoint");
            // Menu opening can complete automatically or through a visible skip button.
            OpeningSequence.SuppressForTests=false;g.BackToMenu();yield return null;yield return null;yield return null;
            var opening=O.FindAnyObjectByType<OpeningSequence>();A(opening!=null&&opening.Playing,"Opening appears before menu interaction on first launch");yield return new WaitForSecondsRealtime(.5f);Capture("opening",1280,720);opening.Finish();yield return null;
            A(!opening.Playing&&O.FindAnyObjectByType<IntegratedMenu>()!=null,"Opening skip returns to usable original menu");
            A(errors==0,"No game runtime errors");
        }
    }
}
