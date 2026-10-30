using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;

namespace Jain
{
    public class Shop : MonoBehaviour
    {
        //돈
        public int coin;

        //우주선 파츠 소유 여부
        public int part1; //비상 동력원
        public int part2; //터보 엔진
        public int part3; //방호 철갑탄

        //스테이지 클리어 시 파츠 구매 권한
        public int partLicense;

        //특수 아이템 소유 여부
        public int stop; //시간 정지
        public int reflex; //공격 반사
        public int pull; //블랙홀
        public int slow; //감속장

        //UI
        public Text coinUI;
        public GameObject shopMenu;

        void Start() { Initialize(); }
        public void Initialize()
        {
            GameDataManager.instance.LoadData();
            if (GameDataManager.instance.dataReset == 1) coin = 0;
            else coin = GameDataManager.instance.money;
            part1 = GameDataManager.instance.part1;
            part2 = GameDataManager.instance.part2;
            part3 = GameDataManager.instance.part3;
            stop = GameDataManager.instance.stop;
            reflex = GameDataManager.instance.reflex;
            pull = GameDataManager.instance.pull;
            slow = GameDataManager.instance.slow;
            ReloadUI();
            partLicense = 1;
        }

        void Update()
        {
            Cheat();
        }

        void Cheat()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                coin += 100;
                ReloadUI();
                GameDataManager.instance.money = coin;
                GameDataManager.instance.SaveData();
            }
        }
        
        public void BackBtn()
        {
            shopMenu.SetActive(false);
        }

        public void StopBtn()
        {
            //800벨
            if (coin >= 800)
            {
                stop += 1;
                coin -= 800;
                GameDataManager.instance.stop = stop;
                GameDataManager.instance.money = coin;
                GameDataManager.instance.SaveData();
                ReloadUI();
            }
        }

        public void ReflexBtn()
        {
            //450벨
            if (coin >= 450)
            {
                reflex += 1;
                coin -= 450;
                GameDataManager.instance.reflex = reflex;
                GameDataManager.instance.money = coin;
                GameDataManager.instance.SaveData();
                ReloadUI();
            }
        }

        public void PullBtn()
        {
            //600벨
            if (coin >= 600)
            {
                pull += 1;
                coin -= 600;
                GameDataManager.instance.pull = pull;
                GameDataManager.instance.money = coin;
                GameDataManager.instance.SaveData();
                ReloadUI();
            }
        }

        public void SlowBtn()
        {
            //550벨
            if (coin >= 550)
            {
                slow += 1;
                coin -= 550;
                GameDataManager.instance.slow = slow;
                GameDataManager.instance.money = coin;
                GameDataManager.instance.SaveData();
                ReloadUI();
            }
        }

        public void LifeBtn()
        {
            if (partLicense == 1)
            {
                if (part1 == 0)
                {
                    if (coin >= 1000)
                    {
                        part1 = 1;
                        partLicense = 0;
                        coin -= 1000;
                        GameDataManager.instance.part1 = part1;
                        GameDataManager.instance.money = coin;
                        GameDataManager.instance.SaveData();
                        ReloadUI();
                    }
                }
            }
        }

        public void SpeedBtn()
        {
            if (partLicense == 1)
            {
                if (part2 == 0)
                {
                    if (coin >= 1000)
                    {
                        part2 = 1;
                        partLicense = 0;
                        coin -= 1000;
                        GameDataManager.instance.part2 = part2;
                        GameDataManager.instance.money = coin;
                        GameDataManager.instance.SaveData();
                        ReloadUI();
                    }
                }
            }
        }

        public void HpBtn()
        {
            if (partLicense == 1)
            {
                if (part3 == 0)
                {
                    if (coin >= 1000)
                    {
                        part3 = 1;
                        partLicense = 0;
                        coin -= 1000;
                        GameDataManager.instance.part3 = part3;
                        GameDataManager.instance.money = coin;
                        GameDataManager.instance.SaveData();
                        ReloadUI();
                    }
                }
            }
        }

        void ReloadUI()
        {
            coinUI.text = "남은 돈 : " + coin + "벨";
        }
    }
}