using UnityEngine;
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
        [Header("Purchase prices")]
        [Min(0)] public int partCost = 1000;
        [Min(0)] public int stopCost = 800;
        [Min(0)] public int reflexCost = 450;
        [Min(0)] public int pullCost = 600;
        [Min(0)] public int slowCost = 550;

        void Start() { Initialize(); }
        public void Initialize()
        {
            var manager = GameDataManager.instance;
            if (manager != null)
            {
                manager.LoadData();
                coin = manager.dataReset == 1 ? 0 : Mathf.Max(0, manager.money);
                part1 = Mathf.Clamp(manager.part1, 0, 1);
                part2 = Mathf.Clamp(manager.part2, 0, 1);
                part3 = Mathf.Clamp(manager.part3, 0, 1);
                stop = Mathf.Max(0, manager.stop);
                reflex = Mathf.Max(0, manager.reflex);
                pull = Mathf.Max(0, manager.pull);
                slow = Mathf.Max(0, manager.slow);
            }
            ReloadUI();
            partLicense = 1;
        }
        
        public void BackBtn()
        {
            if (shopMenu != null) shopMenu.SetActive(false);
        }

        public void StopBtn()
        {
            TryBuySkill(3);
        }

        public void ReflexBtn()
        {
            TryBuySkill(2);
        }

        public void PullBtn()
        {
            TryBuySkill(0);
        }

        public void SlowBtn()
        {
            TryBuySkill(1);
        }

        public void LifeBtn()
        {
            TryBuyPart(0);
        }

        public void SpeedBtn()
        {
            TryBuyPart(1);
        }

        public void HpBtn()
        {
            TryBuyPart(2);
        }

        bool CanPurchase
        {
            get
            {
                var game = Integration.IntegratedGame.Active;
                return game == null || (game.shop == this && game.BetweenStages && !game.Ended && shopMenu != null && shopMenu.activeInHierarchy);
            }
        }

        public bool TryBuySkill(int kind)
        {
            if (!CanPurchase || kind < 0 || kind > 3) return false;
            int cost = Mathf.Max(0, kind == 0 ? pullCost : kind == 1 ? slowCost : kind == 2 ? reflexCost : stopCost);
            if (coin < cost) return false;
            coin -= cost;
            if (kind == 0) pull++;
            else if (kind == 1) slow++;
            else if (kind == 2) reflex++;
            else stop++;
            SyncLegacyData(true);
            ReloadUI();
            return true;
        }

        public bool TryBuyPart(int kind)
        {
            if (!CanPurchase || partLicense != 1 || kind < 0 || kind > 2) return false;
            int owned = kind == 0 ? part1 : kind == 1 ? part2 : part3;
            int cost = Mathf.Max(0, partCost);
            if (owned > 0 || coin < cost) return false;
            coin -= cost;
            if (kind == 0) part1 = 1;
            else if (kind == 1) part2 = 1;
            else part3 = 1;
            // Preserve the existing one-part purchase allowance per cleared stage.
            partLicense = 0;
            SyncLegacyData(true);
            ReloadUI();
            return true;
        }

        public void SyncLegacyData(bool saveToDisk)
        {
            var manager = GameDataManager.instance;
            if (manager == null) return;
            manager.money = coin;
            manager.part1 = part1; manager.part2 = part2; manager.part3 = part3;
            manager.stop = stop; manager.reflex = reflex; manager.pull = pull; manager.slow = slow;
            if (saveToDisk) manager.SaveData();
        }

        void ReloadUI()
        {
            if (coinUI != null) coinUI.text = "남은 돈 : " + coin + "벨";
        }
    }
}
