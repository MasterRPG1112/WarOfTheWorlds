using UnityEngine;
using UnityEngine.UI;
namespace Integration
{
    public class OpeningSequence : MonoBehaviour
    {
        static bool shown;
        public static bool SuppressForTests;
        public bool Playing => panel != null;
        GameObject panel;
        Text story;
        float age;
        readonly string[] lines={"3024년, 황폐해진 지구를 구하기 위한 항해.","정상 항로를 벗어난 우주선. 정체불명의 함대가 길을 막는다.","무기는 없다. 적의 미사일을 유도해 탈출하라.","세 구역의 지휘함을 돌파하고 지구로 돌아가라."};
        void Start()
        {
            if(shown||SuppressForTests)return;shown=true;
            panel=new GameObject("Opening",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            var scaler=panel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var background=new GameObject("Space",typeof(RectTransform),typeof(Image));background.transform.SetParent(panel.transform,false);
            var rt=background.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.sizeDelta=Vector2.zero;background.GetComponent<Image>().color=new Color(.01f,.02f,.06f);
            var font=CampaignTypography.Get(GetComponent<IntegratedMenu>().continueHint.font);
            story=MakeText(background.transform,"Story",font,new Vector2(1100,360),Vector2.zero,36);
            var title=MakeText(background.transform,"Title",font,new Vector2(1100,100),new Vector2(0,230),48);title.text="WAR OF THE WORLDS";
            var skip=new GameObject("Skip",typeof(RectTransform),typeof(Image),typeof(Button));skip.transform.SetParent(background.transform,false);
            rt=skip.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(400,60);rt.anchoredPosition=new Vector2(0,-245);skip.GetComponent<Image>().color=new Color(.1f,.2f,.3f);
            MakeText(skip.transform,"Label",font,new Vector2(400,60),Vector2.zero,26).text="건너뛰기 · Enter / Space";skip.GetComponent<Button>().onClick.AddListener(Finish);
        }
        public void Finish(){if(panel!=null)Destroy(panel);panel=null;}
        void Update()
        {
            if(panel==null)return;age+=Time.unscaledDeltaTime;
            if(age>=10||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Escape)){Finish();return;}
            story.text=lines[Mathf.Min(3,(int)(age/2.5f))];story.color=new Color(.7f,.9f,1,Mathf.Clamp01(Mathf.Repeat(age,2.5f)*3));
        }
        static Text MakeText(Transform parent,string name,Font font,Vector2 size,Vector2 pos,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.sizeDelta=size;rt.anchoredPosition=pos;
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;return text;
        }
        void OnDestroy(){Finish();}
    }
}
