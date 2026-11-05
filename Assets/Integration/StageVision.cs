using UnityEngine;

namespace Integration
{
    // A world-space soft mask preserves HUD visibility and a transparent player bubble.
    public class StageVision : MonoBehaviour
    {
        [Min(3)] public float visibleRadius = 4.25f;
        [Range(0, .65f)] public float baseOpacity = .55f;
        [Range(0, .78f)] public float ultimateOpacity = .72f;
        SpriteRenderer overlay;
        Texture2D texture;
        Sprite sprite;
        float boostedFor;
        public void Boost(float seconds) => boostedFor = Mathf.Max(boostedFor, Mathf.Max(0, seconds));
        void Start()
        {
            visibleRadius = Mathf.Max(3, visibleRadius);
            Rect view = BattleArena.ViewBounds();
            float extent = Mathf.Max(view.width, view.height) * 2 + visibleRadius * 2;
            texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float distance = new Vector2((x - 127.5f) / 256 * extent, (y - 127.5f) / 256 * extent).magnitude;
                float alpha = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(visibleRadius, visibleRadius + 1.75f, distance));
                pixels[y * 256 + x] = new Color(0, 0, .025f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 256, 256), Vector2.one * .5f, 256 / extent);
            overlay = new GameObject("Nebula visibility mask").AddComponent<SpriteRenderer>();
            overlay.transform.SetParent(transform, false);
            overlay.sprite = sprite;
            overlay.sortingOrder = 100;
        }
        void LateUpdate()
        {
            var game = IntegratedGame.Active;
            if (overlay == null || game == null || game.player == null) return;
            overlay.transform.position = game.player.transform.position;
            overlay.enabled = !game.Ended && !game.BetweenStages;
            if (game.Running && !game.EnemiesFrozen) boostedFor = Mathf.Max(0, boostedFor - Time.deltaTime);
            overlay.color = new Color(1, 1, 1, Mathf.Clamp(boostedFor > 0 ? ultimateOpacity : baseOpacity, 0, .78f));
        }
        void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
