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
        Text stageText,scoreText,bossText,help,partsText;
        Text[] stocks=new Text[5],partLabels=new Text[3],cheatLabels=new Text[4];
        Button[] uses=new Button[5];
        RectTransform safe;
        GameObject shopPanel,cheatPanel;
        Button cheatButton;
        static readonly string[] Names={"블랙홀","감속장","공격 반사","시간 정지","강제 유도"};
        static readonly string[] PartNames={"비상 동력원","터보 엔진","강화 선체"};
        static readonly string[] CheatNames={"충돌 범위","무적","전체 적 처치","돈 +1000"};
        void Start()
        {
            font=CampaignTypography.Get(font);
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=250;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var root=new GameObject("Safe area",typeof(RectTransform));root.transform.SetParent(transform,false);safe=root.GetComponent<RectTransform>();
            healthText=Label(Box(safe,"HP",new Vector2(0,1),new Vector2(0,1),new Vector2(245,65),new Vector2(16,-16)).transform,"HP",new Vector2(225,55),Vector2.zero,22);
            stageText=Label(Box(safe,"Stage",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(500,65),new Vector2(0,-16)).transform,"Stage",new Vector2(480,60),Vector2.zero,21);
            scoreText=Label(Box(safe,"Score",Vector2.one,Vector2.one,new Vector2(245,65),new Vector2(-16,-16)).transform,"Score",new Vector2(225,60),Vector2.zero,21);
            bossText=Label(safe,"Boss",new Vector2(620,40),new Vector2(0,-105),23);Anchor(bossText.gameObject,new Vector2(.5f,1));
            for(int i=0;i<5;i++)
            {
                int kind=i;uses[i]=MakeButton(safe,"Skill",new Vector2((i-2f)*178,58),new Vector2(168,78),()=>game.player.TryUsePatch(kind));
                Anchor(uses[i].gameObject,new Vector2(.5f,0));stocks[i]=uses[i].GetComponentInChildren<Text>();
            }
            help=Label(safe,"Controls",new Vector2(1120,55),new Vector2(0,130),19);Anchor(help.gameObject,new Vector2(.5f,0));
            partsText=Label(safe,"Parts",new Vector2(1000,35),new Vector2(-70,177),18);Anchor(partsText.gameObject,new Vector2(.5f,0));
            var cheats=cheatButton=MakeButton(safe,"치트 설정",new Vector2(-85,165),new Vector2(150,50),()=>cheatPanel.SetActive(!cheatPanel.activeSelf));Anchor(cheats.gameObject,new Vector2(1,0));
            shopPanel=Box(safe,"Stage shop",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(1060,530),Vector2.zero);
            Label(shopPanel.transform,"Title",new Vector2(1000,60),new Vector2(0,215),28).text="스테이지 클리어 · 파츠 상점";
            for(int i=0;i<3;i++)
            {
                int kind=i;partLabels[i]=Label(shopPanel.transform,"Part",new Vector2(550,65),new Vector2(-190,115-i*95),22);
                MakeButton(shopPanel.transform,"구매 / 장착·해제",new Vector2(305,115-i*95),new Vector2(330,65),()=>BuyOrToggle(kind));
            }
            Label(shopPanel.transform,"Info",new Vector2(980,60),new Vector2(0,-155),19).text="비상 동력원: 치명상 시 HP4로 1회 부활 · 엔진: 속도 +2 · 선체: 보스 충돌 피해 +10";
            MakeButton(shopPanel.transform,"다음 스테이지",new Vector2(0,-225),new Vector2(460,55),game.ContinueAfterShop);shopPanel.SetActive(false);
            cheatPanel=Box(safe,"Cheats",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(850,490),Vector2.zero);
            Label(cheatPanel.transform,"Title",new Vector2(800,55),new Vector2(0,195),25).text="치트키 설정 · 버튼으로 F1~F12 순환 (중복 제외)";
            for(int i=0;i<4;i++){int action=i;var b=MakeButton(cheatPanel.transform,"Cheat",new Vector2(0,110-i*70),new Vector2(720,55),()=>game.GetComponent<CampaignTools>().CycleBinding(action));cheatLabels[i]=b.GetComponentInChildren<Text>();}
            MakeButton(cheatPanel.transform,"닫기",new Vector2(0,-190),new Vector2(300,50),()=>cheatPanel.SetActive(false));cheatPanel.SetActive(false);
            // Existing flow dialogs also remain fully inside 4:3 and ultrawide screens.
            foreach(var s in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(s!=scaler){s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;}
        }
        void BuyOrToggle(int kind)
        {
            if(!game.BetweenStages)return;
            var s=game.shop;int owned=kind==0?s.part1:kind==1?s.part2:s.part3;
            if(owned>0){game.player.TogglePart(kind);return;}
            game.TryBuyPart(kind);
        }
        public void OpenPatches(){ } // Serialized compatibility; stock is usable without equipment slots.
        public void ClosePatches(){ }
        void Update()
        {
            if(safe==null||game.player==null)return;
            Rect area=Screen.safeArea;safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
            safe.gameObject.SetActive(!game.Ended);
            shopPanel.SetActive(game.BetweenStages);cheatButton.gameObject.SetActive(!game.BetweenStages);
            if(game.BetweenStages){game.shop.shopMenu.SetActive(false);game.intermissionPanel.SetActive(false);}
            healthText.text="HP "+game.player.Health+" / 8\n방어막 "+game.player.data.HaveShield;
            stageText.text="STAGE "+game.director.stageNumber+" / 3 · "+new[]{"생존","잔여 적 정리","보스 대기","보스전","클리어"}[(int)game.director.State]+"\n"+game.director.Elapsed.ToString("000")+" / 300초 · WAVE "+game.director.Wave;
            scoreText.text="점수 "+game.score.score+"\n재화 "+game.shop.coin;
            var boss=FindAnyObjectByType<IntegratedBossTarget>();bossText.text=boss==null?"":"BOSS HP "+boss.CurrentHealth+" / "+boss.MaxHealth+(boss.ContactHazard?" · 붉은 경고: 접촉 주의":" · 몸통 충돌 기회");
            for(int i=0;i<5;i++){float cd=game.player.PatchCooldown(i);stocks[i].text=(i+1)+" · "+Names[i]+"\n×"+game.player.PatchCount(i)+" · "+(cd>0?cd.ToString("0.0")+"초":"사용 가능");uses[i].interactable=game.Running&&game.player.PatchCount(i)>0&&cd<=0;uses[i].gameObject.SetActive(!game.BetweenStages);}
            help.gameObject.SetActive(!game.BetweenStages);partsText.gameObject.SetActive(!game.BetweenStages);
            help.text="WASD / 방향키 이동 · 1~5 스킬 · Space 폭탄 ×"+game.player.bombs+" · Esc 일시정지 · 적 미사일을 적에게 유도";
            string partState="장착: ";for(int i=0;i<3;i++){int owned=i==0?game.shop.part1:i==1?game.shop.part2:game.shop.part3;bool equipped=(game.player.equippedParts&(1<<i))!=0;partLabels[i].text=PartNames[i]+" · "+(owned==0?"1000벨":equipped?"장착 중":"보유 / 미장착");if(equipped)partState+=PartNames[i]+"  ";}partsText.text=partState;
            var tools=game.GetComponent<CampaignTools>();for(int i=0;i<4;i++)cheatLabels[i].text=CheatNames[i]+" : "+tools.keys[i]+(i==0?(tools.showColliders?" ON":" OFF"):i==1?(game.player.cheatInvulnerable?" ON":" OFF"):"");
        }
        void OnGUI()
        {
            if(Camera.main==null||game==null||!game.Running)return;
            foreach(var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))
            {
                Vector3 p=Camera.main.WorldToScreenPoint(enemy.transform.position+Vector3.up*.8f);if(p.z<0)continue;
                if(game.director.stageNumber==3&&Vector2.Distance(p,Camera.main.WorldToScreenPoint(game.player.transform.position))>Mathf.Min(Screen.width,Screen.height)*.22f)continue;
                Rect r=new Rect(p.x-32,Screen.height-p.y-8,64,6);GUI.color=Color.gray;GUI.DrawTexture(r,Texture2D.whiteTexture);r.width*=enemy.CurrentHealth/(float)Mathf.Max(1,enemy.maxHealth);GUI.color=Color.red;GUI.DrawTexture(r,Texture2D.whiteTexture);
            }GUI.color=Color.white;
        }
        static void Anchor(GameObject go,Vector2 anchor){var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;}
        GameObject Box(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.sizeDelta=size;rt.anchoredPosition=position;go.GetComponent<Image>().color=new Color(.025f,.045f,.075f,.96f);return go;
        }
        Text Label(Transform parent,string name,Vector2 size,Vector2 position,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.sizeDelta=size;rt.anchoredPosition=position;var t=go.GetComponent<Text>();t.font=font;t.fontSize=fontSize;t.resizeTextForBestFit=true;t.resizeTextMinSize=14;t.resizeTextMaxSize=fontSize;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
        }
        Button MakeButton(Transform parent,string label,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var go=Box(parent,label,new Vector2(.5f,.5f),new Vector2(.5f,.5f),size,position);go.GetComponent<Image>().color=new Color(.12f,.24f,.32f);var b=go.AddComponent<Button>();b.onClick.AddListener(action);Label(go.transform,"Label",size,Vector2.zero,21).text=label;return b;
        }
    }
}
