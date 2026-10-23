using UnityEngine;

namespace Taeyang
{
    // Tiny runtime-only hit flash; does not need a particle system or another prefab.
    public class SimpleImpact : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float left = 0.25f;
        private float originalSize;
        private Color color;

        public static void Spawn(Vector3 position, Color tint, float size)
        {
            GameObject obj = new GameObject("Projectile_Impact");
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * Mathf.Max(0.1f, size);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = ImpactSprite;
            renderer.sortingOrder = 40;
            renderer.color = tint;
            SimpleImpact fx = obj.AddComponent<SimpleImpact>();
            fx.sr = renderer;
            fx.originalSize = obj.transform.localScale.x;
            fx.color = tint;
        }

        private static Sprite generated;
        private static Sprite ImpactSprite
        {
            get
            {
                if (generated != null) return generated;
                Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                Color[] pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                    float alpha = Mathf.Clamp01((15.5f - dist) / 7.5f);
                    pixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
                }
                tex.SetPixels(pixels);
                tex.Apply();
                generated = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
                return generated;
            }
        }

        private void Update()
        {
            left -= Time.deltaTime;
            float t = Mathf.Clamp01(left / 0.25f);
            transform.localScale = Vector3.one * (originalSize * (1f + (1f - t) * 0.8f));
            color.a = t;
            if (sr != null) sr.color = color;
            if (left <= 0f) Destroy(gameObject);
        }
    }
}
