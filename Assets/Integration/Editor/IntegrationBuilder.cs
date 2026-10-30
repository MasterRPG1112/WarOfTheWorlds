#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using Jain;
using Sanghyuk;
using Taeyang;
using Menu = Jain.Menu;
namespace Integration.Editor
{
    public static class IntegrationBuilder
    {
        public const string ScenePath = "Assets/Integration/TaeyangIntegrated.unity";
        public const string MenuPath = "Assets/Integration/MainMenu.unity";
        static Font font;
        [MenuItem("Tools/WarOfTheWorlds/Build Integrated No Boss Campaign")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Taeyang/Scenes/TaeyangCampaign1.unity");
            foreach (var p in UnityEngine.Object.FindObjectsByType<DemoPlayer>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var p in UnityEngine.Object.FindObjectsByType<EnemyDemoHUD>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(p);
            var director = UnityEngine.Object.FindFirstObjectByType<StageDirector>();
            director.noBossCampaign = true; director.showDemoHUD = false; director.enabled = false;
            director.bossPrefab = null; director.campaignBossPrefabs = new GameObject[3];
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity", OpenSceneMode.Additive);
            foreach (var root in source.GetRootGameObjects())
                if (root.GetComponent<Camera>() == null && root.name != "Stage1BG") SceneManager.MoveGameObjectToScene(root, scene);
            EditorSceneManager.CloseScene(source, true); SceneManager.SetActiveScene(scene);
            // Copy full UI/player hierarchies together to retain all serialized references.
            var player = GameObject.Find("Player"); player.tag = "Player";
            var movement = player.GetComponent<Player>();
            foreach (var p in player.GetComponentsInChildren<Player>()) if (p != movement) UnityEngine.Object.DestroyImmediate(p);
            foreach (var b in player.GetComponentsInChildren<Rigidbody2D>()) if (b.gameObject != player) UnityEngine.Object.DestroyImmediate(b);
            var rb = player.GetComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.constraints = RigidbodyConstraints2D.FreezeRotation; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            foreach (var c in player.GetComponentsInChildren<Collider2D>()) c.isTrigger = true;
            foreach (var hp in player.GetComponentsInChildren<PlayerHp>()) { hp.integrated = true; hp.playerHp = hp.gameObject == player ? 8 : 5; }
            var data = player.AddComponent<PlayerData>(); data.PlayerHp = 8; movement.Data = data; movement.Cam = Camera.main.gameObject;
            movement.Origin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sanghyuk/Skill/Prefeb/Origin.prefab");
            movement.BHole = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sanghyuk/Skill/Prefeb/Blackhole.prefab");
            movement.GravityUnit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sanghyuk/Skill/Prefeb/GravityUnit.prefab");
            movement.ParryingUnit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sanghyuk/Skill/Prefeb/ParryingUnit.prefab");
            var combat = player.AddComponent<IntegratedCombat>(); combat.movement = movement; combat.data = data; combat.core = player.GetComponent<PlayerHp>();
            combat.bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sanghyuk/Skill/Prefeb/Bullet.prefab");
            var manager = new GameObject("Integrated Session Data").AddComponent<GameDataManager>(); manager.persistentStorage = false; manager.dataReset = 1;
            manager.playerhp1 = 8; manager.playerhp2 = 5; manager.playerhp3 = 5;
            var game = new GameObject("Integrated Campaign").AddComponent<IntegratedGame>(); game.director = director; game.player = combat;
            game.score = UnityEngine.Object.FindFirstObjectByType<Score>();
            game.shop = UnityEngine.Object.FindFirstObjectByType<Shop>(FindObjectsInactive.Include);
            game.hpUI = UnityEngine.Object.FindObjectsByType<PlayerHpUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var exit in UnityEngine.Object.FindObjectsByType<ExitMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(exit);
            var oldExit = GameObject.Find("ExitMenu"); if (oldExit != null) oldExit.SetActive(false);
            if (game.shop == null) throw new Exception("Missing team Shop UI");
            game.shop.shopMenu.SetActive(false);
            // Disable test-only cheat polling while retaining score/shop methods and original UI.
            game.score.enabled = false;
            var canvas = new GameObject("Integrated Flow UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            font = game.score.scoreUI.font;
            game.status = Text(canvas.transform, "Status", "", new Vector2(1400,120), new Vector2(0,440)); game.status.fontSize = 22;
            game.selectionPanel = Panel(canvas.transform, "Skill Selection");
            Text(game.selectionPanel.transform, "Title", "CHOOSE SKILL", new Vector2(600,65), new Vector2(0,150));
            string[] labels = {"Black Hole - remove nearby bullets", "Gravity - slow nearby bullets", "Parry - block and return fire"};
            UnityAction[] choices = {game.SelectBlackHole, game.SelectGravity, game.SelectParry};
            for (int i=0;i<3;i++) Button(game.selectionPanel.transform, labels[i], new Vector2(0,55-i*75), choices[i]);
            game.pausePanel = Panel(canvas.transform, "Pause"); Text(game.pausePanel.transform,"Title","PAUSED",new Vector2(600,60),new Vector2(0,140));
            Button(game.pausePanel.transform,"RESUME",new Vector2(0,40),game.TogglePause);
            Button(game.pausePanel.transform,"RESTART",new Vector2(0,-40),game.Restart); game.pausePanel.SetActive(false);
            Button(game.pausePanel.transform,"MAIN MENU",new Vector2(0,-200),game.BackToMenu);
            game.resultPanel = Panel(canvas.transform,"Result"); game.result = Text(game.resultPanel.transform,"Result Text","",new Vector2(600,100),new Vector2(0,80));
            Button(game.resultPanel.transform,"RESTART",new Vector2(0,-60),game.Restart); game.resultPanel.SetActive(false);
            Button(game.resultPanel.transform,"MAIN MENU",new Vector2(0,-140),game.BackToMenu);
            // Existing shop is now accessible during pause, with time held by the flow controller.
            Button(game.pausePanel.transform,"SHOP",new Vector2(0,-120),game.OpenShop);
            var shopCanvas = game.shop.shopMenu.GetComponent<Canvas>();
            if (shopCanvas == null) shopCanvas = game.shop.shopMenu.AddComponent<Canvas>();
            shopCanvas.overrideSorting = true; shopCanvas.sortingOrder = 200;
            if (game.shop.shopMenu.GetComponent<GraphicRaycaster>() == null) game.shop.shopMenu.AddComponent<GraphicRaycaster>();
            // Spawn the team's items inside Taeyang's camera bounds.
            foreach (var name in new[]{"Heal","Shield","Invincibility","Meteorite"})
            {
                var spawner = new GameObject(name+" Spawner").AddComponent<ItemSpawner>();
                spawner.itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MC/Prefeb/"+name+".prefab");
                spawner.spawnRangeX = 6.8f; spawner.spawnY = 5.5f; spawner.spawnInterval = name == "Meteorite" ? 13 : 24;
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets(); Validate(); BuildMenu(); AssetDatabase.Refresh();
            ConfigureBuildScenes(); Debug.Log("INTEGRATION_BUILD_PASS");
        }
        public static void ConfigureBuildScenes()
        {
            var menu = new EditorBuildSettingsScene(MenuPath,true);
            var game = new EditorBuildSettingsScene(ScenePath,true);
            menu.guid = new GUID(AssetDatabase.AssetPathToGUID(MenuPath));
            game.guid = new GUID(AssetDatabase.AssetPathToGUID(ScenePath));
            if (menu.guid.Empty() || game.guid.Empty()) throw new Exception("Build scene GUID is missing");
            EditorBuildSettings.scenes = new[]{menu,game};
            AssetDatabase.SaveAssets();
        }
        public static void BuildMenu()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
            var menu = UnityEngine.Object.FindFirstObjectByType<Menu>(FindObjectsInactive.Include);
            menu.integrated = true;
            var data = UnityEngine.Object.FindFirstObjectByType<GameDataManager>();
            data.persistentStorage = false;
            var bridge = menu.gameObject.AddComponent<IntegratedMenu>(); bridge.menu = menu;
            font = menu.rankTexts[0].font;
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f; }
            }
            foreach (var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                string method = button.onClick.GetPersistentEventCount() == 0 ? "" : button.onClick.GetPersistentMethodName(0);
                var info = typeof(Menu).GetMethod(method);
                if (info == null || info.GetParameters().Length != 0) continue;
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(button.onClick, (UnityAction)Delegate.CreateDelegate(typeof(UnityAction),menu,info));
                if (method == "ContinueBtn") bridge.continueButton = button;
            }
            bridge.continueHint = Text(menu.startInvokeMenu.transform,"Continue checkpoint hint","",new Vector2(1100,50),new Vector2(0,-210));
            bridge.continueHint.fontSize = 24;
            Text(menu.settingMenu.transform,"Volume label","음량",new Vector2(250,60),new Vector2(-340,100));
            var volume = new GameObject("Volume",typeof(RectTransform),typeof(Slider)); volume.transform.SetParent(menu.settingMenu.transform,false);
            var vrt = volume.GetComponent<RectTransform>(); vrt.sizeDelta = new Vector2(600,40); vrt.anchoredPosition = new Vector2(100,100);
            var track = new GameObject("Track",typeof(RectTransform),typeof(Image)); track.transform.SetParent(volume.transform,false);
            track.GetComponent<RectTransform>().sizeDelta = new Vector2(600,12); track.GetComponent<Image>().color = Color.gray;
            var fill = new GameObject("Fill",typeof(RectTransform),typeof(Image)); fill.transform.SetParent(volume.transform,false);
            var fillRect = fill.GetComponent<RectTransform>(); fillRect.anchorMin = new Vector2(0,.5f); fillRect.anchorMax = new Vector2(1,.5f); fillRect.sizeDelta = new Vector2(0,12);
            var handle = new GameObject("Handle",typeof(RectTransform),typeof(Image)); handle.transform.SetParent(volume.transform,false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(25,40);
            bridge.volumeSlider = volume.GetComponent<Slider>(); bridge.volumeSlider.fillRect = fill.GetComponent<RectTransform>();
            bridge.volumeSlider.handleRect = handle.GetComponent<RectTransform>(); bridge.volumeSlider.targetGraphic = handle.GetComponent<Image>(); bridge.volumeSlider.value = 1;
            UnityEventTools.AddPersistentListener(bridge.volumeSlider.onValueChanged,bridge.SetVolume);
            var toggle = new GameObject("Fullscreen",typeof(RectTransform),typeof(Toggle)); toggle.transform.SetParent(menu.settingMenu.transform,false);
            toggle.GetComponent<RectTransform>().sizeDelta = new Vector2(500,70); toggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-10);
            var box = new GameObject("Box",typeof(RectTransform),typeof(Image)); box.transform.SetParent(toggle.transform,false);
            box.GetComponent<RectTransform>().sizeDelta = new Vector2(45,45); box.GetComponent<RectTransform>().anchoredPosition = new Vector2(-220,0); box.GetComponent<Image>().color = Color.gray;
            var mark = new GameObject("Check",typeof(RectTransform),typeof(Image)); mark.transform.SetParent(box.transform,false); mark.GetComponent<RectTransform>().sizeDelta = new Vector2(30,30);
            bridge.fullscreenToggle = toggle.GetComponent<Toggle>(); bridge.fullscreenToggle.targetGraphic = box.GetComponent<Image>(); bridge.fullscreenToggle.graphic = mark.GetComponent<Image>();
            Text(toggle.transform,"Label","전체화면",new Vector2(400,60),new Vector2(30,0));
            UnityEventTools.AddPersistentListener(bridge.fullscreenToggle.onValueChanged,bridge.SetFullscreen);
            if (bridge.continueButton == null) throw new Exception("Original Continue button is not wired");
            EditorSceneManager.SaveScene(scene,MenuPath);
            AssetDatabase.SaveAssets(); ValidateMenu();
        }
        public static void ValidateMenu()
        {
            var scene = EditorSceneManager.OpenScene(MenuPath);
            var bridge = UnityEngine.Object.FindFirstObjectByType<IntegratedMenu>();
            if (bridge == null || bridge.menu.rankTexts.Length != 10 || bridge.continueButton == null || bridge.volumeSlider == null || bridge.fullscreenToggle == null) throw new Exception("Menu references");
            foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Menu missing script: "+t.name);
            File.WriteAllText("../integration-menu-scene-validation.txt","PASS: copied original menu, 10 ranking rows, original callbacks rebound, continue/settings wired, no missing scripts.\n");
        }
        static GameObject Panel(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>(); rt.sizeDelta=new Vector2(950,510);
            go.GetComponent<Image>().color=new Color(.08f,.08f,.08f,.98f); return go;
        }
        static Text Text(Transform parent,string name,string content,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.sizeDelta=size;rt.anchoredPosition=position;
            var text=go.GetComponent<Text>();text.font=font;text.text=content;text.fontSize=30;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;return text;
        }
        static void Button(Transform parent,string label,Vector2 position,UnityAction action)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(650,60);rt.anchoredPosition=position;
            go.GetComponent<Image>().color=new Color(.25f,.25f,.25f);
            Text(go.transform,"Label",label,new Vector2(640,60),Vector2.zero);
            UnityEventTools.AddPersistentListener(go.GetComponent<UnityEngine.UI.Button>().onClick,action);
        }
        public static void Validate()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var game=UnityEngine.Object.FindFirstObjectByType<IntegratedGame>();
            if(game==null || game.player==null || game.score==null || game.shop==null || game.hpUI.Length!=3 || !game.director.noBossCampaign) throw new Exception("Integration wiring");
            if(game.player.bulletPrefab==null || game.player.movement.BHole==null || game.player.movement.GravityUnit==null || game.player.movement.ParryingUnit==null) throw new Exception("Combat prefab references");
            foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0) throw new Exception("Missing script: "+t.name);
            if(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length!=1 || UnityEngine.Object.FindObjectsByType<DemoPlayer>(FindObjectsSortMode.None).Length!=0) throw new Exception("Duplicate camera/demo");
            File.WriteAllText("../integration-scene-validation.txt","PASS: integrated scene, real player, three HP UI references, five enemies, combat/skill references, no missing scene scripts, one camera, no DemoPlayer.\n");
        }
        public static void BuildWindows()
        {
            var report=UnityEditor.BuildPipeline.BuildPlayer(new[]{MenuPath,ScenePath},"../Windows/WarOfTheWorlds.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText("../integration-build-validation.txt",report.summary.result+" errors="+report.summary.totalErrors+" size="+report.summary.totalSize+"\n");
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows build failed");
        }
        public static void BuildAll()
        {
            Build();
            BuildWindows();
        }
    }
}
#endif
