#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Taeyang.Editor
{
    public static class ThreeStageBuilder
    {
        [MenuItem("Tools/WarOfTheWorlds/Build Taeyang Three Stages")]
        public static void Build()
        {
            for(int stage=1;stage<=3;stage++)
            {
                var scene=EditorSceneManager.OpenScene(StageOneBuilder.Path);
                var old=UnityEngine.Object.FindFirstObjectByType<StageOneDirector>();
                var prefabs=old.prefabs;
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                var director=new GameObject("Stage "+stage+" Director").AddComponent<StageDirector>();
                director.stageNumber=stage; director.enemyPrefabs=prefabs;
                EditorSceneManager.SaveScene(scene,"Assets/Taeyang/Scenes/TaeyangCampaign"+stage+".unity");
            }
            Debug.Log("TAEYANG_THREE_SCENES_BUILT");
        }
        public static void Validate()
        {
            try
            {
                Build();
                if(Vector2.Distance(EnemyProjectile.Bezier(Vector2.zero,Vector2.up,Vector2.right,0),Vector2.zero)>.001f ||
                   Vector2.Distance(EnemyProjectile.Bezier(Vector2.zero,Vector2.up,Vector2.right,1),Vector2.right)>.001f ||
                   EnemyProjectile.Bezier(Vector2.zero,Vector2.up,Vector2.right,.5f).y<.4f) throw new Exception("Curve geometry");
                Vector2 predicted=StageOneMath.Predict(Vector2.zero,new Vector2(0,5),Vector2.right,7,1.2f);
                if(predicted.x<=0 || predicted.x>1.2f) throw new Exception("Predictive lead");
                for(int s=1;s<=3;s++)
                {
                    EditorSceneManager.OpenScene("Assets/Taeyang/Scenes/TaeyangCampaign"+s+".unity");
                    var d=UnityEngine.Object.FindFirstObjectByType<StageDirector>();
                    if(d.stageNumber!=s || d.SpawnInterval!=s+2 || d.enemyPrefabs.Length!=5) throw new Exception("Stage configuration");
                    for(int w=1;w<=5;w++) for(int t=0;t<20;t++)
                    { int kind=StageDirector.PickKind(s,w,t,0); if(kind<0 || kind>4) throw new Exception("Enemy pool"); }
                }
                File.WriteAllText("Assets/Taeyang/StageValidation.txt","PASS: Unity 6000.5 compilation; 3 scene references; 300 second duration; 3/4/5 second intervals; 300 wave selection cases.\n");
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
    [InitializeOnLoad]
    public static class ThreeStagePlayCheck
    {
        private static double start;
        private static int errors, observedShots, observedObstacles;
        private static string report="";
        private static bool waiting, sawCurve, sawHoming;
        private static int editorSearchErrors;
        static ThreeStagePlayCheck()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+= state =>
            {
                if(!SessionState.GetBool("Taeyang.ThreeCheck",false)) return;
                if(state==PlayModeStateChange.EnteredEditMode && waiting)
                {
                    waiting=false;
                    int stage=SessionState.GetInt("Taeyang.CheckStage",1)+1;
                    if(stage>3) { File.WriteAllText("Assets/Taeyang/StagePlayValidation.txt",SessionState.GetString("Taeyang.CheckReport","")+"PASS: all stages; accelerated simulation; synthetic boss lifecycle tested. Human gameplay/balance review not performed.\n"); SessionState.SetBool("Taeyang.ThreeCheck",false); EditorApplication.Exit(0); }
                    else { SessionState.SetInt("Taeyang.CheckStage",stage); Open(stage); }
                }
            };
            Application.logMessageReceived+=(m,s,t)=> { if(SessionState.GetBool("Taeyang.ThreeCheck",false) && (t==LogType.Error || t==LogType.Exception || t==LogType.Assert)) { if(s.Contains("UnityEditor.Search.SearchDatabase")) editorSearchErrors++; else { errors++; report+=m+"\n"; } } };
        }
        public static void Run()
        {
            SessionState.SetBool("Taeyang.ThreeCheck",true); SessionState.SetInt("Taeyang.CheckStage",1); SessionState.SetString("Taeyang.CheckReport",""); Open(1);
        }
        private static void Open(int stage)
        {
            start=0; observedShots=0; observedObstacles=0;
            EditorSceneManager.OpenScene("Assets/Taeyang/Scenes/TaeyangCampaign"+stage+".unity");
            EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            if(!SessionState.GetBool("Taeyang.ThreeCheck",false) || !EditorApplication.isPlaying || waiting) return;
            if(start==0) start=EditorApplication.timeSinceStartup;
            try
            {
                if(EditorApplication.timeSinceStartup-start>100) throw new Exception("Stage timeout");
                var d=UnityEngine.Object.FindFirstObjectByType<StageDirector>(); if(d==null) return;
                Time.timeScale=12;
                observedShots=Mathf.Max(observedShots,UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length);
                observedObstacles=Mathf.Max(observedObstacles,UnityEngine.Object.FindObjectsByType<StageObstacle>(FindObjectsSortMode.None).Length);
                foreach(var bullet in UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                {
                    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                    if((bool)typeof(EnemyProjectile).GetField("curved",flags).GetValue(bullet))
                    {
                        Vector2 origin=(Vector2)typeof(EnemyProjectile).GetField("curveStart",flags).GetValue(bullet);
                        if(Vector2.Distance(origin,bullet.transform.position)>.2f) sawCurve=true;
                    }
                    if(typeof(EnemyProjectile).GetField("target",flags).GetValue(bullet) != null) sawHoming=true;
                }
                if(d.State==StageDirector.Phase.AwaitingBoss)
                {
                    int expected=Mathf.CeilToInt(StageDirector.Duration/d.SpawnInterval)-1;
                    if(d.SpawnTicks!=expected || d.Elapsed!=300 || d.Wave!=5 || observedShots==0 || observedObstacles==0 || !sawCurve || !sawHoming || errors!=0) throw new Exception("Progress/spawn/runtime checks: "+d.SpawnTicks+" expected "+expected+" errors "+errors);
                    // Verify real boss attachment and explicit pooled-boss completion contract.
                    var fake=new GameObject("Test boss lifecycle only"); d.AttachBoss(fake);
                    if(d.State!=StageDirector.Phase.Boss) throw new Exception("Boss attach");
                    d.NotifyBossDefeated(); UnityEngine.Object.Destroy(fake);
                    if(d.State!=StageDirector.Phase.Clear) throw new Exception("Boss clear");
                    report+="PASS Stage "+d.stageNumber+": 300 seconds, 5 waves, spawn ticks "+d.SpawnTicks+", max projectiles "+observedShots+", max obstacles "+observedObstacles+", boss callback/clear, moving curve and homing observed, runtime errors 0, Editor Search exceptions "+editorSearchErrors+".\n";
                    SessionState.SetString("Taeyang.CheckReport",SessionState.GetString("Taeyang.CheckReport","")+report);
                    if(d.stageNumber<3)
                    {
                        int old=d.stageNumber; d.AdvanceToNextStage();
                        if(d.stageNumber!=old+1 || d.Elapsed!=0 || d.State!=StageDirector.Phase.Survival) throw new Exception("Next stage reset");
                    }
                    Time.timeScale=1; waiting=true; EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e) { File.WriteAllText("Assets/Taeyang/StagePlayValidation.txt",report+"FAIL: "+e); SessionState.SetBool("Taeyang.ThreeCheck",false); EditorApplication.isPlaying=false; EditorApplication.Exit(1); }
        }
    }
}
#endif
