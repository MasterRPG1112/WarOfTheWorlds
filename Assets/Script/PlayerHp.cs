using Sanghyuk;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jain
{
    public class PlayerHp : MonoBehaviour
    {
        public float playerHp;
        public bool integrated;

        void Start()
        {
            if (integrated) return;
            ReloadUI(); 
            GameDataManager.instance.LoadData();
            if (gameObject.CompareTag("Point1"))
            {
                if (GameDataManager.instance.dataReset == 1) playerHp = 8;
                else playerHp = GameDataManager.instance.playerhp1;
                GameDataManager.instance.playerhp1 = playerHp;
                GameDataManager.instance.SaveData();
            }
            if (gameObject.CompareTag("Point2"))
            {
                if (GameDataManager.instance.dataReset == 1) playerHp = 5;
                else playerHp = GameDataManager.instance.playerhp2;
                GameDataManager.instance.playerhp2 = playerHp;
                GameDataManager.instance.SaveData();
            }
            if (gameObject.CompareTag("Point3"))
            {
                if (GameDataManager.instance.dataReset == 1) playerHp = 5;
                else playerHp = GameDataManager.instance.playerhp3;
                GameDataManager.instance.playerhp3 = playerHp;
                GameDataManager.instance.SaveData();
            }
        }

        void Update()
        {
        
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (integrated) return;
            if (collision.CompareTag("Bullet"))
            {
                Destroy(collision.gameObject);
                playerHp -= 1;
                ReloadUI();
                if (gameObject.CompareTag("Point1"))
                {
                    GameDataManager.instance.playerhp1 = playerHp;
                    GameDataManager.instance.SaveData();
                    if (playerHp <= 0)
                    {
                        GameDataManager.instance.dataReset = 0;
                        GameDataManager.instance.playerhp1 = 8;
                        GameDataManager.instance.playerhp2 = 5;
                        GameDataManager.instance.playerhp3 = 5;
                        GameDataManager.instance.score = 0;
                        GameDataManager.instance.SaveData();
                        if (SceneManager.GetActiveScene().name == "Stage1") SceneManager.LoadScene("Stage1");
                        else if (SceneManager.GetActiveScene().name == "Stage2") SceneManager.LoadScene("Stage2");
                        else SceneManager.LoadScene("Stage3");
                    }
                }
                if (gameObject.CompareTag("Point2"))
                {
                    GameDataManager.instance.playerhp2 = playerHp;
                    GameDataManager.instance.SaveData();
                    if (playerHp <= 0)
                    {
                        SpeedMin();
                    }
                }
                if (gameObject.CompareTag("Point3"))
                {
                    GameDataManager.instance.playerhp3 = playerHp;
                    GameDataManager.instance.SaveData();
                    if (playerHp <= 0)
                    {
                        SpeedMin();
                    }
                }
            }
        }

        void ReloadUI()
        {
            PlayerHpUI playerHpUI = GameObject.Find("HpBar1").GetComponent<PlayerHpUI>();
            PlayerHpUI playerHpUI2 = GameObject.Find("HpBar2").GetComponent<PlayerHpUI>();
            PlayerHpUI playerHpUI3 = GameObject.Find("HpBar3").GetComponent<PlayerHpUI>();

            playerHpUI.ReloadUI();
            playerHpUI2.ReloadUI();
            playerHpUI3.ReloadUI();
        }

        void SpeedMin()
        {
            Player player = GameObject.Find("Player")?.GetComponent<Player>();
            Player point2 = GameObject.Find("Point2")?.GetComponent<Player>();
            Player point3 = GameObject.Find("Point3")?.GetComponent<Player>();

            player.speed = player.speed / (player.speed / 2);
            if (point2 == null) Debug.Log("오른쪽 날개는 이미 파손");
            else point2.speed = point2.speed / (point2.speed / 2);
            if (point3 == null) Debug.Log("왼쪽 날개는 이미 파손");
            else point3.speed = point3.speed / (point3.speed / 2);
            Destroy(gameObject);
        }
    }
}
