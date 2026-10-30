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
        Text stageText,scoreText,bossText;
        Text[] slots=new Text[2], rows=new Text[4];
        Button[] use=new Button[2];
        GameObject patchPanel;
        static readonly string[] Names={"블랙홀","감속장","공격 반사","시간 정지"};
        void Start()
        {
            var canvas=gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=230;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var hp=Box(transform,"공통 체력",new Vector2(0,1),new Vector2(0,1),new Vector2(330,100),new Vector2(24,-20));
            healthText=Label(hp.transform,"HP",new Vector2(300,40),new Vector2(0,22),26);
            var bar=Box(hp.transform,"HP track",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(290,24),new Vector2(0,-22));
            healthFill=Box(bar.transform,"HP fill",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(290,24),Vector2.zero).GetComponent<Image>();
            healthFill.type=Image.Type.Filled;healthFill.fillMethod=Image.FillMethod.Horizontal;healthFill.color=new Color(.2f,.8f,.3f);
            healthFill.rectTransform.anchorMin=healthFill.rectTransform.anchorMax=new Vector2(0,.5f);healthFill.rectTransform.pivot=new Vector2(0,.5f);healthFill.rectTransform.anchoredPosition=Vector2.zero;
            stageText=Label(Box(transform,"진행",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(850,100),new Vector2(0,-20)).transform,"진행",new Vector2(820,90),Vector2.zero,25);
            scoreText=Label(Box(transform,"점수/재화",new Vector2(1,1),new Vector2(1,1),new Vector2(340,100),new Vector2(-24,-20)).transform,"점수/재화",new Vector2(310,90),Vector2.zero,25);
            bossText=Label(transform,"보스 체력",new Vector2(650,45),new Vector2(0,380),25);
            for(int i=0;i<2;i++)
            {
                int slot=i;
                var b=MakeButton(transform,"슬롯 "+(i+1),new Vector2(i==0?-215:215,70),new Vector2(400,90),()=>game.player.TryUsePatch(game.player.equippedPatches[slot]));
                var rt=b.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,0);use[i]=b;slots[i]=b.GetComponentInChildren<Text>();
            }
            var manage=MakeButton(transform,"패치 장착 (P)",new Vector2(-150,70),new Vector2(260,70),OpenPatches);
            manage.GetComponent<RectTransform>().anchorMin=manage.GetComponent<RectTransform>().anchorMax=new Vector2(1,0);
            var help=Label(transform,"조작",new Vector2(420,80),new Vector2(235,65),21);help.rectTransform.anchorMin=help.rectTransform.anchorMax=Vector2.zero;
            help.text="WASD / 방향키 이동 · 적 미사일 유도\nQ / E 장착 패치 · Esc 일시정지";
            patchPanel=Box(transform,"패치 장착",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(1000,570),Vector2.zero);
            Label(patchPanel.transform,"제목",new Vector2(900,60),new Vector2(0,225),28).text="보유 패치 장착 · 최대 2개";
            for(int i=0;i<4;i++)
            {
                int kind=i;rows[i]=Label(patchPanel.transform,Names[i],new Vector2(480,65),new Vector2(-210,135-i*85),23);
                for(int j=0;j<2;j++){int slot=j;MakeButton(patchPanel.transform,"슬롯 "+(j+1),new Vector2(170+j*205,135-i*85),new Vector2(185,60),()=>game.player.EquipPatch(kind,slot));}
            }
            MakeButton(patchPanel.transform,"뒤로",new Vector2(0,-215),new Vector2(400,60),ClosePatches);
            patchPanel.SetActive(false);
        }
        public void OpenPatches()
        {
            if(game.Ended)return;
            if(game.Running)game.TogglePause();
            game.pausePanel.SetActive(false);patchPanel.SetActive(true);
        }
        public void ClosePatches(){patchPanel.SetActive(false);if(!game.BetweenStages&&!game.Ended)game.pausePanel.SetActive(true);}
        void Update()
        {
            if(healthText==null||game.player==null)return;
            if(Input.GetKeyDown(KeyCode.P))OpenPatches();
            if(game.Running&&patchPanel.activeSelf)patchPanel.SetActive(false);
            healthText.text="HP "+game.player.Health+" / 8";healthFill.fillAmount=game.player.Health/8f;
            healthFill.rectTransform.sizeDelta=new Vector2(290*game.player.Health/8f,24);
            stageText.text="STAGE "+game.director.stageNumber+" / 3   WAVE "+game.director.Wave+" / 5\n생존 "+game.director.Elapsed.ToString("000")+" / 300초"+(game.BetweenStages?" · 스테이지 클리어 / 상점":"");
            if(game.director.allowMissingBossTestSkip&&game.director.bossPrefab==null&&
                (game.director.campaignBossPrefabs==null||game.director.campaignBossPrefabs.Length<game.director.stageNumber||game.director.campaignBossPrefabs[game.director.stageNumber-1]==null))stageText.text+="\n보스 미등록 · 테스트 진행 경로";
            scoreText.text="점수 "+game.score.score+"\n재화 "+game.shop.coin;
            var boss=FindAnyObjectByType<IntegratedBossTarget>();bossText.text=boss==null?"":"BOSS HP "+boss.CurrentHealth+" / "+boss.MaxHealth;
            for(int i=0;i<2;i++)
            {
                int k=game.player.equippedPatches[i];
                slots[i].text=(i==0?"Q":"E")+" · "+(k<0?"미장착":Names[k]+" ×"+game.player.PatchCount(k)+"\n"+(game.player.PatchCooldown(k)>0?game.player.PatchCooldown(k).ToString("0.0")+"초":"사용 가능"));
                use[i].interactable=game.Running&&k>=0&&game.player.PatchCount(k)>0&&game.player.PatchCooldown(k)<=0;
            }
            for(int k=0;k<4;k++)rows[k].text=Names[k]+" ×"+game.player.PatchCount(k)+"  쿨타임 "+game.player.PatchCooldown(k).ToString("0.0")+"초"+(game.player.IsEquipped(k)?" (장착)":"");
        }
        void OnGUI()
        {
            if(Camera.main==null||game==null||!game.Running)return;
            foreach(var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))
            {
                Vector3 p=Camera.main.WorldToScreenPoint(enemy.transform.position+Vector3.up*.8f);
                if(p.z<0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)continue;
                Rect r=new Rect(p.x-40,Screen.height-p.y-16,80,7);GUI.color=Color.gray;GUI.DrawTexture(r,Texture2D.whiteTexture);
                r.width*=enemy.CurrentHealth/(float)Mathf.Max(1,enemy.maxHealth);GUI.color=Color.red;GUI.DrawTexture(r,Texture2D.whiteTexture);
                GUI.color=Color.white;GUI.Label(new Rect(p.x-40,Screen.height-p.y-42,120,24),enemy.CurrentHealth+" / "+enemy.maxHealth);
            }
            GUI.color=Color.white;
        }
        GameObject Box(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.sizeDelta=size;rt.anchoredPosition=position;
            go.GetComponent<Image>().color=new Color(.035f,.035f,.045f,.94f);return go;
        }
        Text Label(Transform parent,string name,Vector2 size,Vector2 position,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.sizeDelta=size;rt.anchoredPosition=position;
            var t=go.GetComponent<Text>();t.font=font;t.fontSize=fontSize;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
        }
        Button MakeButton(Transform parent,string label,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var go=Box(parent,label,new Vector2(.5f,.5f),new Vector2(.5f,.5f),size,position);go.GetComponent<Image>().color=new Color(.2f,.23f,.28f);
            var b=go.AddComponent<Button>();b.onClick.AddListener(action);Label(go.transform,"label",size,Vector2.zero,23).text=label;return b;
        }
    }
}
