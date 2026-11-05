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
        public bool HasStarted => started;
        public bool ShopAccessible => started && !ended && (paused || betweenStages);
        public int Kills { get; private set; }
        public bool Won { get; private set; }
        public bool Ended => ended;
        public bool RankedRun { get; private set; } = true;
        public bool ResultSubmitted { get; private set; }
        CampaignNarrative narrative;
        float frozenUntil;
        public bool EnemiesFrozen => Running && Time.time < frozenUntil;
        bool started, paused, ended, betweenStages;
        int startFrame;

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
            narrative = GetComponent<CampaignNarrative>();
            if (narrative == null) narrative = gameObject.AddComponent<CampaignNarrative>();
            if (GetComponent<CampaignCheats>() == null) gameObject.AddComponent<CampaignCheats>();
            BindPauseShop();
            if (director.stageNumber == 1 && !loadingCheckpoint) narrative.PlayOpening(this);
            else StartGame();
        }
        bool loadingCheckpoint;
        void Awake() { Active = this; Time.timeScale = 0; }
        void OnEnable() { EnemyShip.DestroyedByPlayer += Reward; }
        void OnDisable() { EnemyShip.DestroyedByPlayer -= Reward; }
        void OnDestroy() { if (Active == this) Active = null; Time.timeScale = 1; }

        void InitializeNewRun()
        {
            director.stageNumber = 1;
            player.data.PlayerHp = 8;
            player.core.playerHp = 8;
            player.data.HaveShield = 0;
            player.data.Invin = false;
            player.selectedSkill = -1;
            player.bombs = 0;
            player.ownedMask = 0;
            player.equippedPatches = new[] { -1, -1 };
            shop.coin = 0;
            shop.part1 = shop.part2 = shop.part3 = 0;
            shop.equippedPartsMask = 0;
            shop.pull = shop.slow = shop.reflex = shop.stop = 0;
            shop.partLicense = 1;
            score.score = 0;
            EnablePlayerColliders();
        }

        public void StartGame()
        {
            if (started || ended) return;
            started = true;
            startFrame = Time.frameCount;
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
            if (!started || ended) return;
            if (!betweenStages) paused = true;
            Time.timeScale = 0;
            if (pausePanel != null) pausePanel.SetActive(false);
            shop.shopMenu.SetActive(true);
            shop.RefreshUI();
        }
        public void CloseShop()
        {
            shop.shopMenu.SetActive(false);
            if (!betweenStages && paused && !ended && pausePanel != null) pausePanel.SetActive(true);
        }
        void BindPauseShop()
        {
            if (pausePanel == null || pausePanel.transform.Find("Shop and parts") != null) return;
            Button template = null;
            foreach (var button in pausePanel.GetComponentsInChildren<Button>(true))
                if (button.name == "RESTART") { template = button; break; }
            if (template == null) return;
            var copy = Instantiate(template.gameObject, template.transform.parent);
            copy.name = "Shop and parts";
            var rect = copy.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -120);
            var action = copy.GetComponent<Button>();
            action.onClick = new Button.ButtonClickedEvent();
            action.onClick.AddListener(OpenShop);
            var label = copy.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "상점 / 파츠 (O)";
            copy.SetActive(true);
        }
        void Update()
        {
            if (!started || ended) return;
            SyncPatchOwnership();
            if (Time.frameCount > startFrame && Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Input.GetKeyDown(KeyCode.O))
            {
                if (shop.shopMenu.activeInHierarchy) CloseShop(); else OpenShop();
            }
            if (hpUI != null) foreach (var ui in hpUI) if (ui != null) ui.ReloadUI();
            if (status != null) status.text = $"Stage {director.stageNumber}/3   Wave {director.Wave}/5   {director.Elapsed:000}/300s   HP {player.data.PlayerHp}/8\nMove: WASD/Arrows | Skills: B / V / R / T | Inventory: P | Pause: Esc\nStock: Black Hole {shop.pull} | Slow {shop.slow} | Reflect {shop.reflex} | Stop {shop.stop}";
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
            shop.shopMenu.SetActive(true);
        }
        public void ContinueAfterShop()
        {
            if (!betweenStages || ended || director.State != StageDirector.Phase.Clear) return;
            shop.shopMenu.SetActive(false);
            if (intermissionPanel != null) intermissionPanel.SetActive(false);
            director.AdvanceToNextStage();
            player.GetComponent<CampaignDrops>()?.ClearPickups();
            betweenStages = false;
            paused = false;
            frozenUntil = 0;
            Time.timeScale = 1;
            SaveCheckpoint();
        }
        // IntegratedCombat consumes stock and starts its cooldown exactly once.
        public bool TryTimeStop() => player != null && player.TryUsePatch(3);
        public bool ActivateTimeStop(float duration = 3)
        {
            if (!Running) return false;
            frozenUntil = Time.time + Mathf.Max(.1f, duration);
            return true;
        }
        void Reward(EnemyShip enemy, int value)
        {
            if (ended || !Running) return;
            Kills++; score.score += value; shop.coin += value;
            score.scoreUI.text = "Score : " + score.score; shop.coinUI.text = "Coin : " + shop.coin;
            GameDataManager.instance.score = score.score; GameDataManager.instance.money = shop.coin;
        }
        public void TogglePause()
        {
            if (!started || betweenStages || ended) return;
            if (shop.shopMenu.activeInHierarchy) { CloseShop(); return; }
            paused = !paused;
            if (pausePanel != null) pausePanel.SetActive(paused);
            Time.timeScale = paused ? 0 : 1;
            if (!paused) shop.shopMenu.SetActive(false);
        }
        public void End(bool win)
        {
            if (ended) return;
            if (win && (director.stageNumber != 3 || director.State != StageDirector.Phase.Clear)) return;
            ended = true; Won = win; Time.timeScale = 0;
            director.enabled = false; player.movement.enabled = false;
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            result.text = (win ? "CAMPAIGN CLEAR" : "GAME OVER · STAGE " + director.stageNumber) + " · " + score.score + "점" +
                (win ? "" : "\n다시하기: 스테이지 시작점 복원");
            resultPanel.SetActive(true);
            if (win)
            {
                IntegrationSaveStore.ClearCheckpoint();
                narrative.ShowEnding(this);
            }
        }
        public void MarkPracticeRun()
        {
            RankedRun = false;
            if (IntegrationSaveStore.HasCheckpoint)
            {
                IntegrationSaveStore.Data.checkpoint.practice = true;
                IntegrationSaveStore.Save();
            }
        }
        bool DevelopmentControlsAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var cheats = GetComponent<CampaignCheats>();
                return started && !ended && player != null && player.Health > 0 && cheats != null && cheats.Allowed;
#else
                return false;
#endif
            }
        }
        public bool AdvanceSurvivalForTesting(float seconds)
        {
            if (!DevelopmentControlsAllowed || !director.AdvanceSurvivalClock(seconds)) return false;
            if (RankedRun) MarkPracticeRun();
            return true;
        }
        public bool SkipToNextStageForTesting()
        {
            if (!DevelopmentControlsAllowed || director.stageNumber >= 3) return false;
            if (RankedRun) MarkPracticeRun();
            SaveCheckpoint();
            IntegrationSaveStore.Data.checkpoint.stage = director.stageNumber + 1;
            IntegrationSaveStore.Data.checkpoint.practice = true;
            IntegrationSaveStore.Save();
            IntegrationSaveStore.ContinueRequested = true;
            CampaignCheats.ResumeAfterChapterSkip = true;
            Time.timeScale = 1;
            ReleaseSessionData();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return true;
        }
        public bool SubmitVictory(string playerId)
        {
            if (!ended || !Won || ResultSubmitted || !RankedRun) return false;
            IntegrationSaveStore.RecordResult(score.score, playerId);
            ResultSubmitted = true;
            return true;
        }
        public void Restart()
        {
            // Death retries the current stage-start snapshot, including purchases
            // made before entering that stage. Mid-stage spending/drops roll back.
            IntegrationSaveStore.ContinueRequested = !Won && IntegrationSaveStore.HasCheckpoint;
            Time.timeScale = 1;
            ReleaseSessionData();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void BackToMenu()
        {
            Time.timeScale = 1;
            ReleaseSessionData();
            SceneManager.LoadScene("MainMenu");
        }
        public static void ReleaseSessionData()
        {
            // Clear the singleton before deferred destruction so the next scene's
            // Awake cannot reject its own session manager as a duplicate.
            var previous = GameDataManager.instance;
            GameDataManager.instance = null;
            if (previous != null) Destroy(previous.gameObject);
        }
        public void SaveCheckpoint()
        {
            SyncPatchOwnership();
            // Stage-start saves retain stock/ownership/slots. Loading restarts
            // cooldowns and active effects, just as the previous checkpoints did.
            var c = new Checkpoint { stage = director.stageNumber, coreHp = Mathf.Clamp(player.data.PlayerHp, 1, 8),
                score = score.score, money = shop.coin, part1 = shop.part1, part2 = shop.part2, part3 = shop.part3,
                stop = shop.stop, reflex = shop.reflex, pull = shop.pull, slow = shop.slow,
                ownedMask = player.ownedMask, shields = player.data.HaveShield, practice = !RankedRun,
                equippedPartsMask = shop.EquippedPartsMask };
            IntegrationSaveStore.Data.checkpoint = c;
            IntegrationSaveStore.Save();
        }
        void RestoreCheckpoint(Checkpoint c)
        {
            loadingCheckpoint = true;
            director.stageNumber = c.stage;
            RankedRun = !c.practice;
            player.data.PlayerHp = Mathf.Clamp(c.coreHp, 1, 8);
            player.core.playerHp = player.data.PlayerHp;
            EnablePlayerColliders();
            score.score = c.score; shop.coin = c.money;
            shop.part1 = c.part1; shop.part2 = c.part2; shop.part3 = c.part3;
            shop.equippedPartsMask = c.equippedPartsMask;
            shop.stop = c.stop; shop.reflex = c.reflex; shop.pull = c.pull; shop.slow = c.slow;
            shop.partLicense = 1;
            player.selectedSkill = -1;
            player.bombs = 0;
            player.ownedMask = c.ownedMask;
            player.equippedPatches = new[] { c.patchSlot0, c.patchSlot1 };
            player.data.HaveShield = Mathf.Clamp(c.shields, 0, player.maxShields);
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
            shop.SyncLegacyData(false);
            shop.RefreshUI();
            player.RefreshPartEffects();
            if (GameDataManager.instance != null) GameDataManager.instance.score = score.score;
        }
    }
}
