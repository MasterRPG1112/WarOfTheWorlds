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
        [Tooltip("-1 equips all previously owned parts for legacy saves. Bits: emergency / engine / armor.")]
        public int equippedPartsMask = -1;
        public int OwnedPartsMask => (part1 > 0 ? 1 : 0) | (part2 > 0 ? 2 : 0) | (part3 > 0 ? 4 : 0);
        public int EquippedPartsMask => equippedPartsMask < 0 ? OwnedPartsMask : equippedPartsMask & OwnedPartsMask & 7;
        static readonly string[] PartNames = { "비상 동력원", "터보 엔진", "방호 철갑탄" };

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
            var campaign = Integration.IntegratedGame.Active;
            if (campaign != null && campaign.shop == this)
            {
                // The team's scene stores the Menu host inactive. Activating only
                // its ShopMenu child cannot display the shop or enable purchases.
                // Keep the original hierarchy and artwork, with its panel hidden.
                if (shopMenu != null) shopMenu.SetActive(false);
                gameObject.SetActive(true);
            }
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
            var campaign = Integration.IntegratedGame.Active;
            if (campaign != null && campaign.shop == this) { campaign.CloseShop(); return; }
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
            SelectPart(0);
        }

        public void SpeedBtn()
        {
            SelectPart(1);
        }

        public void HpBtn()
        {
            SelectPart(2);
        }

        void SelectPart(int kind)
        {
            if ((OwnedPartsMask & (1 << kind)) != 0) TogglePart(kind);
            else TryBuyPart(kind);
        }

        public bool IsPartEquipped(int kind) => kind >= 0 && kind < 3 && (EquippedPartsMask & (1 << kind)) != 0;

        public bool TogglePart(int kind)
        {
            if (!CanPurchase || kind < 0 || kind > 2 || (OwnedPartsMask & (1 << kind)) == 0) return false;
            equippedPartsMask = EquippedPartsMask ^ (1 << kind);
            SyncLegacyData(true);
            RefreshPartEffects();
            ReloadUI();
            return true;
        }

        public bool ConsumeEmergencyPart()
        {
            if (!IsPartEquipped(0)) return false;
            equippedPartsMask = EquippedPartsMask & ~1;
            part1 = 0;
            SyncLegacyData(false);
            ReloadUI();
            return true;
        }

        bool CanPurchase
        {
            get
            {
                var game = Integration.IntegratedGame.Active;
                return game == null || (game.shop == this && game.ShopAccessible && !game.Ended && shopMenu != null && shopMenu.activeInHierarchy);
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
            int equipped = EquippedPartsMask;
            coin -= cost;
            if (kind == 0) part1 = 1;
            else if (kind == 1) part2 = 1;
            else part3 = 1;
            equippedPartsMask = equipped | (1 << kind);
            // Preserve the existing one-part purchase allowance per cleared stage.
            partLicense = 0;
            SyncLegacyData(true);
            RefreshPartEffects();
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

        void RefreshPartEffects()
        {
            var campaign = Integration.IntegratedGame.Active;
            if (campaign != null && campaign.shop == this && campaign.player != null) campaign.player.RefreshPartEffects();
        }

        public void RefreshUI() => ReloadUI();

        void ReloadUI()
        {
            if (coinUI != null) coinUI.text = "남은 돈 : " + coin + "벨";
            if (shopMenu == null) return;
            // The original SF Button has a nested Background/Label Text. Match its
            // serialized action so both existing columns retain their exact design.
            foreach (var button in shopMenu.GetComponentsInChildren<Button>(true))
                for (int action = 0; action < button.onClick.GetPersistentEventCount(); action++)
                {
                    if (button.onClick.GetPersistentTarget(action) != this) continue;
                    string method = button.onClick.GetPersistentMethodName(action);
                    int kind = method == nameof(LifeBtn) ? 0 : method == nameof(SpeedBtn) ? 1 : method == nameof(HpBtn) ? 2 : -1;
                    if (kind < 0) continue;
                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null) label.text = PartNames[kind] + "\n" +
                        ((OwnedPartsMask & (1 << kind)) == 0 ? Mathf.Max(0, partCost) + "벨 구매" : IsPartEquipped(kind) ? "장착 중 · 해제" : "미장착 · 장착");
                    break;
                }
        }
    }
}
