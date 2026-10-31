using UnityEngine;

namespace Integration
{
    // All integrated movement/spawn limits derive from the same stationary camera.
    public static class BattleArena
    {
        public const float HalfHeight = 8f;
        public const float SpawnMargin = 3.5f;

        public static Rect ViewBounds(Camera camera = null)
        {
            if (camera == null) camera = Camera.main;
            float halfHeight = camera != null && camera.orthographic ? camera.orthographicSize : HalfHeight;
            float aspect = camera != null ? camera.aspect : 16f / 9f;
            Vector2 center = camera != null ? (Vector2)camera.transform.position : Vector2.zero;
            float halfWidth = halfHeight * Mathf.Max(.1f, aspect);
            return Rect.MinMaxRect(center.x - halfWidth, center.y - halfHeight,
                center.x + halfWidth, center.y + halfHeight);
        }

        public static Rect PlayerBounds(Camera camera = null, float margin = .8f)
        {
            Rect view = ViewBounds(camera);
            margin = Mathf.Clamp(margin, 0, Mathf.Min(view.width, view.height) * .45f);
            return Rect.MinMaxRect(view.xMin + margin, view.yMin + margin,
                view.xMax - margin, view.yMax - margin);
        }

        // Clockwise, starting above the camera: N, NE, E, SE, S, SW, W, NW.
        public static Vector2 Direction(int sector)
        {
            switch ((sector % 8 + 8) % 8)
            {
                case 0: return Vector2.up;
                case 1: return new Vector2(1, 1).normalized;
                case 2: return Vector2.right;
                case 3: return new Vector2(1, -1).normalized;
                case 4: return Vector2.down;
                case 5: return new Vector2(-1, -1).normalized;
                case 6: return Vector2.left;
                default: return new Vector2(-1, 1).normalized;
            }
        }

        public static Vector2 SpawnPosition(int direction, int member = 0, int count = 1)
        {
            Rect view = ViewBounds();
            Vector2 outward = Direction(direction);
            float offset = (member - (Mathf.Max(1, count) - 1) * .5f) * 1.5f;
            bool diagonal = Mathf.Abs(outward.x) > .1f && Mathf.Abs(outward.y) > .1f;
            if (diagonal)
            {
                Vector2 corner = new Vector2(outward.x > 0 ? view.xMax + SpawnMargin : view.xMin - SpawnMargin,
                    outward.y > 0 ? view.yMax + SpawnMargin : view.yMin - SpawnMargin);
                return corner + new Vector2(-outward.y, outward.x) * offset;
            }
            if (outward.y != 0)
                return new Vector2(Mathf.Clamp(view.center.x + offset, view.xMin + .5f, view.xMax - .5f),
                    outward.y > 0 ? view.yMax + SpawnMargin : view.yMin - SpawnMargin);
            return new Vector2(outward.x > 0 ? view.xMax + SpawnMargin : view.xMin - SpawnMargin,
                Mathf.Clamp(view.center.y + offset, view.yMin + .5f, view.yMax - .5f));
        }
    }
}
