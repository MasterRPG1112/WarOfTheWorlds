using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Taeyang;
using Jain;
namespace Integration
{
    public class IntegratedGame : MonoBehaviour
    {
        public static IntegratedGame Active;
        public StageDirector director;
        public IntegratedCombat player;
        public Score score;
        public Shop shop;
        public PlayerHpUI[] hpUI;
        // Kept for older serialized scenes; the campaign now starts immediately.
        public GameObject pausePanel, selectionPanel, resultPanel, intermissionPanel;
        public Text status, result;
        public bool Running => started && !paused && !ended && !betweenStages;
        public bool BetweenStages => betweenStages;
        public int Kills { get; private set; }
        public bool Won { get; private set; }
        public bool Ended => ended;
        float frozenUntil;
        public bool EnemiesFrozen => false; // Time stop affects missiles only.
        public bool MissilesFrozen => Running && Time.time < frozenUntil;
        bool started, paused, ended, betweenStages;

        void Start()
        {
            shop.Initialize();
            // Retain the original shop buttons while disabling cheat-key polling.
            shop.enabled = false;
            if (IntegrationSaveStore.ContinueRequested && IntegrationSaveStore.HasCheckpoint)
                RestoreCheckpoint(IntegrationSaveStore.Data.checkpoint);
            else
                InitializeNewRun();
            IntegrationSaveStore.ContinueRequested = false;
            RefreshCounters();
            StartGame();
        }
        void Awake() { Active = this; Time.timeScale = 0; gameObject.AddComponent<CampaignTools>(); }
        void OnEnable() { EnemyShip.DestroyedByPlayer += Reward; }
        void OnDisable() { EnemyShip.DestroyedByPlayer -= Reward; }
        void OnDestroy() { if (Active == this) Active = null; Time.timeScale = 1; }

        void InitializeNewRun()
        {
            director.stageNumber = 1;
            player.data.PlayerHp = 8;
            player.core.playerHp = 8;
            player.selectedSkill = -1;
            player.bombs = 0;
            player.guideStock = 0; player.ownedMask = 0; player.equippedParts = 0;
            player.equippedPatches = new[] { -1, -1 };
            shop.coin = 0;
            shop.part1 = shop.part2 = shop.part3 = 0;
            shop.pull = shop.slow = shop.reflex = shop.stop = 0;
            shop.partLicense = 1;
            score.score = 0;
            EnablePlayerColliders();
        }

        public void StartGame()
        {
            if (started || ended) return;
            started = true;
            paused = false;
            betweenStages = false;
            if (selectionPanel != null) selectionPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (intermissionPanel != null) intermissionPanel.SetActive(false);
            shop.shopMenu.SetActive(false);
            director.enabled = true;
            Time.timeScale = 1;
            SaveCheckpoint();
        }
        // Compatibility bindings for old scene buttons and Editor check sources.
        public void SelectSkill(int index) => StartGame();
        public void SelectBlackHole() => StartGame();
        public void SelectGravity() => StartGame();
        public void SelectParry() => StartGame();

        public void OpenShop()
        {
            if (!started || ended || !betweenStages) return;
            Time.timeScale = 0;
            if (pausePanel != null) pausePanel.SetActive(false);
            shop.shopMenu.SetActive(false); // Responsive shop is rendered by DesignHud.
        }
        void Update()
        {
            if (!started || ended) return;
            SyncPatchOwnership();
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (hpUI != null) foreach (var ui in hpUI) if (ui != null) ui.ReloadUI();
            if (status != null) status.text = $"Stage {director.stageNumber}/3   Wave {director.Wave}/5   {director.Elapsed:000}/300s   HP {player.data.PlayerHp}/8\nMove: WASD/Arrows | Skills: 1 / 2 / 3 / 4 / 5 | Pause: Esc\nPatch stock: Black Hole {shop.pull} | Gravity {shop.slow} | Parry {shop.reflex} | Stop {shop.stop}";
            if (Running && director.State == StageDirector.Phase.Clear)
            {
                if (director.stageNumber == 3) End(true);
                else BeginIntermission();
            }
        }
        void BeginIntermission()
        {
            betweenStages = true;
            paused = false;
            Time.timeScale = 0;
            shop.partLicense = 1;
            if (pausePanel != null) pausePanel.SetActive(false);
            if (intermissionPanel != null) intermissionPanel.SetActive(true);
            shop.shopMenu.SetActive(false); // Responsive shop is rendered by DesignHud.
        }
        public void ContinueAfterShop()
        {
            if (!betweenStages || ended || director.State != StageDirector.Phase.Clear) return;
            shop.shopMenu.SetActive(false);
            if (intermissionPanel != null) intermissionPanel.SetActive(false);
            foreach (var drop in FindObjectsByType<CampaignDrop>(FindObjectsSortMode.None)) Destroy(drop.gameObject);
            foreach (var effect in FindObjectsByType<IntegratedSkillEffect>(FindObjectsSortMode.None)) Destroy(effect.gameObject);
            director.AdvanceToNextStage();
            betweenStages = false;
            paused = false;
            frozenUntil = 0;
            Time.timeScale = 1;
            SaveCheckpoint();
        }
        public bool TryBuyPart(int kind)
        {
            if (!betweenStages || ended || kind < 0 || kind > 2 || shop.coin < 1000) return false;
            int owned = kind == 0 ? shop.part1 : kind == 1 ? shop.part2 : shop.part3;
            if (owned > 0) return false;
            shop.coin -= 1000;
            if (kind == 0) shop.part1 = 1;
            else if (kind == 1) shop.part2 = 1;
            else shop.part3 = 1;
            player.equippedParts |= 1 << kind;
            return true;
        }
        // IntegratedCombat consumes stock and starts its cooldown exactly once.
        public bool TryTimeStop() => player != null && player.TryUsePatch(3);
        public bool ActivateTimeStop()
        {
            if (!Running) return false;
            frozenUntil = Time.time + 3;
            return true;
        }
        void Reward(EnemyShip enemy, int value)
        {
            if (ended) return;
            Kills++; score.score += value; shop.coin += value;
            CampaignDrop.Spawn(enemy.transform.position, Kills);
            score.scoreUI.text = "Score : " + score.score; shop.coinUI.text = "Coin : " + shop.coin;
            GameDataManager.instance.score = score.score; GameDataManager.instance.money = shop.coin;
        }
        public void TogglePause()
        {
            if (!started || betweenStages || ended) return;
            paused = !paused;
            if (pausePanel != null) pausePanel.SetActive(paused);
            Time.timeScale = paused ? 0 : 1;
            if (!paused) shop.shopMenu.SetActive(false);
        }
        public void End(bool win)
        {
            if (ended) return;
            ended = true; Won = win; Time.timeScale = 0;
            director.enabled = false; player.movement.enabled = false;
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            result.text = (win ? "CAMPAIGN CLEAR" : "GAME OVER") + "\nScore : " + score.score;
            resultPanel.SetActive(true);
            IntegrationSaveStore.RecordResult(score.score);
            if (win) IntegrationSaveStore.ClearCheckpoint();
            else { SaveCheckpoint(); IntegrationSaveStore.Data.checkpoint.coreHp = 8; IntegrationSaveStore.Save(); }
        }
        public void Restart()
        {
            if (!Won)
            {
                // Restart the current stage, retaining current money, equipment and item stock.
                SaveCheckpoint();
                IntegrationSaveStore.Data.checkpoint.coreHp = 8;
                IntegrationSaveStore.Save();
                IntegrationSaveStore.ContinueRequested = true;
            }
            else { IntegrationSaveStore.ClearCheckpoint(); }
            Time.timeScale = 1;
            if (GameDataManager.instance != null) Destroy(GameDataManager.instance.gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void BackToMenu()
        {
            Time.timeScale = 1;
            if (GameDataManager.instance != null) Destroy(GameDataManager.instance.gameObject);
            SceneManager.LoadScene("MainMenu");
        }
        public void SaveCheckpoint()
        {
            SyncPatchOwnership();
            // Stage-start saves retain stock/ownership/slots. Loading restarts
            // cooldowns and active effects, just as the previous checkpoints did.
            var c = new Checkpoint { stage = director.stageNumber, coreHp = Mathf.Clamp(player.data.PlayerHp, 1, 8),
                score = score.score, money = shop.coin, part1 = shop.part1, part2 = shop.part2, part3 = shop.part3,
                stop = shop.stop, reflex = shop.reflex, pull = shop.pull, slow = shop.slow,
                equippedParts = player.equippedParts, bombs = player.bombs, guide = player.guideStock,
                ownedMask = player.ownedMask, patchSlot0 = player.equippedPatches[0], patchSlot1 = player.equippedPatches[1] };
            IntegrationSaveStore.Data.checkpoint = c;
            IntegrationSaveStore.Save();
        }
        void RestoreCheckpoint(Checkpoint c)
        {
            director.stageNumber = c.stage;
            player.data.PlayerHp = Mathf.Clamp(c.coreHp, 1, 8);
            player.core.playerHp = player.data.PlayerHp;
            EnablePlayerColliders();
            score.score = c.score; shop.coin = c.money;
            shop.part1 = c.part1; shop.part2 = c.part2; shop.part3 = c.part3;
            shop.stop = c.stop; shop.reflex = c.reflex; shop.pull = c.pull; shop.slow = c.slow;
            shop.partLicense = 1;
            player.selectedSkill = -1;
            player.guideStock = c.guide; player.bombs = c.bombs; player.equippedParts = c.equippedParts;
            player.ownedMask = c.ownedMask;
            player.equippedPatches = new[] { c.patchSlot0, c.patchSlot1 };
            if (hpUI != null) foreach (var ui in hpUI) if (ui != null) ui.ReloadUI();
        }
        void EnablePlayerColliders()
        {
            // Wings are hit surfaces for the single HP pool; old wing HP is ignored.
            foreach (var collider in player.GetComponentsInChildren<Collider2D>(true)) collider.enabled = true;
        }
        void SyncPatchOwnership()
        {
            // Original shop buttons increment their existing counters directly.
            if (shop.pull > 0) player.ownedMask |= 1;
            if (shop.slow > 0) player.ownedMask |= 2;
            if (shop.reflex > 0) player.ownedMask |= 4;
            if (shop.stop > 0) player.ownedMask |= 8;
        }
        void RefreshCounters()
        {
            score.scoreUI.text = "Score : " + score.score;
            shop.coinUI.text = "남은 돈 : " + shop.coin + "벨";
        }
    }
}
