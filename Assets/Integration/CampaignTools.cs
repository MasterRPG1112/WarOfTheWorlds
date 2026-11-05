using UnityEngine;
using Taeyang;
using UnityEngine.UI;
namespace Integration
{
    public class CampaignTools : MonoBehaviour
    {
        public KeyCode[] keys = { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4 };
        public bool showColliders;
        public int moneyAmount = 1000;
        Texture2D fog;
        GameObject fogRoot;
        RectTransform[] fogRects = new RectTransform[5];
        IntegratedGame game;
        public bool FogActive => game != null && game.director.stageNumber == 3 && !game.Ended;
        void Awake()
        {
            game = GetComponent<IntegratedGame>();
            fog = new Texture2D(128,128,TextureFormat.RGBA32,false);
            var pixels = new Color[128*128];
            for(int y=0;y<128;y++) for(int x=0;x<128;x++)
            {
                float d = new Vector2((x-63.5f)/63.5f,(y-63.5f)/63.5f).magnitude;
                pixels[y*128+x] = new Color(0,0,0,Mathf.SmoothStep(0, .94f, Mathf.InverseLerp(.5f,1,d)));
            }
            fog.SetPixels(pixels);fog.Apply();fog.wrapMode=TextureWrapMode.Clamp;
            fogRoot = new GameObject("Stage 3 nebula veil", typeof(Canvas));
            var canvas = fogRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            for (int i=0;i<5;i++)
            {
                var go=new GameObject("Fog region",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(fogRoot.transform,false);
                fogRects[i]=go.GetComponent<RectTransform>();fogRects[i].anchorMin=fogRects[i].anchorMax=fogRects[i].pivot=Vector2.zero;
                var image=go.GetComponent<RawImage>();image.raycastTarget=false;image.texture=i==0?fog:Texture2D.whiteTexture;image.color=i==0?Color.white:new Color(0,0,0,.94f);
            }
            var saved=IntegrationSaveStore.Data.cheatKeys;
            if(saved!=null&&saved.Length==4)for(int i=0;i<4;i++)keys[i]=(KeyCode)saved[i];
        }
        void Update()
        {
            if (game == null || game.Ended) return;
            for (int i=0;i<4;i++) if(Input.GetKeyDown(keys[i])) Execute(i);
        }
        public bool Bind(int action, KeyCode key)
        {
            if(action<0||action>=4||key<KeyCode.F1||key>KeyCode.F12)return false;
            for(int i=0;i<4;i++)if(i!=action&&keys[i]==key)return false;
            keys[action]=key;
            IntegrationSaveStore.Data.cheatKeys=System.Array.ConvertAll(keys,k=>(int)k);IntegrationSaveStore.Save();return true;
        }
        public void CycleBinding(int action)
        {
            int start=(int)keys[action]-(int)KeyCode.F1;
            for(int i=1;i<=12;i++)if(Bind(action,(KeyCode)((int)KeyCode.F1+(start+i)%12)))return;
        }
        public void Execute(int action)
        {
            if(game == null || game.Ended)return;
            if(action==0)showColliders=!showColliders;
            else if(action==1)game.player.cheatInvulnerable=!game.player.cheatInvulnerable;
            else if(action==2 && game.Running)
            {
                foreach(var e in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))e.TakeDamage(int.MaxValue,Vector2.zero);
                foreach(var boss in FindObjectsByType<IntegratedBossTarget>(FindObjectsSortMode.None))boss.TakeRamDamage(int.MaxValue);
            }
            else if(action==3)game.shop.coin=(int)System.Math.Min(int.MaxValue,(long)game.shop.coin+moneyAmount);
        }
        void LateUpdate()
        {
            RefreshFog(Screen.width,Screen.height);
        }
        public void RefreshFog(int width,int height)
        {
            if(fogRoot==null)return;fogRoot.SetActive(FogActive);
            if(!FogActive||Camera.main==null)return;
            Vector3 view=Camera.main.WorldToViewportPoint(game.player.transform.position);
            Vector3 p=new Vector3(view.x*width,view.y*height,0);
            float radius=Mathf.Min(width,height)*.32f,x=p.x-radius,y=p.y-radius,size=radius*2;
            Region(0,x,y,size,size,width,height);Region(1,0,0,width,Mathf.Max(0,y),width,height);Region(2,0,y+size,width,Mathf.Max(0,height-y-size),width,height);
            Region(3,0,y,Mathf.Max(0,x),size,width,height);Region(4,x+size,y,Mathf.Max(0,width-x-size),size,width,height);
        }
        void Region(int i,float x,float y,float w,float h,int width,int height){fogRects[i].anchorMin=new Vector2(x/width,y/height);fogRects[i].anchorMax=new Vector2((x+w)/width,(y+h)/height);fogRects[i].offsetMin=fogRects[i].offsetMax=Vector2.zero;}
        void OnGUI()
        {
            if(game==null||Camera.main==null)return;
            GUI.depth=100;
            if(showColliders)
            {
                GUI.depth=-100;GUI.color=Color.green;
                foreach(var c in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                {
                    if(!c.enabled)continue;
                    if(c is CircleCollider2D circle)
                    {
                        for(int i=0;i<40;i++)Line(c.transform.TransformPoint(circle.offset+Unit(i/40f)*circle.radius),c.transform.TransformPoint(circle.offset+Unit((i+1)/40f)*circle.radius));
                    }
                    else if(c is PolygonCollider2D poly)
                    { for(int path=0;path<poly.pathCount;path++){var v=poly.GetPath(path);for(int i=0;i<v.Length;i++)Line(c.transform.TransformPoint(v[i]+poly.offset),c.transform.TransformPoint(v[(i+1)%v.Length]+poly.offset));} }
                    else if(c is BoxCollider2D box)
                    {
                        Vector2 h=box.size*.5f;Vector2[] v={new Vector2(-h.x,-h.y),new Vector2(h.x,-h.y),new Vector2(h.x,h.y),new Vector2(-h.x,h.y)};
                        for(int i=0;i<4;i++)Line(c.transform.TransformPoint(v[i]+box.offset),c.transform.TransformPoint(v[(i+1)%4]+box.offset));
                    }
                    else {var b=c.bounds;Line(b.min,new Vector3(b.max.x,b.min.y));Line(new Vector3(b.max.x,b.min.y),b.max);Line(b.max,new Vector3(b.min.x,b.max.y));Line(new Vector3(b.min.x,b.max.y),b.min);}
                }
            }
            GUI.color=Color.white;
        }
        static Vector2 Unit(float turn)=>new Vector2(Mathf.Cos(turn*Mathf.PI*2),Mathf.Sin(turn*Mathf.PI*2));
        static void Line(Vector3 a,Vector3 b)
        {
            a=Camera.main.WorldToScreenPoint(a);b=Camera.main.WorldToScreenPoint(b);a.y=Screen.height-a.y;b.y=Screen.height-b.y;
            Vector2 delta=b-a;var old=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            GUI.DrawTexture(new Rect(a.x,a.y,delta.magnitude,2),Texture2D.whiteTexture);GUI.matrix=old;
        }
        void OnDestroy(){if(fogRoot!=null)Destroy(fogRoot);if(fog!=null)Destroy(fog);}
    }
}
