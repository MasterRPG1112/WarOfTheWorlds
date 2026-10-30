using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Integration
{
    // Opt-in executable verification. Dormant during ordinary play.
    public class RuntimeSmoke : MonoBehaviour
    {
        string directory;
        int errors, captureErrors, captures;
        bool saveSandboxed;
        readonly StringBuilder report = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--integration-smoke");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            var probe = new GameObject("Opt-in build verification").AddComponent<RuntimeSmoke>();
            DontDestroyOnLoad(probe.gameObject);
            probe.directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(probe.directory);
            IntegrationSaveStore.ResetForTests();
            probe.saveSandboxed = true;
        }

        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void OnDestroy() { RestoreSaveSandbox(); }
        void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            errors++;
            report.AppendLine("RUNTIME ERROR: " + message).AppendLine(stack);
        }

        IEnumerator Start()
        {
            report.AppendLine("Graphics-enabled Windows menu/game smoke verification")
                .AppendLine("Device: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType)
                .AppendLine("Capture: actual GPU camera render plus existing UI at 1600x900; independent of native window swap chain.");
            var run = Run();
            bool failed = false;
            while (true)
            {
                object step;
                try
                {
                    if (!run.MoveNext()) break;
                    step = run.Current;
                }
                catch (Exception exception)
                {
                    failed = true;
                    report.AppendLine("SMOKE FAILURE: " + exception);
                    break;
                }
                yield return step;
            }
            RestoreSaveSandbox();
            report.AppendLine("Runtime errors=" + errors)
                .AppendLine("Frame captures=" + captures + "; capture failures=" + captureErrors)
                .AppendLine("User save file was not written; test state was restored.")
                .AppendLine("Automated API/button-callback verification; no human keyboard play or balance review.");
            bool passed = !failed && errors == 0 && captureErrors == 0;
            report.AppendLine(passed ? "RESULT: PASS" : "RESULT: FAIL");
            File.WriteAllText(Path.Combine(directory, "runtime-smoke.txt"), report.ToString());
            yield return null;
            Application.Quit(passed ? 0 : 1);
        }

        IEnumerator Run()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            var menu = UnityEngine.Object.FindFirstObjectByType<IntegratedMenu>();
            Require(menu != null && SceneManager.GetActiveScene().name == "MainMenu", "Original main menu is the executable entry scene");
            Require(!menu.continueButton.interactable, "No-save Continue is disabled");
            Capture("01-main-menu.png");

            // The original panel-transition methods remain in use.
            menu.menu.SettingBtn();
            yield return new WaitForSecondsRealtime(.7f);
            Require(menu.menu.settingMenu.activeSelf, "Original settings button opens its panel");
            menu.volumeSlider.value = .37f;
            Require(Mathf.Abs(AudioListener.volume - .37f) < .01f, "Settings slider reaches the master volume");
            Capture("02-settings.png");
            menu.menu.BackBtn1();
            yield return new WaitForSecondsRealtime(.7f);
            menu.menu.ListBtn();
            yield return new WaitForSecondsRealtime(.7f);
            Require(menu.menu.listMenu.activeSelf, "Original ranking button opens its panel");
            Capture("03-ranking-empty.png");
            menu.menu.BackBtn2();
            yield return new WaitForSecondsRealtime(.7f);
            menu.menu.StartBtn();
            yield return new WaitForSecondsRealtime(.7f);
            Require(menu.menu.startInvokeMenu.activeSelf, "Original start button opens new/continue selection");
            Capture("04-start-choice.png");
            menu.menu.ReStartBtn();
            yield return null;
            yield return null;

            var game = IntegratedGame.Active;
            Require(game != null && SceneManager.GetActiveScene().name == "TaeyangIntegrated", "New Game loads integrated Taeyang scene");
            Capture("05-skill-selection.png");
            game.selectionPanel.GetComponentsInChildren<Button>()[0].onClick.Invoke();
            Require(game.Running, "Serialized skill button starts actual gameplay");
            // The full gameplay suite tests lethal damage separately. Keep this visual run alive.
            float gameplayUntil = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < gameplayUntil)
            {
                game.player.data.Invin = true;
                game.player.data.PlayerHp = 8;
                game.player.core.playerHp = 8;
                yield return null;
            }
            report.AppendLine("Visual-run assistance: player invulnerability/HP replenishment during 10-second gameplay capture.");
            game.player.TrySkill();
            Require(game.player.shotsFired > 0 && game.director.Elapsed > 0, "Standalone gameplay advances and automatically fires");
            report.AppendLine("Observed stage=" + game.director.stageNumber + "; elapsed=" + game.director.Elapsed +
                "; shots=" + game.player.shotsFired + "; kills=" + game.Kills);
            Capture("06-gameplay.png");
            game.TogglePause();
            Require(!game.Running && Time.timeScale == 0, "Pause stops standalone gameplay");
            Capture("07-pause.png");
            game.BackToMenu();
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);

            menu = UnityEngine.Object.FindFirstObjectByType<IntegratedMenu>();
            Require(menu != null && Time.timeScale == 1, "Pause-to-menu restores original menu and time scale");
            Require(menu.continueButton.interactable, "Saved stage checkpoint enables Continue");
            menu.menu.StartBtn();
            yield return new WaitForSecondsRealtime(.7f);
            Capture("08-continue-available.png");
            menu.menu.ContinueBtn();
            yield return null;
            yield return null;
            game = IntegratedGame.Active;
            Require(game != null && game.director.stageNumber == 1, "Continue returns to the saved stage start");
            if (!game.Running) game.SelectSkill(0);
            Capture("09-resumed-game.png");
            report.AppendLine("Result navigation is exercised by calling End(false); natural lethal damage is covered by the separate Play Mode suite.");
            game.End(false);
            Capture("10-game-result.png");
            game.BackToMenu();
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            menu = UnityEngine.Object.FindFirstObjectByType<IntegratedMenu>();
            Require(menu != null, "Game result returns to original menu");
            menu.menu.ListBtn();
            yield return new WaitForSecondsRealtime(.7f);
            Require(IntegrationSaveStore.Data.ranks.Count > 0, "Completed run is available to original ranking UI");
            Capture("11-ranking-result.png");
            menu.menu.BackBtn2();
            yield return new WaitForSecondsRealtime(.7f);
            menu.menu.SettingBtn();
            yield return new WaitForSecondsRealtime(.7f);
            Require(Mathf.Abs(menu.volumeSlider.value - .37f) < .01f, "Menu settings survive game/menu transitions");
            Capture("12-settings-restored.png");
        }

        void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            report.AppendLine("PASS: " + label);
        }

        void Capture(string name)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--no-frame-capture") >= 0)
            { report.AppendLine("CAPTURE SKIPPED: " + name); return; }
            if (FrameCapture.TryCapture(Path.Combine(directory, name), out string error))
            { captures++; report.AppendLine("CAPTURE: " + name); }
            else
            { captureErrors++; report.AppendLine("CAPTURE FAILURE: " + name + "\n" + error); }
        }

        void RestoreSaveSandbox()
        {
            if (!saveSandboxed) return;
            IntegrationSaveStore.RestoreAfterTests();
            saveSandboxed = false;
        }
    }
}
