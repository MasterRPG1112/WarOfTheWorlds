#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Taeyang.Editor
{
    public static class StageOneBuilder
    {
        public const string Path = "Assets/Taeyang/Scenes/TaeyangStage1.unity";
        [MenuItem("Tools/WarOfTheWorlds/Build Taeyang Stage 1 Demo")]
        public static void Build()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene("Assets/Taeyang/Scenes/EnemyTest.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var spawner = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSpawner>();
                var hud = UnityEngine.Object.FindFirstObjectByType<EnemyDemoHUD>();
                var director = new GameObject("Stage 1 - Seven Waves").AddComponent<StageOneDirector>();
                director.prefabs = spawner.enemyPrefabs;
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
                UnityEngine.Object.DestroyImmediate(spawner.gameObject);
                // Dynamic player enables trigger contacts with kinematic enemy projectiles.
                var player = UnityEngine.Object.FindFirstObjectByType<DemoPlayer>();
                player.GetComponent<Rigidbody2D>().useFullKinematicContacts = true;
                EditorSceneManager.SaveScene(scene, Path);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        public static void ValidateBatch()
        {
            try
            {
                Build();
                Assert(Vector2.Distance(EnemyProjectile.Bezier(Vector2.zero,Vector2.up,Vector2.right,0),Vector2.zero)<0.001f,"curve start");
                Assert(Vector2.Distance(EnemyProjectile.Bezier(Vector2.zero,Vector2.up,Vector2.right,1),Vector2.right)<0.001f,"curve end");
                Assert(EnemyProjectile.Bezier(Vector2.zero,Vector2.up,Vector2.right,0.5f).y>0.4f,"curve bends");
                Assert(StageOneMath.Predict(Vector2.zero,new Vector2(0,5),Vector2.zero,7,1.2f)==new Vector2(0,5),"stationary target");
                Assert(StageOneMath.Predict(Vector2.zero,new Vector2(0,5),Vector2.right,7,1.2f).x>0,"moving target lead");
                Assert(StageOneMath.Predict(Vector2.zero,new Vector2(0,5),Vector2.right*100,7,1.2f).x<=120.001f,"lead cap");
                Assert(StageOneDirector.Waves.Length==7,"seven waves");
                EditorSceneManager.OpenScene(Path);
                var director = UnityEngine.Object.FindFirstObjectByType<StageOneDirector>();
                Assert(director!=null && director.prefabs.Length==5,"scene references");
                foreach(var prefab in director.prefabs) Assert(prefab!=null,"enemy prefab");
                File.WriteAllText("../unity-validation.txt","PASS: 13 assertions; scene generated; Unity compilation successful.");
                Debug.Log("TAEYANG_VALIDATION_PASS");
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void Assert(bool condition,string name)
        { if(!condition) throw new Exception("Validation failed: "+name); }
    }
}
#endif
