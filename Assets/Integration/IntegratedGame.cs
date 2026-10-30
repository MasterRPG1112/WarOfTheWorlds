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
        public GameObject pausePanel, selectionPanel, resultPanel;
        public Text status, result;
        public bool Running => started && !paused && !ended && !choosing;
        public int Kills { get; private set; }
        public bool Won { get; private set; }
        public bool Ended => ended;
        float frozenUntil;
        public bool EnemiesFrozen => Running && Time.time < frozenUntil;
        bool started, paused, ended, choosing = true;
        bool checkpointPending = true;
        void Start()
        {
            shop.Initialize();
            // Start initializes the original shop once; its cheat-key polling is disabled.
            shop.enabled = false;
            if (IntegrationSaveStore.ContinueRequested && IntegrationSaveStore.HasCheckpoint)
                RestoreCheckpoint(IntegrationSaveStore.Data.checkpoint);
            IntegrationSaveStore.ContinueRequested = false;
            score.scoreUI.text = "Score : " + score.score;
            shop.coinUI.text = "남은 돈 : " + shop.coin + "벨";
        }
        void Awake() { Active = this; Time.timeScale = 0; }
        void OnEnable() { EnemyShip.DestroyedByPlayer += Reward; }
        void OnDisable() { EnemyShip.DestroyedByPlayer -= Reward; }
        void OnDestroy() { if (Active == this) Active = null; Time.timeScale = 1; }
        public void SelectSkill(int index)
        {
            if (index < 0 || index > 2 || ended) return;
            player.selectedSkill = index; choosing = false; started = true;
            selectionPanel.SetActive(false); director.enabled = true; Time.timeScale = 1;
            if (checkpointPending) { SaveCheckpoint(); checkpointPending = false; }
        }
        public void SelectBlackHole() => SelectSkill(0);
        public void SelectGravity() => SelectSkill(1);
        public void SelectParry() => SelectSkill(2);
        public void OpenShop() { pausePanel.SetActive(false); shop.shopMenu.SetActive(true); }
        void Update()
        {
            if (!started || ended) return;
            if (paused && !shop.shopMenu.activeSelf && !pausePanel.activeSelf) pausePanel.SetActive(true);
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Tab) && Running) { choosing = true; selectionPanel.SetActive(true); Time.timeScale = 0; }
            foreach (var ui in hpUI) if (ui != null) ui.ReloadUI();
            if (status != null) status.text = $"Stage {director.stageNumber}/3   Wave {director.Wave}/5   {director.Elapsed:000}/300s   Skill {player.SkillCooldown:0.0}s\nMove: WASD/Arrows | Skill: Space/Shift | Choose: Tab | Pause: Esc | Bomb: X ({player.bombs})\nShop stock: B Black Hole {shop.pull} | V Gravity {shop.slow} | R Parry {shop.reflex} | T Stop {shop.stop}";
            if (Running && director.State == StageDirector.Phase.Clear)
            {
                if (director.stageNumber == 3) End(true);
                else { director.AdvanceToNextStage(); player.bombs = 1; shop.partLicense = 1; checkpointPending = true; SaveCheckpoint(); choosing = true; selectionPanel.SetActive(true); Time.timeScale = 0; }
            }
        }
        public bool TryTimeStop()
        {
            if (!Running || shop.stop <= 0) return false;
            shop.stop--; frozenUntil = Time.time + 3; return true;
        }
        void Reward(EnemyShip enemy, int value)
        {
            if (ended) return;
            Kills++; score.score += value; shop.coin += value;
            score.scoreUI.text = "Score : " + score.score; shop.coinUI.text = "Coin : " + shop.coin;
            GameDataManager.instance.score = score.score; GameDataManager.instance.money = shop.coin;
        }
        public void TogglePause()
        {
            if (!started || choosing || ended) return;
            paused = !paused; pausePanel.SetActive(paused); Time.timeScale = paused ? 0 : 1;
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
            IntegrationSaveStore.ClearCheckpoint();
        }
        public void Restart()
        {
            IntegrationSaveStore.ContinueRequested = false;
            IntegrationSaveStore.ClearCheckpoint();
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
            var c = new Checkpoint {stage = director.stageNumber, coreHp = Mathf.Clamp(player.data.PlayerHp,1,8),
                score = score.score, money = shop.coin, part1 = shop.part1, part2 = shop.part2, part3 = shop.part3,
                stop = shop.stop, reflex = shop.reflex, pull = shop.pull, slow = shop.slow, selectedSkill = player.selectedSkill};
            foreach (var hp in player.GetComponentsInChildren<PlayerHp>())
                if (hp.name == "Point2") c.leftHp = Mathf.RoundToInt(hp.playerHp);
                else if (hp.name == "Point3") c.rightHp = Mathf.RoundToInt(hp.playerHp);
            IntegrationSaveStore.Data.checkpoint = c;
            IntegrationSaveStore.Save();
        }
        void RestoreCheckpoint(Checkpoint c)
        {
            director.stageNumber = c.stage;
            player.data.PlayerHp = c.coreHp; player.core.playerHp = c.coreHp;
            foreach (var hp in player.GetComponentsInChildren<PlayerHp>())
            {
                if (hp == player.core) continue;
                hp.playerHp = hp.name == "Point2" ? c.leftHp : c.rightHp;
                var collider = hp.GetComponent<Collider2D>(); if (collider != null) collider.enabled = hp.playerHp > 0;
            }
            score.score = c.score; shop.coin = c.money;
            shop.part1 = c.part1; shop.part2 = c.part2; shop.part3 = c.part3;
            shop.stop = c.stop; shop.reflex = c.reflex; shop.pull = c.pull; shop.slow = c.slow;
            player.selectedSkill = c.selectedSkill;
            foreach (var ui in hpUI) if (ui != null) ui.ReloadUI();
        }
    }
}
