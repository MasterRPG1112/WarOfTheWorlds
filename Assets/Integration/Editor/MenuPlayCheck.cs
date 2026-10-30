#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Jain;
using UObject = UnityEngine.Object;

namespace Integration.Editor
{
    // Drives the actual serialized menu callbacks in Play Mode; no pointer/UI capture claims.
    [InitializeOnLoad]
    public static class MenuPlayCheck
    {
        const string Flag = "Integration.MenuCheck";
        static IEnumerator checks;
        static string report = "";
        static int errors, searchErrors, exitCode;
        static double deadline;
        static float previousVolume;
        static bool isolated;

        static MenuPlayCheck()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
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
            EditorSceneManager.OpenScene(IntegrationBuilder.MenuPath);
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = ""; errors = 0; searchErrors = 0; exitCode = 0;
                deadline = EditorApplication.timeSinceStartup + 90;
                previousVolume = AudioListener.volume;
                try
                {
                    CheckDiskStore();
                    IntegrationSaveStore.ResetForTests(); isolated = true;
                    checks = Check();
                }
                catch (Exception exception)
                {
                    report += "FAIL: " + exception + "\n";
                    exitCode = 1; Finish();
                }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (checks != null)
                {
                    report += "FAIL: Play Mode ended before the menu checks completed.\n";
                    exitCode = 1; checks = null; RestoreIsolation();
                }
                SessionState.SetBool(Flag, false);
                File.WriteAllText("../integration-menu-play-validation.txt",
                    report + "Runtime errors=" + errors + "; Editor Search exceptions=" + searchErrors + "\n");
                EditorApplication.Exit(exitCode);
            }
        }

        static void Tick()
        {
            if (checks == null || !EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Menu Play Mode timeout");
                if (checks.MoveNext()) return;
                Assert(errors == 0, "Zero game runtime errors during menu flow");
                exitCode = 0; Finish();
            }
            catch (Exception exception)
            {
                report += "FAIL: " + exception + "\n";
                exitCode = 1; Finish();
            }
        }

        static void Finish()
        {
            checks = null; RestoreIsolation(); Time.timeScale = 1;
            EditorApplication.isPlaying = false;
        }

        static void RestoreIsolation()
        {
            if (!isolated) return;
            IntegrationSaveStore.RestoreAfterTests(); isolated = false;
            AudioListener.volume = previousVolume;
        }

        static void Assert(bool value, string label)
        {
            if (!value) throw new Exception(label);
            report += "PASS: " + label + "\n";
            File.WriteAllText("../integration-menu-play-progress.txt", report);
            Debug.Log("INTEGRATION_MENU_CHECK " + label);
        }

        static Button Callback(UObject target, string method, GameObject panel = null)
        {
            Button[] buttons = panel == null
                ? UObject.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                : panel.GetComponentsInChildren<Button>(true);
            var matches = buttons.Where(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(index => button.onClick.GetPersistentTarget(index) == target &&
                              button.onClick.GetPersistentMethodName(index) == method)).ToArray();
            if (matches.Length != 1) throw new Exception("Expected one serialized " + method + " button, found " + matches.Length);
            return matches[0];
        }

        static void FreezeGame(IntegratedGame game)
        {
            game.director.enabled = false;
            game.player.data.Invin = true;
            foreach (var spawner in UObject.FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None))
            { spawner.CancelInvoke(); spawner.enabled = false; }
        }

        static void CheckDiskStore()
        {
            string previousOverride = IntegrationSaveStore.ValidationPathOverride;
            bool previousSuppression = IntegrationSaveStore.SuppressDiskWrites;
            bool previousContinue = IntegrationSaveStore.ContinueRequested;
            string work = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "work"));
            string path = Path.Combine(work, "menu-save-validation.json");
            Directory.CreateDirectory(work);
            try
            {
                IntegrationSaveStore.ValidationPathOverride = path;
                IntegrationSaveStore.SuppressDiskWrites = false;
                IntegrationSaveStore.Reload();
                IntegrationSaveStore.Data.checkpoint = new Checkpoint
                { stage = 2, coreHp = 6, leftHp = 4, rightHp = 3, score = 123, money = 456, pull = 2, selectedSkill = 1 };
                IntegrationSaveStore.Data.volume = .37f;
                IntegrationSaveStore.Data.fullscreen = true;
                IntegrationSaveStore.Data.ranks.Clear();
                IntegrationSaveStore.Data.ranks.Add(new GameDataManager.RankData("Player", 123));
                IntegrationSaveStore.Save();
                Assert(File.Exists(path), "Physical save file is written only to the validation work path");
                IntegrationSaveStore.Reload();
                Assert(IntegrationSaveStore.HasCheckpoint && IntegrationSaveStore.Data.checkpoint.stage == 2 &&
                       IntegrationSaveStore.Data.checkpoint.coreHp == 6 && IntegrationSaveStore.Data.checkpoint.money == 456 &&
                       Mathf.Abs(IntegrationSaveStore.Data.volume - .37f) < .001f &&
                       IntegrationSaveStore.Data.fullscreen && IntegrationSaveStore.Data.ranks[0].stageScore == 123,
                    "Physical JSON reload restores checkpoint, settings and ranking");
                IntegrationSaveStore.Data.checkpoint.money = 789;
                IntegrationSaveStore.Save(); IntegrationSaveStore.Reload();
                Assert(IntegrationSaveStore.Data.checkpoint.money == 789 && File.Exists(path + ".previous"),
                    "Repeated physical save atomically replaces the file and keeps a previous backup");
                const string corrupt = "{this is not valid JSON";
                File.WriteAllText(path, corrupt);
                IntegrationSaveStore.Reload();
                Assert(!IntegrationSaveStore.HasCheckpoint && IntegrationSaveStore.Data.ranks.Count == 0 &&
                       Mathf.Abs(IntegrationSaveStore.Data.volume - 1) < .001f && File.ReadAllText(path) == corrupt,
                    "Corrupt validation save falls back safely while retaining the original file");
                IntegrationSaveStore.Save();
                Assert(Directory.GetFiles(work, "menu-save-validation.json.rejected-*")
                        .Any(file => File.ReadAllText(file) == corrupt),
                    "Repairing a corrupt validation save preserves it in a unique rejected backup");
                report += "Physical save evidence: " + path + " (validation-only file and backups retained).\n";
            }
            finally
            {
                IntegrationSaveStore.ValidationPathOverride = previousOverride;
                IntegrationSaveStore.SuppressDiskWrites = previousSuppression;
                IntegrationSaveStore.ContinueRequested = previousContinue;
                IntegrationSaveStore.Reload();
            }
        }

        static IEnumerator Check()
        {
            // Reload after test isolation so the real menu Start sees the empty test save.
            SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
            var bridge = UObject.FindFirstObjectByType<IntegratedMenu>();
            var menu = bridge.menu;
            Assert(SceneManager.GetActiveScene().path == IntegrationBuilder.MenuPath && menu.integrated,
                "Build menu opens the copied original menu scene");
            Assert(menu.basicMenu.activeSelf && !menu.settingMenu.activeSelf && !menu.listMenu.activeSelf && !menu.startInvokeMenu.activeSelf,
                "Original basic menu is the initial panel");
            Assert(!IntegrationSaveStore.HasCheckpoint && !bridge.continueButton.interactable,
                "Continue is disabled without a checkpoint");

            string[] originalMethods = { "StartBtn", "SettingBtn", "ListBtn", "ExitBtn", "ReStartBtn", "ContinueBtn", "BackBtn1", "BackBtn2", "BackBtn3" };
            foreach (string method in originalMethods)
                Assert(Callback(menu, method) != null, "Original callback is rebound: " + method);

            // Inspect Quit's actual target/method above; never terminate the test process via Quit.
            Time.timeScale = 0;
            Callback(menu, "StartBtn").onClick.Invoke();
            double until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(menu.startInvokeMenu.activeSelf && !menu.basicMenu.activeSelf &&
                   menu.startInvokeMenu.GetComponent<CanvasGroup>().interactable,
                "Original Start button completes its fade even at timeScale zero");
            Callback(menu, "BackBtn3").onClick.Invoke();
            until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(menu.basicMenu.activeSelf && !menu.startInvokeMenu.activeSelf,
                "Original start-panel Back button returns to basic menu");
            Time.timeScale = 1;
            Callback(menu, "StartBtn").onClick.Invoke();
            until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Callback(menu, "ReStartBtn").onClick.Invoke(); yield return null; yield return null;
            var game = UObject.FindFirstObjectByType<IntegratedGame>();
            Assert(game != null && SceneManager.GetActiveScene().path == IntegrationBuilder.ScenePath && !game.Running,
                "Original New Game button opens the integrated game skill selection");
            FreezeGame(game);
            game.selectionPanel.GetComponentsInChildren<Button>()[0].onClick.Invoke();
            FreezeGame(game); yield return null;
            Assert(game.Running && game.director.stageNumber == 1 && game.player.data.PlayerHp == 8,
                "New game starts stage one with clean player state");
            Assert(IntegrationSaveStore.HasCheckpoint && IntegrationSaveStore.Data.checkpoint.stage == 1,
                "Starting the stage creates a stage-start checkpoint");
            game.TogglePause();
            Callback(game, "BackToMenu", game.pausePanel).onClick.Invoke(); yield return null; yield return null;
            bridge = UObject.FindFirstObjectByType<IntegratedMenu>(); menu = bridge.menu;
            Assert(Time.timeScale == 1 && menu.basicMenu.activeSelf && bridge.continueButton.interactable,
                "Pause Main Menu callback restores time and enables continuation");

            IntegrationSaveStore.Data.checkpoint = new Checkpoint
            {
                stage = 2, coreHp = 6, leftHp = 4, rightHp = 3,
                score = 123, money = 456, pull = 2, selectedSkill = 1
            };
            IntegrationSaveStore.Save(); IntegrationSaveStore.Reload();
            SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
            bridge = UObject.FindFirstObjectByType<IntegratedMenu>(); menu = bridge.menu;
            Assert(bridge.continueButton.interactable && bridge.continueHint.text.Contains("2"),
                "Reloaded menu identifies the saved stage-start checkpoint");
            Callback(menu, "StartBtn").onClick.Invoke();
            until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Callback(menu, "ContinueBtn").onClick.Invoke(); yield return null; yield return null;
            game = UObject.FindFirstObjectByType<IntegratedGame>(); FreezeGame(game);
            var wings = game.player.GetComponentsInChildren<PlayerHp>();
            Assert(game.director.stageNumber == 2 && game.director.Elapsed == 0 && !game.Running,
                "Original Continue button opens stage two at the stage start");
            Assert(game.player.data.PlayerHp == 6 && game.player.core.playerHp == 6 &&
                   wings.Single(hp => hp.name == "Point2").playerHp == 4 &&
                   wings.Single(hp => hp.name == "Point3").playerHp == 3,
                "Continuation restores all three HP segments");
            Assert(game.score.score == 123 && game.shop.coin == 456 && game.shop.pull == 2 && game.player.selectedSkill == 1,
                "Continuation restores score, money, skill stock and selected skill");
            game.selectionPanel.GetComponentsInChildren<Button>()[1].onClick.Invoke(); FreezeGame(game);
            game.End(true);
            Assert(game.Won && game.Ended && game.resultPanel.activeSelf && !IntegrationSaveStore.HasCheckpoint,
                "Campaign result invalidates continuation and opens the result panel");
            int rankCount = IntegrationSaveStore.Data.ranks.Count;
            game.End(true);
            Assert(rankCount == 1 && IntegrationSaveStore.Data.ranks.Count == rankCount &&
                   IntegrationSaveStore.Data.ranks[0].stageScore == 123,
                "Result is recorded once even if End is called twice");
            Callback(game, "BackToMenu", game.resultPanel).onClick.Invoke(); yield return null; yield return null;
            bridge = UObject.FindFirstObjectByType<IntegratedMenu>(); menu = bridge.menu;
            Assert(!bridge.continueButton.interactable && Time.timeScale == 1,
                "Result Main Menu callback returns safely without stale continuation");
            Callback(menu, "ListBtn").onClick.Invoke();
            until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(menu.listMenu.activeSelf && menu.rankTexts[0].text.Contains("123") &&
                   menu.rankTexts[0].text.Contains("Player"),
                "Original ranking panel displays the actual integrated result");
            Callback(menu, "BackBtn2").onClick.Invoke();
            until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(menu.basicMenu.activeSelf && !menu.listMenu.activeSelf,
                "Original ranking Back button returns to basic menu");
            Callback(menu, "SettingBtn").onClick.Invoke();
            until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(menu.settingMenu.activeSelf && !menu.basicMenu.activeSelf,
                "Original Settings button opens the settings panel");
            bool fullScreen = !bridge.fullscreenToggle.isOn;
            bridge.volumeSlider.value = .37f;
            bridge.fullscreenToggle.isOn = fullScreen;
            IntegrationSaveStore.Reload();
            Assert(Mathf.Abs(IntegrationSaveStore.Data.volume - .37f) < .001f &&
                   Mathf.Abs(AudioListener.volume - .37f) < .001f &&
                   IntegrationSaveStore.Data.fullscreen == fullScreen,
                "Slider/toggle serialized callbacks apply volume and retain settings across save/reload");
            Callback(menu, "BackBtn1").onClick.Invoke();
            until = EditorApplication.timeSinceStartup + .7;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert(menu.basicMenu.activeSelf && !menu.settingMenu.activeSelf,
                "Original settings Back button returns to basic menu");
            report += "LIMIT: physical serialization, replacement and corrupt-file recovery use a separate validation file; menu scene tests use the memory store. Existing user save is untouched.\n";
            report += "LIMIT: this Editor run validates serialized callbacks and scene state, not pointer interaction, actual screen rendering, audio listening or standalone fullscreen switching.\n";
            report += "LIMIT: Quit binding is inspected without calling Application.Quit; checkpoint continuation restarts the saved stage rather than resuming mid-wave.\n";
        }
    }
}
#endif
