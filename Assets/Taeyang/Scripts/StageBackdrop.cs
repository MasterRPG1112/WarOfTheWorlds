using UnityEngine;
namespace Taeyang
{
    public class StageBackdrop : MonoBehaviour
    {
        private Transform[] stars;
        private float[] speeds;
        private Rect starBounds = new Rect(-9,-6,18,12);
        private static Sprite[] shapes=new Sprite[10];
        public static Sprite MakeSprite(int sides)
        {
            if(shapes[sides] != null) return shapes[sides];
            var texture=new Texture2D(64,64); texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color[4096];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                Vector2 p=new Vector2(x-31.5f,y-31.5f)/30;
                float angle=Mathf.Atan2(p.y,p.x); float sector=2*Mathf.PI/sides;
                float radius=Mathf.Cos(Mathf.PI/sides)/Mathf.Cos(Mathf.Repeat(angle+Mathf.PI/sides,sector)-Mathf.PI/sides);
                pixels[y*64+x]=p.magnitude<radius ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply();
            return shapes[sides]=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),64);
        }
        public void Initialize(int stage, bool expanded = false)
        {
            if(Camera.main != null) Camera.main.backgroundColor=stage==1 ? new Color(.06f,.025f,.015f) : stage==2 ? new Color(.012f,.005f,.035f) : new Color(.015f,.07f,.13f);
            if (expanded)
            {
                Rect view = Integration.BattleArena.ViewBounds();
                starBounds = Rect.MinMaxRect(view.xMin-1,view.yMin-1,view.xMax+1,view.yMax+1);
            }
            stars=new Transform[110]; speeds=new float[110];
            var random=new System.Random(800+stage);
            for(int i=0;i<stars.Length;i++)
            {
                var star=new GameObject("Parallax star"); star.transform.SetParent(transform);
                star.transform.position=new Vector3(starBounds.xMin+(float)random.NextDouble()*starBounds.width,
                    starBounds.yMin+(float)random.NextDouble()*starBounds.height,0);
                float size=.025f+(float)random.NextDouble()*.065f;
                star.transform.localScale=new Vector3(size,stage==3 ? size*3 : size,1);
                var r=star.AddComponent<SpriteRenderer>(); r.sprite=MakeSprite(6); r.sortingOrder=-20;
                r.color=stage==1 ? new Color(1,.65f,.4f,.7f) : stage==2 ? new Color(.55f,.4f,.85f,.5f) : new Color(.6f,.9f,1,.8f);
                stars[i]=star.transform; speeds[i]=.15f+size*5;
            }
        }
        private void Update()
        {
            if(stars==null) return;
            for(int i=0;i<stars.Length;i++) { stars[i].position+=Vector3.down*speeds[i]*Time.deltaTime; if(stars[i].position.y < starBounds.yMin) stars[i].position+=Vector3.up*starBounds.height; }
        }
    }
}
