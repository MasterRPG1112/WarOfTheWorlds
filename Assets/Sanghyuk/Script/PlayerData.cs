using UnityEngine;
using UnityEngine.SceneManagement;

namespace sanghyuk
{
    public class PlayerData : MonoBehaviour
    {
        public int PlayerHp = 5;
        public int Bomb;
        public int Score;
        public float time;

        void Start()
        {

        }

        void Update()
        {
            if (PlayerHp <= 0)
            {
                SceneManager.LoadScene("Die"); // 체력 없으면 사망씬으로 감
            }
        }
    }

}