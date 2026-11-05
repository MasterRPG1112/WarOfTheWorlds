using UnityEngine;

namespace Integration
{
    // World sprites stay above the stage fog and below the team's overlay UI.
    public static class BossTelegraph
    {
        static Sprite rectangle;
        static Sprite Rectangle
        {
            get
            {
                if (rectangle != null) return rectangle;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white); texture.Apply();
                rectangle = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
                return rectangle;
            }
        }
        public static void Line(Transform parent, Vector2 start, Vector2 end, float width, Color color)
        {
            var visual = new GameObject("Warning lane").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(parent, false);
            visual.sprite = Rectangle;
            Vector2 difference = end - start;
            visual.transform.position = (start + end) * .5f;
            visual.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
            visual.transform.localScale = new Vector3(difference.magnitude, width, 1);
            visual.color = color;
            visual.sortingOrder = 180;
        }
        public static void Circle(Transform parent, Vector2 center, float radius, Color color)
        {
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
                Line(parent, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, .07f, color);
            }
        }
    }
}
