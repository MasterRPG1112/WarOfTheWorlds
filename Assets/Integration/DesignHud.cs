using UnityEngine;
using UnityEngine.UI;
using Taeyang;
namespace Integration
{
    public class DesignHud : MonoBehaviour
    {
        public IntegratedGame game;
        public Font font;
        public Text healthText;
        public Image healthFill;
        Text stageText, scoreText, bossText;
        Text[] slots = new Text[2], rows = new Text[4];
        Button[] use = new Button[2];
        GameObject patchPanel;
        static readonly string[] Names = { "블랙홀", "감속장", "공격 반사", "시간 정지" };
        void Start()
        {
            // 화면 해상도 변화 및 모서리 밀착에 가장 안정적인 Overlay 모드 사용
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 230;

            gameObject.AddComponent<GraphicRaycaster>();

            // [핵심] CanvasScaler 설정: 해상도 비율 변화에 UI가 유연하게 대응하도록 설정
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // matchWidthOrHeight를 0.5f로 두어 가로/세로 비율 변화에 양쪽 모두 균형 있게 반응하도록 함
            scaler.matchWidthOrHeight = 0.5f;

            // --- 보이는 화면 모서리에 딱 밀착되는 앵커 기반 배치 ---

            // 1. 공통 체력 (좌측 상단 모서리 밀착)
            var hp = Box(transform, "공통 체력", new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, 80), new Vector2(30, -30));
            healthText = Label(hp.transform, "HP", new Vector2(260, 30), new Vector2(0, 18), 22);
            var bar = Box(hp.transform, "HP track", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(250, 18), new Vector2(0, -15));
            healthFill = Box(bar.transform, "HP fill", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(250, 18), Vector2.zero).GetComponent<Image>();
            healthFill.type = Image.Type.Filled; healthFill.fillMethod = Image.FillMethod.Horizontal; healthFill.color = new Color(.2f, .8f, .3f);
            healthFill.rectTransform.pivot = new Vector2(0, .5f); healthFill.rectTransform.anchoredPosition = Vector2.zero;

            // 2. 진행 상황 및 스테이지 정보 (상단 중앙 고정)
            stageText = Label(Box(transform, "진행", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(650, 90), new Vector2(0, -30)).transform, "진행", new Vector2(620, 80), Vector2.zero, 20);

            // 3. 점수 및 재화 (우측 상단 모서리 밀착)
            var scoreBox = Box(transform, "점수/재화", new Vector2(1, 1), new Vector2(1, 1), new Vector2(280, 80), new Vector2(-30, -30));
            scoreText = Label(scoreBox.transform, "점수/재화", new Vector2(260, 70), Vector2.zero, 19);

            // 4. 보스 체력 (상단 중앙 아래)
            bossText = Label(transform, "보스 체력", new Vector2(400, 35), new Vector2(0, -135), 20);
            bossText.rectTransform.anchorMin = bossText.rectTransform.anchorMax = new Vector2(.5f, 1);

            // 5. 하단 Q / E 패치 슬롯 버튼 (하단 중앙 양옆)
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                var b = MakeButton(transform, "슬롯 " + (i + 1), new Vector2(i == 0 ? -180 : 180, 40), new Vector2(320, 70), () => game.player.TryUsePatch(game.player.equippedPatches[slot]));
                var rt = b.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0); use[i] = b; slots[i] = b.GetComponentInChildren<Text>();
            }

            // 6. 패치 장착 버튼 (P) (우측 하단 모서리 밀착)
            var manage = MakeButton(transform, "패치 장착 (P)", new Vector2(-30, 40), new Vector2(210, 55), OpenPatches);
            manage.GetComponent<RectTransform>().anchorMin = manage.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);

            // 7. 조작 설명 (좌측 하단 모서리 밀착)
            var help = Label(transform, "조작", new Vector2(310, 55), new Vector2(30, 40), 15);
            help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(0, 0);
            help.text = "WASD / 방향키 이동 · 적 미사일 유도\nQ / E 장착 패치 · Esc 일시정지";
            // -------------------------------------------------------------

            patchPanel = Box(transform, "패치 장착", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(1000, 570), Vector2.zero);
            Label(patchPanel.transform, "제목", new Vector2(900, 60), new Vector2(0, 225), 28).text = "보유 패치 장착 · 최대 2개";
            for (int i = 0; i < 4; i++)
            {
                int kind = i; rows[i] = Label(patchPanel.transform, Names[i], new Vector2(480, 65), new Vector2(-210, 135 - i * 85), 23);
                for (int j = 0; j < 2; j++) { int slot = j; MakeButton(patchPanel.transform, "슬롯 " + (j + 1), new Vector2(170 + j * 205, 135 - i * 85), new Vector2(185, 60), () => game.player.EquipPatch(kind, slot)); }
            }
            MakeButton(patchPanel.transform, "뒤로", new Vector2(0, -215), new Vector2(400, 60), ClosePatches);
            patchPanel.SetActive(false);
        }
        public void OpenPatches()
        {
            if (game.Ended) return;
            if (game.Running) game.TogglePause();
            game.pausePanel.SetActive(false); patchPanel.SetActive(true);
        }
        public void ClosePatches() { patchPanel.SetActive(false); if (!game.BetweenStages && !game.Ended) game.pausePanel.SetActive(true); }
        void Update()
        {
            if (healthText == null || game.player == null) return;
            if (game.Running && patchPanel.activeSelf) patchPanel.SetActive(false);
            healthText.text = "HP " + game.player.Health + " / 8"; healthFill.fillAmount = game.player.Health / 8f;
            healthFill.rectTransform.sizeDelta = new Vector2(250 * game.player.Health / 8f, 18);
            stageText.text = "STAGE " + game.director.stageNumber + " / 3  WAVE " + game.director.Wave + " / 5\n생존 " + game.director.Elapsed.ToString("000") + " / 300초" + (game.BetweenStages ? " · 스테이지 클리어 / 상점" : "");
            if (game.director.allowMissingBossTestSkip && game.director.bossPrefab == null &&
                (game.director.campaignBossPrefabs == null || game.director.campaignBossPrefabs.Length < game.director.stageNumber || game.director.campaignBossPrefabs[game.director.stageNumber - 1] == null)) stageText.text += "\n보스 미등록 · 테스트 진행 경로";
            scoreText.text = "점수 " + game.score.score + "  |  재화 " + game.shop.coin;
            var boss = FindAnyObjectByType<IntegratedBossTarget>(); bossText.text = boss == null ? "" : "BOSS HP " + boss.CurrentHealth + " / " + boss.MaxHealth;
            for (int i = 0; i < 2; i++)
            {
                int k = game.player.equippedPatches[i];
                slots[i].text = (i == 0 ? "Q" : "E") + " · " + (k < 0 ? "미장착" : Names[k] + " ×" + game.player.PatchCount(k) + "\n" + (game.player.PatchCooldown(k) > 0 ? game.player.PatchCooldown(k).ToString("0.0") + "초" : "사용 가능"));
                use[i].interactable = game.Running && k >= 0 && game.player.PatchCount(k) > 0 && game.player.PatchCooldown(k) <= 0;
            }
            for (int k = 0; k < 4; k++) rows[k].text = Names[k] + " ×" + game.player.PatchCount(k) + "  쿨타임 " + game.player.PatchCooldown(k).ToString("0.0") + "초" + (game.player.IsEquipped(k) ? " (장착)" : "");
        }
        void OnGUI()
        {
            if (Camera.main == null || game == null || !game.Running) return;
            foreach (var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))
            {
                Vector3 p = Camera.main.WorldToScreenPoint(enemy.transform.position + Vector3.up * .8f);
                if (p.z < 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height) continue;
                Rect r = new Rect(p.x - 40, Screen.height - p.y - 16, 80, 7); GUI.color = Color.gray; GUI.DrawTexture(r, Texture2D.whiteTexture);
                r.width *= enemy.CurrentHealth / (float)Mathf.Max(1, enemy.maxHealth); GUI.color = Color.red; GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white; GUI.Label(new Rect(p.x - 40, Screen.height - p.y - 42, 120, 24), enemy.CurrentHealth + " / " + enemy.maxHealth);
            }
            GUI.color = Color.white;
        }
        GameObject Box(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = position;
            go.GetComponent<Image>().color = new Color(.035f, .035f, .045f, .94f); return go;
        }
        Text Label(Transform parent, string name, Vector2 size, Vector2 position, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = position;
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = fontSize; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; return t;
        }
        Button MakeButton(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = Box(parent, label, new Vector2(.5f, .5f), new Vector2(.5f, .5f), size, position); go.GetComponent<Image>().color = new Color(.2f, .23f, .28f);
            var b = go.AddComponent<Button>(); b.onClick.AddListener(action); Label(go.transform, "label", size, Vector2.zero, 23).text = label; return b;
        }
    }
}