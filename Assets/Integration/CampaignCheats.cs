using UnityEngine;
using Taeyang;

namespace Integration
{
    public class CampaignCheats : MonoBehaviour
    {
        [Tooltip("Development builds only. Default off; use a separate practice run.")]
        public bool enableDevelopmentCheats;
        public KeyCode debugKey = KeyCode.F1, invulnerableKey = KeyCode.F2,
            defeatEnemiesKey = KeyCode.F3, moneyKey = KeyCode.F4;
        [Min(1)] public int moneyAmount = 1000;
        public bool ShowColliders { get; private set; }
        public bool Allowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return enableDevelopmentCheats;
#else
                return false;
#endif
            }
        }
        void Update()
        {
            var game = IntegratedGame.Active;
            if (!Allowed)
            {
                ShowColliders = false;
                if (game != null && game.player != null) game.player.CheatInvulnerable = false;
                return;
            }
            if (game == null || !game.Running) return;
            if (game.RankedRun) game.MarkPracticeRun();
            if (Pressed(debugKey)) { ShowColliders = !ShowColliders; game.MarkPracticeRun(); }
            if (Pressed(invulnerableKey)) { game.player.CheatInvulnerable = !game.player.CheatInvulnerable; game.MarkPracticeRun(); }
            if (Pressed(defeatEnemiesKey))
            {
                game.MarkPracticeRun();
                Rect view = BattleArena.ViewBounds();
                foreach (var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))
                    if (enemy.GetComponentInParent<IntegratedBossTarget>() == null && view.Contains(enemy.transform.position))
                        enemy.TakeDamage(int.MaxValue, Vector2.zero);
            }
            if (Pressed(moneyKey))
            {
                game.MarkPracticeRun();
                game.shop.coin = (int)System.Math.Min(int.MaxValue, (long)game.shop.coin + Mathf.Max(1, moneyAmount));
                game.shop.SyncLegacyData(false);
            }
        }
        static bool Pressed(KeyCode key) => CampaignRules.IsFunctionKey((int)key, (int)KeyCode.F1, (int)KeyCode.F12) && Input.GetKeyDown(key);
        void OnGUI()
        {
            var game = IntegratedGame.Active;
            if (!Allowed || game == null) return;
            GUI.color = Color.yellow;
            GUI.Label(new Rect(12, Screen.height - 48, 700, 30), "개발·테스트 모드 / 랭킹 제외: " + debugKey + " 충돌범위 · " + invulnerableKey + " 무적 · " + defeatEnemiesKey + " 일반 적 제거 · " + moneyKey + " 돈");
            if (ShowColliders && Camera.main != null)
            {
                foreach (var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None)) DrawColliders(enemy.gameObject);
                foreach (var bullet in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) DrawColliders(bullet.gameObject);
                foreach (var boss in FindObjectsByType<IntegratedBossTarget>(FindObjectsSortMode.None)) DrawColliders(boss.gameObject);
            }
            GUI.color = Color.white;
        }
        void DrawColliders(GameObject target)
        {
            foreach (var collider in target.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled) continue;
                var circle = collider as CircleCollider2D;
                if (circle != null)
                {
                    Vector3 scale = circle.transform.lossyScale;
                    float radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    Vector2 center = circle.transform.TransformPoint(circle.offset);
                    Vector2 previous = center + Vector2.right * radius;
                    for (int i = 1; i <= 48; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 48;
                        Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        Line(previous, point); previous = point;
                    }
                }
                var box = collider as BoxCollider2D;
                if (box != null)
                {
                    Vector2 h = box.size * .5f;
                    Vector2[] points = { new Vector2(-h.x,-h.y),new Vector2(h.x,-h.y),new Vector2(h.x,h.y),new Vector2(-h.x,h.y) };
                    for (int i = 0; i < 4; i++) Line(box.transform.TransformPoint(points[i] + box.offset), box.transform.TransformPoint(points[(i+1)%4] + box.offset));
                }
                var polygon = collider as PolygonCollider2D;
                if (polygon != null)
                    for (int path = 0; path < polygon.pathCount; path++)
                    {
                        var points = polygon.GetPath(path);
                        for (int i = 0; i < points.Length; i++) Line(polygon.transform.TransformPoint(points[i]+polygon.offset), polygon.transform.TransformPoint(points[(i+1)%points.Length]+polygon.offset));
                    }
            }
        }
        static void Line(Vector2 a, Vector2 b)
        {
            Vector3 p = Camera.main.WorldToScreenPoint(a), q = Camera.main.WorldToScreenPoint(b);
            if (p.z <= 0 || q.z <= 0) return;
            Vector2 start = new Vector2(p.x, Screen.height-p.y), end = new Vector2(q.x, Screen.height-q.y);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y-start.y,end.x-start.x)*Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x,start.y,(end-start).magnitude,1.5f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
