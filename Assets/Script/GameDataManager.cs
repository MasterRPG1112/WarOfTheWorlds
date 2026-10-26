using UnityEngine;

namespace Jain
{
    public class GameDataManager : MonoBehaviour
    {
        public static GameDataManager instance;

        //플레이어 관련
        public float playerhp1;
        public float playerhp2;
        public float playerhp3;

        //점수 관련
        public int score;

        //데이터 리셋
        public int dataReset = 0;

        void Start()
        {
            instance = this;
            DontDestroyOnLoad(instance);
        }

        void Update()
        {
        
        }

        public void SaveData()
        {
            //ID 관련
            if (PlayerPrefs.HasKey("id"))
            {
                string id = PlayerPrefs.GetString("id");
                Debug.Log(id);
            }
            else
            {
                PlayerPrefs.SetString("id", "Jain");
            }

            //플레이어 관련
            PlayerPrefs.SetFloat("PlayerHp1", playerhp1);
            PlayerPrefs.SetFloat("PlayerHp2", playerhp2);
            PlayerPrefs.SetFloat("PlayerHp3", playerhp3);

            //점수 관련
            PlayerPrefs.SetInt("Score", score);

            //데이터 리셋
            PlayerPrefs.SetInt("DataReset", dataReset);
        }

        public void LoadData()
        {
            //플레이어 관련
            playerhp1 = PlayerPrefs.GetFloat("PlayerHp1");
            playerhp2 = PlayerPrefs.GetFloat("PlayerHp2");
            playerhp3 = PlayerPrefs.GetFloat("PlayerHp3");

            //점수 관련
            score = PlayerPrefs.GetInt("Score");

            //데이터 리셋
            dataReset = PlayerPrefs.GetInt("DataReset");
        }
    }
}
