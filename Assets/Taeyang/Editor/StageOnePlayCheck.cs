#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Taeyang.Editor
{
    [InitializeOnLoad]
    public static class StageOnePlayCheck
    {
        private static double started;
        private static int wave, bullets, errors;
        private static float waveStarted;
        private static bool collisionChecked, sawMovingCurve;
        private static int editorSearchErrors;
        private static string report = "";
        static StageOnePlayCheck()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            { if (SessionState.GetBool("Taeyang.PlayCheck", false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) { if(stack.Contains("UnityEditor.Search.SearchDatabase")) editorSearchErrors++; else errors++; } };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(StageOneBuilder.Path);
            SessionState.SetBool("Taeyang.PlayCheck", true);
            EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool("Taeyang.PlayCheck", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (started == 0) started = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Play Mode timeout");
                var director = UnityEngine.Object.FindFirstObjectByType<StageOneDirector>();
                var player = UnityEngine.Object.FindFirstObjectByType<DemoPlayer>();
                if (director == null || player == null) return;
                Time.timeScale = 10;
                if (!collisionChecked)
                {
                    if (Time.timeSinceLevelLoad < 0.4f) return;
                    if (SessionState.GetInt("Taeyang.CollisionHealth", -1) < 0)
                    {
                        SessionState.SetInt("Taeyang.CollisionHealth", player.health);
                        var prefab = director.prefabs[0].straightProjectile;
                        var bullet = UnityEngine.Object.Instantiate(prefab, player.transform.position + Vector3.up * 0.7f, Quaternion.identity);
                        bullet.Initialize(null,Vector2.down,null);
                        return;
                    }
                    if (Time.timeSinceLevelLoad < 1.5f) return;
                    if (player.health >= SessionState.GetInt("Taeyang.CollisionHealth", -1)) throw new Exception("Projectile/player trigger damage did not occur");
                    report += "PASS: projectile trigger reduces demo player health\n";
                    collisionChecked = true;
                }
                foreach(var projectile in UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                {
                    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                    bool curved=(bool)typeof(EnemyProjectile).GetField("curved",flags).GetValue(projectile);
                    Vector2 start=(Vector2)typeof(EnemyProjectile).GetField("curveStart",flags).GetValue(projectile);
                    if(curved && Vector2.Distance(start,projectile.transform.position)>0.2f) sawMovingCurve=true;
                }
                bullets = Mathf.Max(bullets,UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length);
                if (wave != director.Wave)
                {
                    wave = director.Wave; waveStarted = Time.timeSinceLevelLoad;
                    report += "ENTER WAVE " + wave + "\n";
                }
                if (Time.timeSinceLevelLoad-waveStarted > 6)
                    foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyShip>(FindObjectsSortMode.None)) enemy.TakeDamage(10000,Vector2.up);
                if (director.Complete)
                {
                    if(wave!=7 || bullets==0 || errors!=0 || !sawMovingCurve) throw new Exception("Incomplete wave/projectile/error checks: "+wave+"/"+bullets+"/"+errors);
                    File.WriteAllText("../unity-play-validation.txt",report+"PASS: all 7 waves complete; projectiles observed; runtime errors: "+errors+"; moving Bezier projectile observed: "+sawMovingCurve+"; Unity Editor Search exceptions: "+editorSearchErrors+"\nAutomated damage used to advance waves. Human gameplay and visual review not performed.");
                    Finish(0);
                }
            }
            catch(Exception e)
            { File.WriteAllText("../unity-play-validation.txt",report+"FAIL: "+e); Debug.LogException(e); Finish(1); }
        }
        private static void Finish(int code)
        {
            SessionState.SetBool("Taeyang.PlayCheck",false);
            SessionState.EraseInt("Taeyang.CollisionHealth");
            Time.timeScale=1;
            EditorApplication.isPlaying=false;
            EditorApplication.delayCall += ()=> EditorApplication.Exit(code);
        }
    }
}
#endif

