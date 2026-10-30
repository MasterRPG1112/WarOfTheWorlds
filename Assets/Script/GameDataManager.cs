using System.Collections.Generic;
using UnityEngine;


namespace Jain
{
    public class GameDataManager : MonoBehaviour
    {
        public static GameDataManager instance;
        public bool persistentStorage = true;
        void Awake() { if (instance != null && instance != this) { Destroy(gameObject); return; } instance = this; }
        void OnDestroy() { if (instance == this) instance = null; }

        //플레이어 관련
        public float playerhp1;
        public float playerhp2;
        public float playerhp3;

        //점수 관련
        public int score;
        public int money;

        //데이터 리셋
        public int dataReset = 0;

        //상점 관련
        public int part1; //비상 동력원
        public int part2; //터보 엔진
        public int part3; //방호 철갑탄
        public int stop; //시간 정지
        public int reflex; //공격 반사
        public int pull; //블랙홀
        public int slow; //감속장

        //순위 관련
        [System.Serializable]
        public struct RankData
        {
            public string playerName;
            public int stageScore;

            public RankData(string name, int score)
            {
                this.playerName = name;
                this.stageScore = score;
            }
        }
        public List<RankData> rank = new List<RankData>();
        private const int maxRankCount = 10;

        void Start()
        {
            instance = this;
            DontDestroyOnLoad(instance);
            LoadData();
        }

        void Update()
        {
        
        }

        public void SaveData()
        {
            if (!persistentStorage) return;
            //플레이어 관련
            PlayerPrefs.SetFloat("PlayerHp1", playerhp1);
            PlayerPrefs.SetFloat("PlayerHp2", playerhp2);
            PlayerPrefs.SetFloat("PlayerHp3", playerhp3);

            //점수 관련
            PlayerPrefs.SetInt("Score", score);
            PlayerPrefs.SetInt("Money", money);

            //데이터 리셋
            PlayerPrefs.SetInt("DataReset", dataReset);

            //상점 관련
            PlayerPrefs.SetInt("Part1", part1);
            PlayerPrefs.SetInt("Part2", part2);
            PlayerPrefs.SetInt("Part3", part3);
            PlayerPrefs.SetInt("Stop", stop);
            PlayerPrefs.SetInt("Reflex", reflex);
            PlayerPrefs.SetInt("Pull", pull);
            PlayerPrefs.SetInt("Slow", slow);

            //순위 관련
            for (int i = 0; i < rank.Count; i++)
            {
                PlayerPrefs.SetString($"RankName_{i}", rank[i].playerName);
                PlayerPrefs.SetInt($"RankScore_{i}", rank[i].stageScore);
            }
            PlayerPrefs.SetInt("RankCount", rank.Count);
        }

        public void LoadData()
        {
            if (!persistentStorage) return;
            //플레이어 관련
            playerhp1 = PlayerPrefs.GetFloat("PlayerHp1");
            playerhp2 = PlayerPrefs.GetFloat("PlayerHp2");
            playerhp3 = PlayerPrefs.GetFloat("PlayerHp3");

            //점수 관련
            score = PlayerPrefs.GetInt("Score");
            money = PlayerPrefs.GetInt("Money");

            //데이터 리셋
            dataReset = PlayerPrefs.GetInt("DataReset");

            //상점 관련
            part1 = PlayerPrefs.GetInt("Part1");
            part2 = PlayerPrefs.GetInt("Part2");
            part3 = PlayerPrefs.GetInt("Part3");
            stop = PlayerPrefs.GetInt("Stop");
            reflex = PlayerPrefs.GetInt("Reflex");
            pull = PlayerPrefs.GetInt("Pull");
            slow = PlayerPrefs.GetInt("Slow");

            //순위 관련
            rank.Clear();
            int rankCount = PlayerPrefs.GetInt("RankCount", 0);
            for (int i = 0; i < rankCount; i++)
            {
                string name = PlayerPrefs.GetString($"RankName_{i}", "Unknown");
                int stageScore = PlayerPrefs.GetInt($"RankScore_{i}", 0);
                rank.Add(new RankData(name, stageScore));
            }
        }

        // 새로운 점수를 랭킹에 추가하고 정렬/저장하는 함수
        public void AddRank(string playerName, int newScore)
        {
            rank.Add(new RankData(playerName, newScore));

            // 점수가 높은 순(내림차순)으로 정렬
            rank.Sort((x, y) => y.stageScore.CompareTo(x.stageScore));

            // 10개를 초과하면 하위 기록 제거
            if (rank.Count > maxRankCount)
            {
                rank.RemoveRange(maxRankCount, rank.Count - maxRankCount);
            }

            // PlayerPrefs에 저장
            SaveData();
        }
    }
}
