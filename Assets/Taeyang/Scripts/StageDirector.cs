using System;
using UnityEngine;
using UnityEngine.Events;

namespace Taeyang
{
    public class StageDirector : MonoBehaviour
    {
        public enum Phase { Survival, Clearing, AwaitingBoss, Boss, Clear }
        [Range(1,3)] public int stageNumber = 1;
        public EnemyShip[] enemyPrefabs;
        public GameObject bossPrefab;
        public GameObject[] campaignBossPrefabs = new GameObject[3];
        private GameObject CurrentBoss => campaignBossPrefabs != null && campaignBossPrefabs.Length >= stageNumber && campaignBossPrefabs[stageNumber-1] != null ? campaignBossPrefabs[stageNumber-1] : bossPrefab;
        public Vector3 bossPosition = new Vector3(0, 3, 0);
        public UnityEvent bossRequested = new UnityEvent();
        public UnityEvent stageCleared = new UnityEvent();
        public Phase State { get; private set; }
        public float Elapsed { get; private set; }
        public int Wave => Mathf.Min(5, (int)(Elapsed / 60) + 1);
        public int SpawnTicks { get; private set; }
        public const float Duration = 300;
        public bool noBossCampaign;
        // Prototype-only skip; a registered boss always uses its real defeat pipeline.
        public bool allowMissingBossTestSkip;
        public bool perimeterSpawns;
        public int LastSpawnDirection { get; private set; } = -1;
        public Vector2 LastSpawnPosition { get; private set; }
        public event Action<EnemyShip, int> EnemySpawned;
        public bool showDemoHUD = true;
        public float SpawnInterval => stageNumber + 2;
        private float nextSpawn, nextObstacle = 7;
        private GameObject boss;
        private bool bossWasAttached;
        private StageBackdrop backdrop;
        public static int PickKind(int stage, int wave, int tick, int member)
        {
            int[] pool = stage == 1 ? new[]{0,1,3,0,4} : stage == 2 ? new[]{1,2,3,4} : new[]{0,1,2,3,4};
            int count = stage == 1 && wave < 3 ? 3 : pool.Length;
            return pool[(tick + member) % count];
        }
        private void Start()
        {
            stageNumber = Mathf.Clamp(stageNumber,1,3);
            if(enemyPrefabs == null || enemyPrefabs.Length != 5 || Array.Exists(enemyPrefabs,p => p == null))
            { Debug.LogError("Stage requires all five Taeyang enemy prefabs",this); enabled=false; return; }
            backdrop = new GameObject("Stage environment").AddComponent<StageBackdrop>();
            backdrop.Initialize(stageNumber, perimeterSpawns);
            nextSpawn = SpawnInterval;
        }
        private void Update()
        {
            if(State == Phase.Survival)
            {
                Elapsed = Mathf.Min(Duration, Elapsed + Time.deltaTime);
                while(nextSpawn <= Elapsed && nextSpawn < Duration)
                {
                    SpawnFormation(); nextSpawn += SpawnInterval;
                }
                if(Elapsed >= nextObstacle && Elapsed < Duration)
                { SpawnObstacle(); nextObstacle += stageNumber == 1 ? 7 : stageNumber == 2 ? 6 : 5; }
                if(Elapsed >= Duration) State = Phase.Clearing;
            }
            if(State == Phase.Clearing && FindObjectsByType<EnemyShip>(FindObjectsSortMode.None).Length == 0)
            {
                foreach(var p in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
                foreach(var p in FindObjectsByType<StageObstacle>(FindObjectsSortMode.None)) Destroy(p.gameObject);
                if (noBossCampaign) { State = Phase.Clear; stageCleared.Invoke(); return; }
                State = Phase.AwaitingBoss;
                bossRequested.Invoke();
                if(CurrentBoss != null && State == Phase.AwaitingBoss) AttachBoss(Instantiate(CurrentBoss,bossPosition,Quaternion.identity));
                else if (CurrentBoss == null && allowMissingBossTestSkip && State == Phase.AwaitingBoss)
                    SkipMissingBossForTesting();
            }
            if(State == Phase.Boss && bossWasAttached && boss == null) NotifyBossDefeated();
        }
        private void SpawnFormation()
        {
            int members = stageNumber == 1 ? (Wave >= 4 ? 2 : 1) : stageNumber == 2 ? (Wave >= 3 ? 3 : 2) : (Wave >= 3 ? 4 : 3);
            float center = UnityEngine.Random.Range(-3.5f,3.5f);
            LastSpawnDirection = perimeterSpawns ? SpawnTicks % 8 : -1;
            for(int i=0;i<members;i++)
            {
                int kind = PickKind(stageNumber,Wave,SpawnTicks,i);
                Vector2 position = perimeterSpawns ? Integration.BattleArena.SpawnPosition(LastSpawnDirection,i,members)
                    : new Vector2(center+(i-(members-1)*0.5f)*1.5f,5.8f+i*0.25f);
                LastSpawnPosition = position;
                var ship = Instantiate(enemyPrefabs[kind],position,Quaternion.identity);
                ship.approachPlayer = perimeterSpawns;
                ship.stageOnePatterns = true;
                ship.stageDifficulty = stageNumber;
                ship.maxHealth = Mathf.RoundToInt(ship.maxHealth*(1+(stageNumber-1)*0.3f+(Wave-1)*0.08f));
                ship.ResetHealthForTesting();
                ship.fireInterval *= Mathf.Max(0.48f,1-(stageNumber-1)*0.15f-(Wave-1)*0.04f);
                EnemySpawned?.Invoke(ship,LastSpawnDirection);
            }
            SpawnTicks++;
        }
        private void SpawnObstacle()
        {
            var go = new GameObject(stageNumber == 3 ? "Ice meteor" : stageNumber == 2 ? "Drifting wreck" : "Asteroid");
            if (perimeterSpawns)
            {
                Rect view = Integration.BattleArena.ViewBounds();
                go.transform.position = new Vector3(UnityEngine.Random.Range(view.xMin+.8f,view.xMax-.8f),view.yMax+1.5f,0);
            }
            else go.transform.position = new Vector3(UnityEngine.Random.Range(-6.8f,6.8f),6.5f,0);
            go.AddComponent<StageObstacle>().Initialize(stageNumber);
        }
        public void AttachBoss(GameObject instance)
        {
            if(State != Phase.AwaitingBoss || instance == null) return;
            boss = instance; bossWasAttached = true; State = Phase.Boss;
            var target = instance.GetComponent<Integration.IntegratedBossTarget>();
            if (target != null) target.director = this;
        }
        // Call on actual defeat if the team's boss pools/disables itself rather than being destroyed.
        public void NotifyBossDefeated()
        {
            if(State != Phase.Boss) return;
            State = Phase.Clear; stageCleared.Invoke(); Debug.Log("TAEYANG_STAGE_"+stageNumber+"_CLEAR");
        }
        public void SkipMissingBossForTesting()
        {
            if(State != Phase.AwaitingBoss || CurrentBoss != null) return;
            State = Phase.Clear; stageCleared.Invoke(); Debug.Log("TAEYANG_STAGE_"+stageNumber+"_TEST_SKIP_BOSS");
        }
        public void AdvanceToNextStage()
        {
            if(State != Phase.Clear || stageNumber >= 3) return;
            foreach(var p in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            foreach(var p in FindObjectsByType<StageObstacle>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            if(backdrop != null) Destroy(backdrop.gameObject);
            bossPrefab=null; stageNumber++; Elapsed=0; SpawnTicks=0; LastSpawnDirection=-1; nextObstacle=7; boss=null; bossWasAttached=false;
            State=Phase.Survival; Start();
        }
        private void OnGUI()
        {
            if (!showDemoHUD) return;
            float scale = Mathf.Min(Screen.width/960f,Screen.height/540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUI.Box(new Rect(14,12,470,110),GUIContent.none);
            GUI.Label(new Rect(28,20,450,25),"STAGE "+stageNumber+" / "+new[]{"ASTEROID FIELD","DARK SPACE","FROZEN REGION"}[stageNumber-1]+"   WAVE "+Wave+" / 5");
            GUI.Label(new Rect(28,45,450,25),Mathf.FloorToInt(Elapsed/60).ToString("00")+":"+Mathf.FloorToInt(Elapsed%60).ToString("00")+" / 05:00    "+State);
            GUI.Box(new Rect(28,76,430,10),GUIContent.none);
            GUI.Box(new Rect(28,76,430*Elapsed/Duration,10),GUIContent.none);
            GUI.Label(new Rect(28,91,450,25),"Move: WASD / Arrows    Clear projectiles: Space");
            if(State == Phase.AwaitingBoss)
            {
                GUI.Box(new Rect(250,220,460,90),"BOSS NOT CONNECTED - awaiting team prefab");
                if(GUI.Button(new Rect(310,260,340,30),"TEST ONLY: skip missing boss")) SkipMissingBossForTesting();
            }
            if(State == Phase.Clear)
            {
                GUI.Box(new Rect(310,210,340,100),stageNumber == 3 ? "ALL THREE STAGES CLEAR" : "STAGE CLEAR");
                if(stageNumber < 3 && GUI.Button(new Rect(350,255,260,35),"NEXT STAGE")) AdvanceToNextStage();
            }
        }
        private void OnDestroy() { if(backdrop != null) Destroy(backdrop.gameObject); }
    }
}
