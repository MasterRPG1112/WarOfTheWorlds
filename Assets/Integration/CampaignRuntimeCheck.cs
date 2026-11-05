using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Integration
{
    // Opt-in validation of the actual Windows player; dormant in normal play.
    public class CampaignRuntimeCheck : MonoBehaviour
    {
        string directory;
        int errors, captures;
        readonly StringBuilder report = new StringBuilder();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--campaign-check");
            if (index < 0 || index + 1 >= args.Length) return;
            var check = new GameObject("Opt-in current campaign validation").AddComponent<CampaignRuntimeCheck>();
            DontDestroyOnLoad(check.gameObject);
            check.directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(check.directory);
            Application.runInBackground = true;
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            report.AppendLine("RUNTIME ERROR " + message).AppendLine(stack);
        }
        IEnumerator Start()
        {
            report.AppendLine("Actual Windows player: menu, opening, campaign and inventory.")
                .AppendLine("GPU: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType)
                .AppendLine("Isolated test save. Programmatic callbacks; no keyboard or pointer claims.");
            var run = Run();
            while (true)
            {
                object next = null;
                bool more = false;
                try { more = run.MoveNext(); if (more) next = run.Current; }
                catch (Exception exception) { errors++; report.AppendLine("FAIL " + exception); break; }
                if (!more) break;
                yield return next;
            }
            report.AppendLine("Runtime errors/failures=" + errors + "; GPU captures=" + captures);
            File.WriteAllText(Path.Combine(directory, "result.txt"), report.ToString());
            Application.Quit(errors == 0 ? 0 : 1);
        }
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            report.AppendLine("PASS " + label);
        }
        void Capture(string name)
        {
            string error;
            if (!FrameCapture.TryCapture(Path.Combine(directory, name + ".png"), out error, 1280, 720))
                throw new InvalidOperationException("GPU capture " + name + ": " + error);
            captures++;
            report.AppendLine("PASS GPU capture " + name);
        }
        void ClickPart(IntegratedGame game, string method)
        {
            foreach (var button in game.shop.shopMenu.GetComponentsInChildren<Button>(true))
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    if (button.gameObject.activeInHierarchy && button.onClick.GetPersistentTarget(i) == game.shop &&
                        button.onClick.GetPersistentMethodName(i) == method)
                    { button.onClick.Invoke(); return; }
            throw new InvalidOperationException("No visible existing shop button bound to " + method);
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            var menu = FindFirstObjectByType<IntegratedMenu>();
            Check(menu != null && menu.menu != null && menu.menu.integrated, "Existing integrated menu loads");
            Capture("01-menu");
            menu.menu.ReStartBtn();
            yield return new WaitForSecondsRealtime(1);
            var game = IntegratedGame.Active;
            Check(game != null && !game.Running && game.GetComponent<CampaignNarrative>().Opening, "New-game callback opens the story");
            Capture("02-opening");
            game.GetComponent<CampaignNarrative>().FinishOpening();
            yield return new WaitForSecondsRealtime(1);
            Check(game.Running && game.director.stageNumber == 1 && game.player.Health == 8, "Skipping starts stage1 with HP8");
            Capture("03-stage1");
            var hud = FindFirstObjectByType<DesignHud>();
            Check(hud != null, "Existing HUD connected");
            hud.OpenPatches();
            yield return new WaitForSecondsRealtime(.3f);
            Check(!game.Running && Time.timeScale == 0, "Inventory callback pauses play");
            Capture("04-inventory");
            hud.ClosePatches();
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.Running && Time.timeScale == 1, "Closing inventory resumes play");
            yield return new WaitForSecondsRealtime(3);
            Check(FindObjectsByType<Taeyang.EnemyShip>(FindObjectsSortMode.None).Length > 0, "Normal stage enemies spawn in the actual player");
            Capture("05-stage1-enemies");
            game.TogglePause();
            yield return new WaitForSecondsRealtime(.2f);
            Check(!game.Running && game.pausePanel.activeInHierarchy, "Pause panel opens");
            Capture("06-pause");
            var shopControl = game.pausePanel.transform.Find("Shop and parts");
            Check(shopControl != null, "Pause menu includes the shop and parts action");
            shopControl.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Check(game.ShopAccessible && !game.BetweenStages && game.shop.shopMenu.activeInHierarchy && !game.Running,
                "Shop is accessible from pause in stage1");
            Capture("07-shop");
            game.shop.coin = 2000;
            game.shop.RefreshUI();
            ClickPart(game, "SpeedBtn");
            Check(game.shop.IsPartEquipped(1) && game.player.movement.speed == 7,
                "Part purchase equips turbo and applies its speed");
            Capture("08-part-equipped");
            ClickPart(game, "SpeedBtn");
            Check(game.shop.part2 == 1 && !game.shop.IsPartEquipped(1) && game.player.movement.speed == 5,
                "Owned part can be unequipped and effect removed");
            Capture("09-part-unequipped");
            var cheats = game.GetComponent<CampaignCheats>();
            Check(!cheats.Allowed, "Cheats default disabled");
            if (cheats.CheatModeAvailable)
            {
                Check(cheats.ToggleDevelopmentMode(), "Development mode can be enabled");
                int coins = game.shop.coin;
                Check(cheats.ExecuteCheat(3) && game.shop.coin == coins + cheats.moneyAmount, "Development money cheat works in pause shop");
                Check(!cheats.ToggleDevelopmentMode() && !game.RankedRun, "Practice status survives disabling cheats");
            }
            else Check(!cheats.ToggleDevelopmentMode() && !cheats.ExecuteCheat(3), "Release build rejects cheat activation");
            game.CloseShop();
            Check(game.pausePanel.activeInHierarchy && !game.Running, "Shop back returns to pause controls");
            game.BackToMenu();
            yield return new WaitForSecondsRealtime(1);
            menu = FindFirstObjectByType<IntegratedMenu>();
            Check(menu != null && menu.continueButton.interactable, "Menu return retains checkpoint continuation");
            Check(errors == 0, "No native player runtime errors during the flow");
        }
    }
}
