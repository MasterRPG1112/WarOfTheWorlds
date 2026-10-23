#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Taeyang.Editor
{
    // Creates real Unity prefabs and an isolated test scene once the PNGs/C# import.
    // Never changes the team's Stage1, Player or ProjectSettings assets.
    public static class EnemyPackInstaller
    {
        private const string Root = "Assets/Taeyang/";
        private static bool busy;

        [InitializeOnLoadMethod]
        private static void OnDomainLoaded()
        {
            EditorApplication.delayCall += () =>
            {
                if (!busy && !EditorApplication.isPlayingOrWillChangePlaymode &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Enemy_Scout.prefab") == null)
                    Install();
            };
        }

        [MenuItem("Tools/WarOfTheWorlds/Generate Enemy Pack")]
        public static void Install()
        {
            if (busy) return;
            busy = true;
            try
            {
                EnsureFolder("Prefabs");
                EnsureFolder("Scenes");
                EnsureSprite("Scout", 180);
                EnsureSprite("Fighter", 180);
                EnsureSprite("Heavy", 180);
                EnsureSprite("Interceptor", 180);
                EnsureSprite("Bomber", 180);
                EnsureSprite("Projectile_Homing", 150);
                EnsureSprite("Projectile_Straight", 150);
                EnsureSprite("Projectile_Bomb", 150);
                EnsureSprite("Player_Demo", 180);

                EnemyProjectile homing = CreateProjectile("Projectile_Homing", 5.0f, 175f, 20);
                EnemyProjectile straight = CreateProjectile("Projectile_Straight", 7.0f, 0f, 16);
                EnemyProjectile bomb = CreateProjectile("Projectile_Bomb", 4.0f, 0f, 30);

                EnemyShip[] ships = new EnemyShip[5];
                ships[0] = CreateShip("Scout", EnemyShip.EnemyKind.Scout, 20, 3.1f, 2.6f, 0.65f, 100, 1f, homing, straight, bomb);
                ships[1] = CreateShip("Fighter", EnemyShip.EnemyKind.Fighter, 40, 2.0f, 3.4f, 0.35f, 180, 1.15f, homing, straight, bomb);
                ships[2] = CreateShip("Heavy", EnemyShip.EnemyKind.Heavy, 80, 0.95f, 4.0f, 0.15f, 350, 1.5f, homing, straight, bomb);
                ships[3] = CreateShip("Interceptor", EnemyShip.EnemyKind.Interceptor, 15, 3.7f, 1.6f, 0.7f, 130, 0.9f, homing, straight, bomb);
                ships[4] = CreateShip("Bomber", EnemyShip.EnemyKind.Bomber, 60, 1.3f, 3.1f, 0.2f, 260, 1.4f, homing, straight, bomb);

                string scenePath = Root + "Scenes/EnemyTest.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    MakeDemoScene(scenePath, ships);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Taeyang EnemyPack] Installed five enemy prefabs, three projectile prefabs and EnemyTest scene. No team scenes changed.");
            }
            finally { busy = false; }
        }

        [MenuItem("Tools/WarOfTheWorlds/Open Enemy Test Scene")]
        public static void OpenTest()
        {
            string path = Root + "Scenes/EnemyTest.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) Install();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static void EnsureFolder(string relative)
        {
            string path = Root + relative;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder("Assets/Taeyang", relative);
        }

        private static Sprite EnsureSprite(string filename, float pixelsPerUnit)
        {
            string path = Root + "Sprites/" + filename + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (importer == null) throw new System.Exception("Missing enemy sprite: " + path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != pixelsPerUnit)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static EnemyProjectile CreateProjectile(string file, float speed, float turning, int damage)
        {
            string path = Root + "Prefabs/" + file + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<EnemyProjectile>();
            GameObject obj = new GameObject(file);
            obj.tag = "Bullet";
            SpriteRenderer art = obj.AddComponent<SpriteRenderer>();
            art.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Sprites/" + file + ".png");
            art.sortingOrder = 15;
            obj.transform.localScale = file == "Projectile_Bomb" ? Vector3.one * 0.8f : Vector3.one * 0.65f;
            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.34f;
            EnemyProjectile missile = obj.AddComponent<EnemyProjectile>();
            missile.speed = speed;
            missile.rotationSpeed = turning;
            missile.enemyDamage = damage;
            missile.lifetime = 8.5f;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(obj, path);
            Object.DestroyImmediate(obj);
            return saved.GetComponent<EnemyProjectile>();
        }

        private static EnemyShip CreateShip(string file, EnemyShip.EnemyKind kind, int hp,
            float speed, float interval, float drift, int score, float size,
            EnemyProjectile homing, EnemyProjectile straight, EnemyProjectile bomb)
        {
            string path = Root + "Prefabs/Enemy_" + file + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<EnemyShip>();
            GameObject obj = new GameObject("Enemy_" + file);
            obj.transform.localScale = Vector3.one * size;
            SpriteRenderer art = obj.AddComponent<SpriteRenderer>();
            art.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Sprites/" + file + ".png");
            art.sortingOrder = 10;
            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = kind == EnemyShip.EnemyKind.Heavy ? 0.52f : 0.42f;
            EnemyShip ship = obj.AddComponent<EnemyShip>();
            ship.kind = kind;
            ship.maxHealth = hp;
            ship.moveSpeed = speed;
            ship.fireInterval = interval;
            ship.driftWidth = drift;
            ship.scoreValue = score;
            ship.homingProjectile = homing;
            ship.straightProjectile = straight;
            ship.bombProjectile = bomb;
            GameObject muzzle = new GameObject("FirePoint");
            muzzle.transform.SetParent(obj.transform, false);
            muzzle.transform.localPosition = new Vector3(0, -0.47f, 0);
            ship.firePoint = muzzle.transform;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(obj, path);
            Object.DestroyImmediate(obj);
            return saved.GetComponent<EnemyShip>();
        }

        private static void MakeDemoScene(string path, EnemyShip[] ships)
        {
            Scene original = SceneManager.GetActiveScene();
            Scene demo = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(demo);
            try
            {
                GameObject cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                Camera camera = cam.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.018f, 0.026f, 0.075f);
                cam.transform.position = new Vector3(0, 0, -10);
                cam.AddComponent<AudioListener>();

                GameObject hero = new GameObject("Demo Player (independent of Sanghyuk)");
                hero.tag = "Player";
                hero.transform.position = new Vector3(0, -3.5f, 0);
                SpriteRenderer heroArt = hero.AddComponent<SpriteRenderer>();
                heroArt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Sprites/Player_Demo.png");
                heroArt.sortingOrder = 10;
                Rigidbody2D heroBody = hero.AddComponent<Rigidbody2D>();
                heroBody.bodyType = RigidbodyType2D.Kinematic;
                heroBody.gravityScale = 0;
                CircleCollider2D heroCollider = hero.AddComponent<CircleCollider2D>();
                heroCollider.isTrigger = true;
                heroCollider.radius = 0.44f;
                DemoPlayer player = hero.AddComponent<DemoPlayer>();

                GameObject spawnerObject = new GameObject("Enemy Wave Spawner");
                EnemyWaveSpawner spawner = spawnerObject.AddComponent<EnemyWaveSpawner>();
                spawner.enemyPrefabs = ships;
                spawner.stage = 1;
                spawner.spawnY = 5.7f;

                GameObject hudObject = new GameObject("Enemy Demo HUD");
                EnemyDemoHUD hud = hudObject.AddComponent<EnemyDemoHUD>();
                hud.player = player;
                hud.spawner = spawner;

                EditorSceneManager.SaveScene(demo, path);
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(demo, true);
            }
        }
    }
}
#endif
